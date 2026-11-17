using System.Collections.Generic;
using UnityEngine;

// Bloco de import (entidade ou script).
// Dois parâmetros no TopSlot:
//   [0] ImportTargetParameter  -> o alvo: seções Robô | Construção | Script -> a instância
//   [1] ImportElementParameter -> o que importar: seções Variável | Função | Signal -> o elemento
//
// A seção do popup é o "tipo": no alvo ela decide o caminho (entidade ou script),
// no elemento ela decide o que está sendo importado. O valor selecionado é a
// 'key' do item (id da instância, id do script, "f:Andar", "i:bateria", ...).
//
// Em runtime o bloco só segue a sequência: a referência da entidade já está
// guardada no ExecutionContext (importedEntities) e as funções importadas de
// outro script já foram copiadas para o script na mesclagem da compilação.
public class ImportBlockUI : BlockUINext
{
    public const int TargetParamIndex = 0;
    public const int ElementParamIndex = 1;

    [HideInInspector] public string targetKey;   // instance id da entidade ou id do script
    [HideInInspector] public ParameterSections targetSection = ParameterSections.Robos;
    [HideInInspector] public string elementKey = ImportKeys.All;
    [HideInInspector] public ParameterSections elementSection = ParameterSections.Variaveis;

    private bool providersReady;
    private ParameterSections lastTargetSection;
    private string lastTargetKey;
    private ParameterSections lastElementSection;
    private string lastElementKey;

    // --- Setup ---
    public override void SetupUI(BlockData newData) {
        base.SetupUI(newData);

        targetKey = "";
        targetSection = ParameterSections.Robos;
        elementKey = ImportKeys.All;
        elementSection = ParameterSections.Variaveis;
        providersReady = false;
        lastTargetSection = ParameterSections.Robos;
        lastTargetKey = "";
        lastElementSection = ParameterSections.Variaveis;
        lastElementKey = ImportKeys.All;
    }

    // Restaura um import salvo (alvo + elemento).
    public void SetupImport(string savedTargetKey, ParameterSections savedTargetSection,
                            string savedElementKey, ParameterSections savedElementSection) {
        targetKey = savedTargetKey ?? "";
        targetSection = savedTargetSection;
        elementKey = string.IsNullOrEmpty(savedElementKey) ? ImportKeys.All : savedElementKey;
        elementSection = savedElementSection;

        EnsureProviders();

        if (parameterInitialList == null) return;

        var targetRef = GetParam(TargetParamIndex);
        if (targetRef != null) {
            var targetConfig = targetRef.GetComponent<ParameterConfig>();
            if (targetConfig != null) targetConfig.RestoreState(targetKey, null, targetSection);
        }

        var elementRef = GetParam(ElementParamIndex);
        if (elementRef != null) {
            var elementConfig = elementRef.GetComponent<ParameterConfig>();
            if (elementConfig != null) elementConfig.RestoreState(elementKey, null, elementSection);
        }

        lastTargetSection = targetSection;
        lastTargetKey = targetKey;
        lastElementSection = elementSection;
        lastElementKey = elementKey;
    }

    // --- Sincronização ---
    private void Update() {
        if (parameterInitialList == null) return;

        EnsureProviders();

        var targetRef = GetParam(TargetParamIndex);
        if (targetRef == null) return;

        // --- Alvo: seção (Robô/Construção/Script) + instância escolhida ---
        ParameterSections targetSectionNow = SelectedSection(targetRef);
        string targetKeyNow = targetRef.selectedValueKey ?? "";

        if (targetSectionNow != lastTargetSection) {
            // A seção do alvo mudou (ex.: Robô -> Script): a escolha antiga não vale mais.
            lastTargetSection = targetSectionNow;
            targetSection = targetSectionNow;
            targetKey = "";
            lastTargetKey = "";
            ClearSelection(targetRef);
            ResetElementSelection();
        } else if (targetKeyNow != lastTargetKey) {
            // A instância escolhida mudou: a lista de elementos depende dela.
            lastTargetKey = targetKeyNow;
            targetKey = targetKeyNow;
            ResetElementSelection();
        }

        // --- Elemento: seção (Variável/Função/Signal) + elemento escolhido ---
        var elementRef = GetParam(ElementParamIndex);
        if (elementRef == null) return;

        ParameterSections elementSectionNow = SelectedSection(elementRef);
        string elementKeyNow = elementRef.selectedValueKey ?? "";

        if (elementSectionNow != lastElementSection) {
            lastElementSection = elementSectionNow;
            elementSection = elementSectionNow;
            elementKey = ImportKeys.All;
            lastElementKey = ImportKeys.All;
            ClearSelection(elementRef);
            SelectValueByKey(elementRef, ImportKeys.All);
        } else if (elementKeyNow != lastElementKey) {
            lastElementKey = elementKeyNow;
            elementKey = string.IsNullOrEmpty(elementKeyNow) ? ImportKeys.All : elementKeyNow;
        }
    }

    // Limpa a seleção de valor do popup (sem mexer na seção escolhida).
    private static void ClearSelection(ReferenceHolder refs) {
        if (refs == null) return;
        if (refs.selectedValueVisual != null && refs.selectedValueVisual.checkmark != null) {
            refs.selectedValueVisual.checkmark.enabled = false;
        }
        refs.selectedValueVisual = null;
        refs.selectedValueKey = null;
    }

    // --- Listas dinâmicas ---
    // As listas do popup são calculadas aqui (não vêm do SectionDatabase).
    private void EnsureProviders() {
        var targetRef = GetParam(TargetParamIndex);
        if (targetRef != null && targetRef.dynamicValueProvider == null) {
            targetRef.dynamicValueProvider = GetTargetValues;
        }

        var elementRef = GetParam(ElementParamIndex);
        if (elementRef != null && elementRef.dynamicValueProvider == null) {
            elementRef.dynamicValueProvider = GetElementValues;
        }

        providersReady = targetRef != null && elementRef != null;
        if (!providersReady) return;

        // Garante que os dois popups tenham uma seção selecionada (com a lista montada).
        EnsureSectionSelected(targetRef, ParameterSections.Robos);
        EnsureSectionSelected(elementRef, ParameterSections.Variaveis);
    }

    private List<ParamValue> GetTargetValues(ParameterSections section) {
        var values = new List<ParamValue>();

        switch (section) {
            case ParameterSections.Robos: {
                var entities = MapManager.instancia?.GetEntitiesBySection(MapManager.SectionRobot);
                AddEntities(values, entities);
                break;
            }
            case ParameterSections.Construcoes: {
                var entities = MapManager.instancia?.GetEntitiesBySection(MapManager.SectionConstruction);
                AddEntities(values, entities);
                break;
            }
            case ParameterSections.Scripts: {
                var scripts = ScriptManager.instancia?.scripts;
                if (scripts != null) {
                    foreach (var script in scripts) {
                        if (script == null) continue;
                        string label = string.IsNullOrEmpty(script.name) ? ("Script " + script.id) : script.name;
                        values.Add(new ParamValue(label, script.id.ToString()));
                    }
                }
                break;
            }
        }

        return values;
    }

    private List<ParamValue> GetElementValues(ParameterSections section) {
        var values = new List<ParamValue>();
        var target = ResolveTarget();

        switch (section) {
            case ParameterSections.Variaveis: {
                // Variáveis: só a origem Robô/Construção oferece (internas + as do script que o robô executa).
                if (target is IMapEntity entity) {
                    values.Add(new ParamValue("Tudo", ImportKeys.All));

                    var internalVars = entity.GetExposedVariables();
                    if (internalVars != null) {
                        foreach (var varName in internalVars) {
                            if (string.IsNullOrEmpty(varName)) continue;
                            values.Add(new ParamValue(varName, ImportKeys.InternalVarPrefix + varName));
                        }
                    }

                    var scriptVars = entity.GetScriptVariableNames();
                    if (scriptVars != null) {
                        foreach (var varName in scriptVars) {
                            if (string.IsNullOrEmpty(varName)) continue;
                            values.Add(new ParamValue(varName + " (do script)", ImportKeys.ScriptVarPrefix + varName));
                        }
                    }
                }
                break;
            }
            case ParameterSections.Funcoes: {
                // Funções: só a origem Script oferece (as exportadas).
                if (target is ScriptData script) {
                    values.Add(new ParamValue("Tudo", ImportKeys.All));

                    var catalog = ScriptManager.instancia?.GetCatalog(script);
                    if (catalog != null) {
                        foreach (var function in catalog.ExportedFunctions()) {
                            if (function == null || string.IsNullOrEmpty(function.name)) continue;
                            values.Add(new ParamValue(function.name, ImportKeys.FunctionPrefix + function.name));
                        }
                    }
                }
                break;
            }
            case ParameterSections.Signals: {
                values.Add(new ParamValue("Tudo", ImportKeys.All));

                if (target is IMapEntity entity) {
                    var signals = entity.GetExposedSignals();
                    if (signals != null) {
                        foreach (var signal in signals) {
                            if (string.IsNullOrEmpty(signal)) continue;
                            values.Add(new ParamValue(signal, ImportKeys.SignalPrefix + signal));
                        }
                    }
                } else if (target is ScriptData script) {
                    var catalog = ScriptManager.instancia?.GetCatalog(script);
                    if (catalog != null) {
                        foreach (var signal in catalog.signals) {
                            if (string.IsNullOrEmpty(signal)) continue;
                            values.Add(new ParamValue(signal, ImportKeys.SignalPrefix + signal));
                        }
                    }
                }
                break;
            }
        }

        return values;
    }

    private static void AddEntities(List<ParamValue> values, List<IMapEntity> entities) {
        if (entities == null) return;
        foreach (var entity in entities) {
            if (entity == null) continue;
            string label = MapManager.GetEntityDisplayName(entities, entity);
            if (string.IsNullOrEmpty(label)) continue;
            values.Add(new ParamValue(label, entity.GetEntityId().ToString()));
        }
    }

    // O alvo atual: entidade (Robô/Construção) ou script.
    private object ResolveTarget() {
        if (targetSection == ParameterSections.Scripts) {
            if (!int.TryParse((targetKey ?? "").Trim(), out int scriptId)) return null;
            return ScriptManager.instancia?.FindScriptById(scriptId);
        }

        if (!int.TryParse((targetKey ?? "").Trim(), out int instanceId) || instanceId == 0) return null;
        string section = targetSection == ParameterSections.Construcoes
            ? MapManager.SectionConstruction
            : MapManager.SectionRobot;
        return MapManager.instancia?.FindEntity(section, instanceId, null);
    }

    // --- Auxiliares ---
    private ReferenceHolder GetParam(int index) {
        if (parameterInitialList == null) return null;
        if (index < 0 || index >= parameterInitialList.Count) return null;
        return parameterInitialList[index];
    }

    // A seção selecionada no popup (o cabeçalho); se não houver, a seção do valor escolhido.
    private static ParameterSections SelectedSection(ReferenceHolder refs) {
        if (refs == null) return ParameterSections.Robos;
        if (refs.selectedSection != null) return refs.selectedSection.sectionCurrent;
        return refs.selectedValueSection;
    }

    // Deixa uma seção selecionada (e a lista dela montada) se ainda não houver nenhuma.
    private static void EnsureSectionSelected(ReferenceHolder refs, ParameterSections fallback) {
        if (refs == null || refs.selectedSection != null) return;
        if (refs.sectionContent == null) return;

        var config = refs.GetComponent<ParameterConfig>();
        if (config == null) return;

        var sectionInfo = FindSectionInfo(refs, fallback);
        if (sectionInfo == null) {
            var content = refs.sectionContent.transform;
            if (content.childCount == 0) return;
            sectionInfo = content.GetChild(0).GetComponent<SectionInfo>();
        }
        if (sectionInfo == null) return;

        config.UpdateValues(sectionInfo);
        config.UpdateSelectedSection(sectionInfo);
    }

    private static SectionInfo FindSectionInfo(ReferenceHolder refs, ParameterSections section) {
        if (refs?.sectionContent == null) return null;
        var content = refs.sectionContent.transform;
        for (int i = 0; i < content.childCount; i++) {
            var info = content.GetChild(i).GetComponent<SectionInfo>();
            if (info != null && info.sectionCurrent == section) return info;
        }
        return null;
    }

    // O alvo mudou: a seleção do elemento volta para "Tudo" e a lista é reconstruída.
    private void ResetElementSelection() {
        var elementRef = GetParam(ElementParamIndex);
        if (elementRef == null) return;

        ClearSelection(elementRef);
        elementRef.selectedVariableName = null;

        if (elementRef.placeholderLabel != null) {
            var paramData = BlocksFactory.instance?.blockRegistry?.GetParamData(elementRef.type);
            string defaultName = paramData != null ? paramData.defaultParamName : elementRef.placeholderLabel.text;
            elementRef.placeholderLabel.text = defaultName;
            elementRef.placeholderLabel.ForceMeshUpdate();
        }

        var config = elementRef.GetComponent<ParameterConfig>();
        if (config == null) return;

        config.ResetValues();

        // Deixa "Tudo" selecionado (primeiro item da lista), quando existe.
        SelectValueByKey(elementRef, ImportKeys.All);
    }

    private static void SelectValueByKey(ReferenceHolder refs, string key) {
        if (refs == null || refs.valueContent == null) return;
        var config = refs.GetComponent<ParameterConfig>();
        if (config == null) return;

        var content = refs.valueContent.transform;
        for (int i = 0; i < content.childCount; i++) {
            var value = content.GetChild(i).GetComponent<ValueInfo>();
            if (value == null || value.value != key) continue;
            ClearSelection(refs);
            config.ValuePressionado(value);
            return;
        }
    }

    // --- Save ---
    // Lê direto dos parâmetros (fonte da verdade) pra não depender do frame do Update.
    public string GetTargetKeyForSave() {
        var targetRef = GetParam(TargetParamIndex);
        if (targetRef == null) return targetKey ?? "";
        return targetRef.selectedValueKey ?? "";
    }

    public ParameterSections GetTargetSectionForSave() {
        var targetRef = GetParam(TargetParamIndex);
        if (targetRef == null) return targetSection;
        return SelectedSection(targetRef);
    }

    public string GetElementKeyForSave() {
        var elementRef = GetParam(ElementParamIndex);
        if (elementRef == null) return elementKey ?? ImportKeys.All;
        string key = elementRef.selectedValueKey;
        return string.IsNullOrEmpty(key) ? ImportKeys.All : key;
    }

    public ParameterSections GetElementSectionForSave() {
        var elementRef = GetParam(ElementParamIndex);
        if (elementRef == null) return elementSection;
        return SelectedSection(elementRef);
    }
}
