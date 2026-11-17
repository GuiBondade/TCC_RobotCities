using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ParameterConfig : MonoBehaviour
{
    public ReferenceHolder refs;

    private BlockerManager blocker;

    public void Start() {
        blocker = BlockerManager.instancia;
    }

    public void OnTriggerButtonPress() {
        blocker.SetUIOnBlocker(refs.selectionArea);
    }

    // Chamado pelo input do parâmetro de signal ao terminar de digitar (enter/perda de foco).
    // Adiciona o signal na lista UI (se não existir) ou altera a string lá (com propagação),
    // e seleciona o botão do signal resultante, como se o usuário tivesse clicado nele.
    public void OnSignalInputEndEdit(string text) {
        var paramSetup = GetComponent<ParameterSetup>();
        if (paramSetup == null || paramSetup.paramData == null || !paramSetup.paramData.CanInputText) return;

        // o onEndEdit também dispara quando o input é desativado (ex.: selecionar um signal
        // existente no popup esconde o input) — nesse caso não registra nada
        if (refs.inputTextInstance == null || !refs.inputTextInstance.activeSelf) return;

        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        string finalName = string.IsNullOrEmpty(refs.selectedSignalName)
            ? refs.executionManager.RegisterSignal(text)
            : refs.executionManager.RenameSignal(refs.selectedSignalName, text);

        if (string.IsNullOrEmpty(finalName)) return;

        refs.selectedSignalName = finalName;

        SelectValueByName(finalName);
    }

    // Chamado pelo input do parâmetro de texto (StringParameter) ao terminar de digitar.
    // Diferente do signal: só guarda o texto — nada de registrar/renomear no ExecutionManager.
    public void OnStringInputEndEdit(string text) {
        var paramSetup = GetComponent<ParameterSetup>();
        if (paramSetup == null || paramSetup.paramData == null || !paramSetup.paramData.CanInputText) return;

        // o onEndEdit também dispara quando o input é desativado (ex.: escolher uma variável
        // no popup esconde o input) — nesse caso não altera nada
        if (refs.inputTextInstance == null || !refs.inputTextInstance.activeSelf) return;

        text = text?.Trim();
        refs.selectedVariableName = null;
        refs.selectedSignalName = null;
        refs.selectedFunctionName = null;

        // o sentinel fica selecionado; o texto real é salvo lendo o input no WorkspaceSaveScript
        string sentinel = paramSetup.GetValueAt(ParameterSections.StringValues, 0);
        refs.selectedValueKey = string.IsNullOrEmpty(sentinel) ? text : sentinel;
        refs.selectedValueSection = ParameterSections.StringValues;

        refs.placeholderLabel.text = string.IsNullOrEmpty(text) ? paramSetup.paramData.defaultParamName : text;
        refs.placeholderLabel.ForceMeshUpdate();
        GetComponent<AdjustWidthByText>().AdjustWidth();
    }

    // Rebuilda os values da seção atual e reaplica a seleção do item com esse nome
    // (como se o usuário tivesse clicado nele). Compartilhado por signal e função.
    private void SelectValueByName(string finalName) {
        if (refs.selectedSection == null) return;

        UpdateValues(refs.selectedSection); // rebuild inclui o valor novo/renomeado

        ValueInfo valueToSelect = null;
        for (int i = 0; i < refs.valueContent.transform.childCount; i++) {
            var value = refs.valueContent.transform.GetChild(i).GetComponent<ValueInfo>();
            if (value != null && value.value == finalName) {
                valueToSelect = value;
                break;
            }
        }

        if (valueToSelect != null) {
            refs.selectedValueKey = null; // força ValuePressionado a reaplicar o estado (mesmo nome após rename)
            refs.selectedValueVisual = null; // o visual antigo foi devolvido ao pool no UpdateValues
            ValuePressionado(valueToSelect);
        }
    }

    public void SectionPressionado(SectionInfo sectionPressed) { 
        // da pra subtituir sectionPressed por this?
        if (sectionPressed != refs.selectedSection) { // se mudou o section selecionado
            UpdateValues(sectionPressed);
            UpdateSelectedSection(sectionPressed);
            if (refs.selectedValueVisual != null) {
                if (sectionPressed.sectionCurrent == refs.selectedValueSection) {
                    refs.selectedValueVisual.checkmark.enabled = true;
                } else {
                    refs.selectedValueVisual.checkmark.enabled = false;
                }
            }
        }
    }

    public void ValuePressionado(ValueInfo valuePressed) {
        bool sameValue = refs.selectedValueKey != null
            && valuePressed.value == refs.selectedValueKey;
        bool needsOperandRebuild = sameValue
            && NeedsOperandRebuild(valuePressed.sectionCurrent, valuePressed.value);

        if (!sameValue || needsOperandRebuild) {
            // refs.placeholderLabel vai ser o valor final(por enquanto), depois vai ter somente valor equivalente ao atribuido ao valor final
            // atribuir valor no setup/config para leitura pratica no save
            string valueText = valuePressed.label.text;
            refs.placeholderLabel.text = valueText; 
            refs.placeholderLabel.ForceMeshUpdate();

            bool sectionChanged = valuePressed.sectionCurrent != refs.selectedValueSection;
            bool operationShapeChanged = valuePressed.sectionCurrent == ParameterSections.BoolLogicOperations
                && refs.selectedValueKey != null
                && IsUnaryOperation(refs.selectedValueKey) != IsUnaryOperation(valuePressed.value);

            if (sectionChanged || operationShapeChanged || needsOperandRebuild) {
                ResetParameterState();
                ConfigureOperands(valuePressed.sectionCurrent, valuePressed.value);
            }

            var paramSetup = GetComponent<ParameterSetup>();
            string signalSentinel = paramSetup.GetValueAt(ParameterSections.SignalValues, 0);

            if (valuePressed.sectionCurrent == ParameterSections.SignalValues) {
                refs.selectedVariableName = null;
                refs.selectedFunctionName = null;
                bool isTypedSignal = !string.IsNullOrEmpty(signalSentinel) && valuePressed.value == signalSentinel;
                if (isTypedSignal) {
                    // "Digitar...": mostra o input já com o signal atual (se houver) pra permitir editar/renomear
                    if (refs.inputTextInstance != null) {
                        var inputField = refs.inputTextInstance.GetComponent<TMP_InputField>();
                        inputField.text = refs.selectedSignalName ?? string.Empty;
                        refs.inputTextInstance.SetActive(true);
                    }
                } else {
                    refs.selectedSignalName = valueText;
                    if (refs.inputTextInstance != null) refs.inputTextInstance.SetActive(false);
                }
            } else if (valuePressed.sectionCurrent == ParameterSections.FunctionValues) {
                refs.selectedVariableName = null;
                refs.selectedSignalName = null;
                refs.selectedFunctionName = valueText;
                if (refs.inputTextInstance != null) refs.inputTextInstance.SetActive(false);
            } else if (valuePressed.sectionCurrent == ParameterSections.StringValues) {
                // texto livre: a seção só tem o sentinel "Digitar..." — mostra o input
                refs.selectedVariableName = null;
                refs.selectedSignalName = null;
                refs.selectedFunctionName = null;
                if (refs.inputTextInstance != null) {
                    var inputField = refs.inputTextInstance.GetComponent<TMP_InputField>();
                    // mantém o texto já digitado (o input é a fonte da verdade do parâmetro)
                    if (inputField != null && !string.IsNullOrEmpty(inputField.text)) {
                        refs.placeholderLabel.text = inputField.text;
                        refs.placeholderLabel.ForceMeshUpdate();
                    }
                    refs.inputTextInstance.SetActive(true);
                    if (inputField != null) inputField.ActivateInputField();
                }
            } else if (IsVariableOfSection(valueText, valuePressed.sectionCurrent)) {
                refs.selectedVariableName = valueText;
                refs.selectedSignalName = null;
                refs.selectedFunctionName = null;
                if (refs.inputTextInstance != null) refs.inputTextInstance.SetActive(false);
            } else {
                refs.selectedVariableName = null;
                refs.selectedSignalName = null;
                refs.selectedFunctionName = null;
                if (refs.inputTextInstance != null) {
                    bool isInputValue = valuePressed.sectionCurrent == ParameterSections.IntValues
                        && valuePressed.value == paramSetup.GetValueAt(ParameterSections.IntValues, 0);
                    refs.inputTextInstance.SetActive(isInputValue);
                }
            }
            Transform atual = transform.parent; // Começa do pai direto
            while (atual != null) {
                LayoutRebuilder.ForceRebuildLayoutImmediate(atual.GetComponent<RectTransform>());
                var componente = atual.GetComponent<ParameterConfig>();
                if (componente == null) break;
                atual = atual.parent; // Sobe um nível
            }

            GetComponent<AdjustWidthByText>().AdjustWidth();

            UpdateSelectedValue(valuePressed);
        }
        blocker.Reset();
    }

    private bool IsUnaryOperation(string operation) {
        return operation == "NÃO";
    }

    // Uma variável só é selecionada se ela pertence à mesma seção do valor clicado
    // (evita que uma variável chamada "Pedra" sequestre a opção "Pedra" da seção de entidade).
    private bool IsVariableOfSection(string variableName, ParameterSections section) {
        if (section == ParameterSections.VariableValues)
            return refs.variableManager.variables.ContainsKey(variableName);
        return refs.variableManager.variables.TryGetValue(variableName, out var info)
            && info.section == section;
    }

    private bool NeedsOperandRebuild(ParameterSections section, string operation) {
        if (section != ParameterSections.BoolLogicOperations) return false;

        if (IsUnaryOperation(operation)) {
            return refs.leftOperand != null || refs.rightOperand == null;
        }

        return refs.leftOperand == null || refs.rightOperand == null;
    }

    private void SetUnarySpacerActive(bool isActive) {
        if (refs.unarySpacer != null) {
            refs.unarySpacer.SetActive(isActive);
        }
    }

    private void ConfigureOperands(ParameterSections section, string operation) {
        switch (section) {
            case ParameterSections.BoolLogicOperations:
                SetUnarySpacerActive(IsUnaryOperation(operation));
                refs.parentesesInstance.SetActive(true);
                if (IsUnaryOperation(operation)) {
                    InstantiateParameter(ref refs.rightOperand, ParameterType.BooleanParameter, false, "Boolean");
                } else {
                    InstantiateParameter(ref refs.leftOperand, ParameterType.BooleanParameter, true, "Boolean");
                    InstantiateParameter(ref refs.rightOperand, ParameterType.BooleanParameter, false, "Boolean");
                }
                refs.parentesesInstance.transform.SetAsFirstSibling();
                break;
            case ParameterSections.BoolAllComparisonOperations:
                refs.parentesesInstance.SetActive(true);
                InstantiateParameter(ref refs.leftOperand, ParameterType.AllTypeParameter, true, "All Types");
                InstantiateParameter(ref refs.rightOperand, ParameterType.AllTypeParameter, false, "All Types");
                refs.parentesesInstance.transform.SetAsFirstSibling();
                break;
            case ParameterSections.BoolEntityOperations:
                SetUnarySpacerActive(false);
                refs.parentesesInstance.SetActive(true);
                InstantiateParameter(ref refs.leftOperand, ParameterType.DirectionParameter, true, "Direção");
                InstantiateParameter(ref refs.rightOperand, ParameterType.EntityParameter, false, "Entidade");
                refs.parentesesInstance.transform.SetAsFirstSibling();
                break;
            case ParameterSections.IntOperations:
            case ParameterSections.BoolIntComparisonOperations:
                refs.parentesesInstance.SetActive(true);
                InstantiateParameter(ref refs.leftOperand, ParameterType.IntParameter, true, "Int");
                InstantiateParameter(ref refs.rightOperand, ParameterType.IntParameter, false, "Int");
                refs.parentesesInstance.transform.SetAsFirstSibling();
                break;
            case ParameterSections.BoolValues:
            case ParameterSections.DirectionValues:
            case ParameterSections.MaterialValues:
            case ParameterSections.ToolValues:
            case ParameterSections.SignalValues:
            case ParameterSections.FunctionValues:
            case ParameterSections.StringValues:
            case ParameterSections.VariableValues:
            case ParameterSections.EntityGeneralValues:
            case ParameterSections.EntityConstructionValues:
            case ParameterSections.EntityResourceValues:
            case ParameterSections.Robos:
            case ParameterSections.Construcoes:
            case ParameterSections.Scripts:
            case ParameterSections.Variaveis:
            case ParameterSections.Funcoes:
            case ParameterSections.Signals:
            case ParameterSections.IntValues:
                SetUnarySpacerActive(false);
                break;
            default:
                Debug.Log("Deu erro ao pressionar valor, parece que tem opção de seção a mais no Setup(ParameterSections) do que no Config(ValuePressionado");
                break;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(refs.rootParameter.GetComponent<RectTransform>());
    }

    public void InstantiateParameter(ref ReferenceHolder instanceReference, ParameterType type, bool isFirst, string label)
    {
        var parameterRefs = refs.blocksFactory.InitializeParameter(label, type, transform);
        instanceReference = parameterRefs;
        if (isFirst) parameterRefs.transform.SetAsFirstSibling();
        else parameterRefs.transform.SetAsLastSibling();

        LayoutRebuilder.ForceRebuildLayoutImmediate(parameterRefs.GetComponent<RectTransform>()); //precisa?
    }


    public void UpdateSelectedSection(SectionInfo sectionPressed) { // tem que ALTERAR porque ta so funfando pra section(?, nao sei mais)
        if (refs.selectedSection != null) refs.selectedSection.checkmark.enabled = false;
        refs.selectedSection = sectionPressed;
        sectionPressed.checkmark.enabled = true;
    }

    public void UpdateSelectedValue(ValueInfo valuePressed) { 
        if (refs.selectedValueVisual != null) refs.selectedValueVisual.checkmark.enabled = false;
        refs.selectedValueVisual = valuePressed;
        refs.selectedValueKey = valuePressed.value;
        refs.selectedValueSection = valuePressed.sectionCurrent;
        valuePressed.checkmark.enabled = true;
    }

    public void RestoreState(string savedValue, string savedVariableName, ParameterSections savedSection) {
        for (int i = 0; i < refs.sectionContent.transform.childCount; i++) {
            var section = refs.sectionContent.transform.GetChild(i).GetComponent<SectionInfo>();
            if (section != null && section.checkmark != null) section.checkmark.enabled = false;
        }
        for (int i = 0; i < refs.valueContent.transform.childCount; i++) {
            var value = refs.valueContent.transform.GetChild(i).GetComponent<ValueInfo>();
            if (value != null && value.checkmark != null) value.checkmark.enabled = false;
        }

        refs.selectedSection = null;
        refs.selectedValueVisual = null;
        refs.selectedValueKey = null;
        refs.selectedValueSection = ParameterSections.BoolValues;
        refs.selectedVariableName = null;
        refs.selectedSignalName = null;
        if (refs.inputTextInstance != null) refs.inputTextInstance.SetActive(false);

        SectionInfo sectionToSelect = null;
        for (int i = 0; i < refs.sectionContent.transform.childCount; i++) {
            var section = refs.sectionContent.transform.GetChild(i).GetComponent<SectionInfo>();
            if (section != null && section.sectionCurrent == savedSection) {
                sectionToSelect = section;
                break;
            }
        }

        if (sectionToSelect == null && refs.sectionContent.transform.childCount > 0) {
            sectionToSelect = refs.sectionContent.transform.GetChild(0).GetComponent<SectionInfo>();
        }

        if (sectionToSelect == null) return;

        // signal: garante que o signal salvo existe na lista UI antes de reconstruir os values
        // (a lista UI é limpa ao abrir o script; aqui ele é re-registrado)
        if (refs.type == ParameterType.SignalParameter && !string.IsNullOrEmpty(savedValue)) {
            refs.executionManager.RegisterSignal(savedValue);
        }

        // função: mesmo esquema do signal (a lista UI de funções é limpa ao abrir o script;
        // o declare re-registra, mas se o call vier antes no arquivo garantimos aqui também)
        if (refs.type == ParameterType.FunctionParameter && !string.IsNullOrEmpty(savedValue)) {
            refs.executionManager.RegisterFunction(savedValue);
        }

        UpdateValues(sectionToSelect);
        UpdateSelectedSection(sectionToSelect);

        string selectedValue = string.IsNullOrEmpty(savedVariableName) ? savedValue : savedVariableName;
        bool isTypedInt = string.IsNullOrEmpty(savedVariableName)
            && sectionToSelect.sectionCurrent == ParameterSections.IntValues
            && int.TryParse(savedValue, out _);

        // texto livre (StringParameter): o valor salvo é o próprio texto digitado
        bool isTypedString = string.IsNullOrEmpty(savedVariableName)
            && sectionToSelect.sectionCurrent == ParameterSections.StringValues;

        if (isTypedInt) {
            selectedValue = GetComponent<ParameterSetup>().GetValueAt(ParameterSections.IntValues, 0);
            if (refs.inputTextInstance != null) {
                refs.inputTextInstance.GetComponent<TMP_InputField>().text = savedValue;
                refs.inputTextInstance.SetActive(true);
            }
        } else if (isTypedString) {
            selectedValue = GetComponent<ParameterSetup>().GetValueAt(ParameterSections.StringValues, 0);
            if (refs.inputTextInstance != null) {
                refs.inputTextInstance.GetComponent<TMP_InputField>().text = savedValue ?? string.Empty;
                refs.inputTextInstance.SetActive(true);
            }
            // o placeholder mostra o texto digitado (o sentinel só existe na lista do popup)
            var paramSetup = GetComponent<ParameterSetup>();
            refs.placeholderLabel.text = string.IsNullOrEmpty(savedValue)
                ? (paramSetup.paramData != null ? paramSetup.paramData.defaultParamName : string.Empty)
                : savedValue;
            refs.placeholderLabel.ForceMeshUpdate();
        } else if (refs.type == ParameterType.StringParameter && refs.inputTextInstance != null) {
            // variável: o input não representa o valor — limpa o prefill que o Setup fez com o nome
            refs.inputTextInstance.GetComponent<TMP_InputField>().text = string.Empty;
        }

        ValueInfo valueToSelect = null;
        for (int i = 0; i < refs.valueContent.transform.childCount; i++) {
            var value = refs.valueContent.transform.GetChild(i).GetComponent<ValueInfo>();
            if (value != null && value.value == selectedValue) {
                valueToSelect = value;
                break;
            }
        }

        if (valueToSelect == null) return;

        bool hasLogicalOperation = savedSection == ParameterSections.BoolLogicOperations;
        bool mustRebuildLogicalOperation = hasLogicalOperation
            && NeedsOperandRebuild(savedSection, selectedValue);

        if (mustRebuildLogicalOperation) {
            ResetParameterState();
            ConfigureOperands(savedSection, selectedValue);
            UpdateSelectedSection(sectionToSelect);
        } else if (hasLogicalOperation) {
            SetUnarySpacerActive(IsUnaryOperation(selectedValue));
            refs.parentesesInstance.SetActive(true);
        }

        UpdateSelectedValue(valueToSelect);
        refs.selectedVariableName = savedVariableName;
        if (refs.type == ParameterType.SignalParameter) refs.selectedSignalName = selectedValue;
        if (refs.type == ParameterType.FunctionParameter) refs.selectedFunctionName = selectedValue;
        refs.placeholderLabel.text = isTypedString && !string.IsNullOrEmpty(savedValue)
            ? savedValue
            : valueToSelect.label.text;
        refs.placeholderLabel.ForceMeshUpdate();

        bool keepsTypedTextVisible = refs.type == ParameterType.SignalParameter
            || (refs.type == ParameterType.StringParameter && isTypedString);
        if (!isTypedInt && !keepsTypedTextVisible && refs.inputTextInstance != null) {
            refs.inputTextInstance.SetActive(false);
        }
    }

    public void UpdateValues(SectionInfo sectionInfo) { //operations / values
        for (int i = refs.valueContent.transform.childCount - 1; i >= 0; i--)
        {
            var value = refs.valueContent.transform.GetChild(i);
            var valueInfo = value.GetComponent<ValueInfo>();
            refs.blocksFactory.CleanUpValue(valueInfo);
        }
        var section = sectionInfo.sectionCurrent;

        // Lista dinâmica (bloco de import): o provider do bloco monta os itens da seção.
        // Tem precedência sobre a lista estática (o valor salvo é a 'key' do item).
        if (refs.dynamicValueProvider != null)
        {
            var dynamicValues = refs.dynamicValueProvider(section);
            if (dynamicValues != null)
            {
                foreach (var item in dynamicValues)
                {
                    refs.blocksFactory.InitializeValue(item.label, section, refs.valueContent.transform, false, item.key);
                }
                refs.valueContent.GetComponent<AdjustWidthByText>().AdjustWidth();
                return;
            }
        }

        var existingValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string valor in sectionInfo.valueList) {
            existingValues.Add(valor);
            refs.blocksFactory.InitializeValue(valor, section, refs.valueContent.transform);
        }
        var variablesNames = refs.variableManager.GetVarNamesBySection(section);

        foreach (string variable in variablesNames) {
            if (!existingValues.Add(variable)) continue;
            refs.blocksFactory.InitializeValue(variable, section, refs.valueContent.transform);
        }

        // signals: mesma ideia das variáveis — opções = lista UI + chaves do dicionário geral do ExecutionManager
        if (section == ParameterSections.SignalValues) {
            var signalNames = refs.executionManager.GetSignalOptions();

            foreach (string signal in signalNames) {
                if (!existingValues.Add(signal)) continue;
                refs.blocksFactory.InitializeValue(signal, section, refs.valueContent.transform);
            }
        }

        // funções: lista viva do ExecutionManager (espelho dos signals), alimenta o popup do call
        if (section == ParameterSections.FunctionValues) {
            var functionNames = refs.executionManager.GetFunctionOptions();

            foreach (string function in functionNames) {
                refs.blocksFactory.InitializeValue(function, section, refs.valueContent.transform);
            }
        }

        // variáveis (seção única do parâmetro de texto): lista viva com todas as variáveis
        if (section == ParameterSections.VariableValues) {
            foreach (string variable in refs.variableManager.variables.Keys) {
                if (!existingValues.Add(variable)) continue;
                refs.blocksFactory.InitializeValue(variable, section, refs.valueContent.transform);
            }
        }

        refs.valueContent.GetComponent<AdjustWidthByText>().AdjustWidth();
    }

    public void ResetValues() {
        if (refs.selectedSection is SectionInfo section) UpdateValues(section);
    }

    public void ResetParameterState() { // chamar cleanup no refs.blocksFactory
        if (refs.inputTextInstance != null) refs.inputTextInstance.SetActive(false);
        refs.parentesesInstance.SetActive(false);
        SetUnarySpacerActive(false);

        if (refs.selectedSection != null) refs.selectedSection.checkmark.enabled = false;
        if (refs.selectedValueVisual != null) refs.selectedValueVisual.checkmark.enabled = false;

        refs.selectedValueVisual = null;
        refs.selectedValueKey = null;
        refs.selectedValueSection = ParameterSections.BoolValues;
        refs.selectedSignalName = null;
        refs.selectedFunctionName = null;

        if (refs.leftOperand != null) {
            refs.blocksFactory.CleanUpParameter(refs.leftOperand);
            refs.leftOperand = null;
        }
        if (refs.rightOperand != null) {
            refs.blocksFactory.CleanUpParameter(refs.rightOperand);
            refs.rightOperand = null;
        }
    }
}
