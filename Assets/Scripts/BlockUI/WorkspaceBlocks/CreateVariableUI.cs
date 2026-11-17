using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

public class CreateVariableUI : BlockUIWorkspaceGeneric
{
    private VariableManager variableManager; 
    [SerializeField] private SectionDatabase sectionDatabase;
    // Toggle "Exportar": variável pode ser importada por outro script.
    [SerializeField] private ExportToggleUI exportToggle;
    
    public TMP_InputField inputVarName;
    public TMP_Dropdown varTypeDropdown;
    public TMP_Dropdown defaultValueDropdown;
    public TMP_InputField inputIntValue;

    private int selectedTypeIndex = -1; //qual tipo da variavel criada (precisa ser index?)

    [SerializeField] private string _variableName;
    public string variableName {
        get => _variableName;
        set => _variableName = value;
    }

    void Awake() {
        variableManager = VariableManager.instancia;
        Debug.Log(variableManager);
    }

    // Variável exportada: pode ser importada por outro script.
    public bool IsExported => exportToggle != null && exportToggle.IsExported;

    public void Setup() {
        if (variableManager == null) variableManager = VariableManager.instancia;
    
        variableName = variableManager.CreateVariable(variableManager.defaultVarName); // vai ser "Nova Variável", nova referencia dentro do variable Manager  
        
        varTypeDropdown.value = 0; // deixa o default
        if (defaultValueDropdown.value == 0)
            OnDefaultValueDropdownSelect(0);
        defaultValueDropdown.value = 0;

        if (exportToggle != null) exportToggle.Setup(false);

        /* inputIntValue.text = null;
        inputIntValue.gameObject.SetActive(false); */

        inputVarName.text = variableName;
        Debug.Log(variableName);
    }

    public void SetupWithData(string varName, ParameterType varType, ParameterSections section, string defaultValue, bool exported = false) {
        if (variableManager == null) variableManager = VariableManager.instancia;
        
        // 1. Seta os nomes e textos iniciais
        variableName = variableManager.CreateVariable(varName);
        VariableManager.instancia.UpdateVariableData(variableName, varType, section, defaultValue);
        inputVarName.text = variableName;

        if (exportToggle != null) exportToggle.SetExported(exported);

        // 2. Sincroniza o Dropdown de Tipo visualmente
        string varTypeString = varType.ToString();
        int typeIndex = varTypeDropdown.options.FindIndex(o => o.text == varTypeString);
        if (typeIndex != -1) {
            selectedTypeIndex = typeIndex;
            varTypeDropdown.SetValueWithoutNotify(typeIndex); // SetValueWithoutNotify evita loops de eventos
        }

        // 3. Atualiza as opções do Dropdown de Valores baseado na seção salva
        var valuesList = sectionDatabase.GetValuesForSection(section);
        defaultValueDropdown.ClearOptions();
        defaultValueDropdown.AddOptions(valuesList);

        // 4. Configura o campo numérico (Int) ou o Dropdown de valor padrão
        if (section == ParameterSections.IntValues) {
            inputIntValue.gameObject.SetActive(true);
            inputIntValue.text = defaultValue;
        } else {
            inputIntValue.text = null;
            inputIntValue.gameObject.SetActive(false);
            
            // Sincroniza o index do dropdown de valores para Bool, Direction, Material, etc.
            int valueIndex = valuesList.IndexOf(defaultValue);
            if (valueIndex != -1) {
                defaultValueDropdown.SetValueWithoutNotify(valueIndex);
            }
        }
    }

    public void SelectVarType(int index) {
        if (index != selectedTypeIndex) {
            selectedTypeIndex = index;
            string varTypeString = varTypeDropdown.options[index].text; 
            ParameterType varType;
            if (Enum.TryParse<ParameterType>(varTypeString, out ParameterType type)) {
                varType = type;
            } else {
                varType = ParameterType.BooleanParameter;
            }

            ParameterSections section = ParameterSetup.SectionForType(varType);
            var valuesList = sectionDatabase.GetValuesForSection(section);
            defaultValueDropdown.ClearOptions();
            defaultValueDropdown.AddOptions(valuesList);
            if (section == ParameterSections.IntValues) {
                inputIntValue.gameObject.SetActive(true);
            } else {
                inputIntValue.text = null;
                inputIntValue.gameObject.SetActive(false);
            }

            string defaultValue = valuesList.Count > 0 ? valuesList[0] : string.Empty;
            variableManager.UpdateVariableData(variableName, varType, section, defaultValue); // ja chama handle variable retyped
        }
    }

    public void OnInputValueEndEdit(string text) {
        variableManager.UpdateVariableData(variableName, null, null, text);
    }

    public void OnDefaultValueDropdownSelect(int index) {
        if (defaultValueDropdown == null || index < 0 || index >= defaultValueDropdown.options.Count) return;

        var text = defaultValueDropdown.options[index].text;
        Debug.Log("alterou value de create variable para: " + text);
        variableManager.UpdateVariableData(variableName, null, null, text);
    }

    public void OnInputFieldEndEdit(string text) {
        var newName = variableManager.UpdateVariableName(variableName, text);
        variableName = newName;
        inputVarName.text = newName;
    }
}