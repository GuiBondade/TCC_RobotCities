using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SectionDatabase", menuName = "Block Programming/Section Database")]
public class SectionDatabase : ScriptableObject
{
    [Serializable]
    public struct SectionEntry{
        public ParameterSections section;
        public List<string> values;
    }

    private static readonly List<string> EmptyList = new List<string>(0);//pra retornar caso der erro no get values
    [SerializeField] private List<SectionEntry> sectionEntries;

    [Tooltip("Catálogo de materiais. A seção MaterialValues é preenchida sempre por aqui.")]
    [SerializeField] private MaterialDatabase materialList;
    [Tooltip("Catálogo de ferramentas. A seção ToolValues é preenchida sempre por aqui.")]
    [SerializeField] private ToolDatabase toolList;
    [Tooltip("Catálogo de entidades (construções e recursos agrupados). A seção [entidade] é preenchida sempre por aqui.")]
    [SerializeField] private EntityDatabase entityList;

    [NonSerialized] private List<string>[] valuesArray; //cache só de runtime (nunca é gravado no asset)

    public void Awake() {
        Initialize();
    }

    public void OnEnable() {
        // popula no load: os dados estáticos do MaterialList/ToolDatabase precisam chegar ao jogo
        ApplyMaterialValues();
        ApplyToolValues();
        if (entityList != null) EntityDatabase.SetInstance(entityList);

        // Desassina antes de assinar para evitar duplicação caso OnEnable rode mais de uma vez.
        ObjectiveSignals.OnUnlockGranted -= HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
        ObjectiveSignals.OnUnlockGranted += HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded += HandleObjectivesReloaded;
    }

    public void OnDisable() {
        if (entityList != null) EntityDatabase.ClearInstance(entityList);

        ObjectiveSignals.OnUnlockGranted -= HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
    }

    private void HandleUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source) {
        if (type != ObjectiveUnlockType.Tool) return;
        ApplyToolValues();
    }

    private void HandleObjectivesReloaded() {
        ApplyToolValues();
    }

    public void Initialize() {
        var totalSections = Enum.GetValues(typeof(ParameterSections)).Length;
        valuesArray = new List<string>[totalSections];

        if (sectionEntries != null) {
            foreach (var entry in sectionEntries) {
                // materiais e ferramentas NUNCA vêm do inspector: a fonte é sempre o catálogo
                if (entry.section == ParameterSections.MaterialValues
                    || entry.section == ParameterSections.ToolValues) continue;

                var index = (int)entry.section;
                if (index < 0 || index >= valuesArray.Length) continue;
                valuesArray[index] = entry.values;
            }
        }

        ApplyMaterialValues();
        ApplyToolValues();
    }

    // Preenche a seção de materiais a partir do MaterialList.
    private void ApplyMaterialValues() {
        var index = (int)ParameterSections.MaterialValues;
        if (valuesArray == null || index < 0 || index >= valuesArray.Length) return;

        var materialNames = new List<string> { "Nenhum" };

        if (materialList == null) {
            Debug.LogError("[SectionDatabase] Referência do MaterialList não atribuída — a seção de materiais ficará vazia.", this);
        } else {
            foreach (var material in materialList.Materials) {
                materialNames.Add(material.itemName);
            }

            if (materialNames.Count == 0) {
                Debug.LogWarning("[SectionDatabase] MaterialList vazio — a seção de materiais ficará sem opções.", this);
            }
        }

        // lista nova: não altera a lista que veio serializada no asset
        valuesArray[index] = materialNames;
    }

    // Preenche a seção de ferramentas a partir do ToolDatabase.
    private void ApplyToolValues() {
        int index = (int)ParameterSections.ToolValues;
        if (valuesArray == null || index < 0 || index >= valuesArray.Length) return;

        var toolNames = new List<string> { "Nenhum", "Qualquer" };
        ToolDatabase db = toolList;
        if (db == null && ExecutionManager.instancia != null) db = ExecutionManager.instancia.toolDatabase;
        if (db == null) {
            Debug.LogError("[SectionDatabase] Referência do ToolDatabase não atribuída — a seção de ferramentas ficará vazia.", this);
        } else {
            ObjectiveManager om = null;
            try { om = ObjectiveManager.instancia; } catch { om = null; }
            foreach (var tool in db.Tools) {
                if (tool == null) continue;
                // Desbloqueio por objetivo: ferramenta gerenciada só aparece se desbloqueada.
                if (om != null && om.IsToolManaged(tool) && !om.IsToolUnlocked(tool)) continue;
                toolNames.Add(tool.displayName);
            }
        }

        valuesArray[index] = toolNames;
    }

    public List<string> GetValuesForSection(ParameterSections section) {
        if (valuesArray == null || valuesArray.Length != Enum.GetValues(typeof(ParameterSections)).Length) Initialize();

        var index = (int)section;
        if (index >= 0 && index < valuesArray.Length && valuesArray[index] != null) {
            return valuesArray[index];
        }             
        
        Debug.Log("Não conseguiu pegar os valores da seção");
        return EmptyList;
    }

    // Monta a lista dinâmica da seção [entidade] a partir do catálogo de entidades.
    // A chave gravada é autodescritiva: "g:*" (gerais), "c:<key>" (construção) e
    // "r:<key>" (recurso).
    public List<ParamValue> GetEntityValues(ParameterSections section) {
        if (entityList != null) EntityDatabase.SetInstance(entityList);

        var values = new List<ParamValue>();

        switch (section) {
            case ParameterSections.EntityGeneralValues:
                values.Add(new ParamValue("Nenhuma", EntityFilter.GeneralNone));
                values.Add(new ParamValue("Qualquer", EntityFilter.GeneralAny));
                values.Add(new ParamValue("Robô", EntityFilter.GeneralRobot));
                values.Add(new ParamValue("Construção", EntityFilter.GeneralConstruction));
                values.Add(new ParamValue("Recurso", EntityFilter.GeneralResource));
                break;

            case ParameterSections.EntityConstructionValues:
                if (entityList == null) {
                    Debug.LogError("[SectionDatabase] EntityDatabase não atribuído — a seção de construção ficará vazia.", this);
                    break;
                }
                foreach (var group in entityList.ConstructionGroups) {
                    if (group == null || string.IsNullOrEmpty(group.key)) continue;
                    if (group.constructions == null || group.constructions.Count == 0) continue;
                    string label = string.IsNullOrEmpty(group.label) ? group.key : group.label;
                    values.Add(new ParamValue(label, EntityFilter.ConstructionPrefix + group.key));
                }
                break;

            case ParameterSections.EntityResourceValues:
                if (entityList == null) {
                    Debug.LogError("[SectionDatabase] EntityDatabase não atribuído — a seção de recurso ficará vazia.", this);
                    break;
                }
                foreach (var group in entityList.ResourceGroups) {
                    if (group == null || string.IsNullOrEmpty(group.key)) continue;
                    if (group.resources == null || group.resources.Count == 0) continue;
                    string label = string.IsNullOrEmpty(group.label) ? group.key : group.label;
                    values.Add(new ParamValue(label, EntityFilter.ResourcePrefix + group.key));
                }
                break;
        }

        return values;
    }
}
