using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

public class VariableBlockUI : BlockUINext
{
    public VariableNamesDropdown varNameDropdown;

    private VariableManager variableManager; 
    private BlocksFactory blocksFactory;

    public ReferenceHolder valueParameter = null;
    [HideInInspector] public bool isLoadingState = false;
    
    private int selectedVariableIndex = -1;
    [SerializeField] private string _variableName; // esse ou o de cima talvez irá sumir, ja que se referem ao mesmo
    public string variableName {
        get => _variableName;
        set => _variableName = value;
    }

    void Awake() {
        variableManager = VariableManager.instancia;
        blocksFactory = BlocksFactory.instance;
    }

    public override void SetupUI(BlockData newData) {
        base.SetupUI(newData);
        if (variableManager == null) variableManager = VariableManager.instancia;
        ResetVariable();
        variableManager.OnVariableRenamed += HandleVariableRenamed;
        variableManager.OnVariableRetyped += HandleVariableRetyped;
        varNameDropdown.AtualizarLista(variableManager.defaultVarFirstOption, 0); // vai ser "Selecionar Variável"
    }

    void OnDisable() { // reseta no blocks factory on clean up sepa
        variableManager.OnVariableRenamed -= HandleVariableRenamed;
        variableManager.OnVariableRetyped -= HandleVariableRetyped;
    }

    public void ResetVariable () {
        variableName = variableManager.defaultVarFirstOption;
        if (valueParameter != null) {
            blocksFactory.CleanUpParameter(valueParameter);
            valueParameter = null;
        }
        parameterInitialList.Clear();
        selectedVariableIndex = 0;
        varNameDropdown.value = 0;  
    }
    
    public void SelectVarName(int index) {
        if (index != selectedVariableIndex) {
            var varSelectedName = varNameDropdown.options[index].text; //pegar do variables?
            selectedVariableIndex = index;
            variableName = null; // por varSelectedName?
            if (valueParameter != null) {
                parameterInitialList.Remove(valueParameter);
                blocksFactory.CleanUpParameter(valueParameter);
                valueParameter = null;
            }
            if (index != 0) {
                var varSelectedType = variableManager.GetVarTypeByName(varSelectedName);
                variableName = varSelectedName;
                if (isLoadingState) return;

                Debug.Log(varSelectedName + " e " + varSelectedType);
                valueParameter = blocksFactory.InitializeParameter(null, varSelectedType, TopSlot);
                parameterInitialList.Add(valueParameter);
            }
        }
    }

    public void HandleVariableRenamed(string oldName, string newName) {
        if (variableName != oldName) return; 
        if (newName == null) {
            ResetVariable();
            return;
        }

        variableName = newName; 
        Debug.Log("renomeou: " + this.gameObject);

        varNameDropdown.AtualizarLista(newName, selectedVariableIndex);
    }

    public void HandleVariableRetyped(string name, ParameterType varType) {
        Debug.Log("tinha param: " + (valueParameter != null) + ", nome buscado: " + name + ", nome da variavel" + variableName + ", tipo novo: " + varType);
        if (name != variableName) return;

        if (valueParameter != null) {
            if (valueParameter.type == varType) return; // é pra ta certo, no caso não altera nada caso o tipo não ser alterado
            blocksFactory.CleanUpParameter(valueParameter);
        }
        valueParameter = blocksFactory.InitializeParameter(null, varType, TopSlot);

        Debug.Log("retipou: " + this.gameObject + " para: " + varType);
    }
}