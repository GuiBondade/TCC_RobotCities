using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReferenceHolder : MonoBehaviour
{
    [HideInInspector] public GameObject rootParameter;

    public BlocksFactory blocksFactory;

    public VariableManager variableManager;

    public ExecutionManager executionManager;

    public GameObject parentesesInstance;

    public GameObject unarySpacer;

    public GameObject childParamLayout;

    public GameObject inputTextPrefab;

    public GameObject inputTextInstance;

    public TMP_Text placeholderLabel;

    public GameObject sectionContent;

    public GameObject sectionPrefab;

    [HideInInspector] public ParameterType type;

    public ReferenceHolder leftOperand;

    public ReferenceHolder rightOperand;

    public GameObject selectionArea;

    public GameObject valueContent;

    public GameObject valuePrefab;

    public ValueInfo selectedValueVisual;

    public ParameterSections selectedValueSection;
    
    public string selectedValueKey; 

    public string selectedVariableName;

    [HideInInspector] public string selectedSignalName;

    [HideInInspector] public string selectedFunctionName;

    // Lista dinâmica de valores: quando o provider é setado, o ParameterConfig usa
    // a lista devolvida por ele no lugar da lista estática da seção.
    // O provider recebe a seção e devolve os itens (label + key) daquele tipo.
    public System.Func<ParameterSections, List<ParamValue>> dynamicValueProvider;

    public SectionInfo selectedSection;

    void Awake() {
        blocksFactory = BlocksFactory.instance;
        variableManager = VariableManager.instancia;
        executionManager = ExecutionManager.instancia;
    }

    public void HandleVariableRenamed(string oldName, string newName) {
        if (selectedVariableName == oldName) {
            selectedVariableName = newName;
            placeholderLabel.text = newName; 
        }
    }

    public void HandleVariableRetyped(string name, ParameterType variableType) {
        if (selectedVariableName == name) {
            Debug.Log(variableType + " == " + name);
            if (variableType != type) {
                placeholderLabel.text = blocksFactory.blockRegistry.GetParamData(type).defaultParamName;
                placeholderLabel.ForceMeshUpdate();
                GetComponent<AdjustWidthByText>().AdjustWidth();
                selectedValueKey = null;
                selectedVariableName = null;
            }
        }
    }

    public void HandleSignalRenamed(string oldName, string newName) {
        if (selectedSignalName == oldName) {
            selectedSignalName = newName;
        }
        // o signal selecionado num parâmetro de signal fica no selectedValueKey (é um value button)
        if (selectedValueKey == oldName) {
            selectedValueKey = newName;
            placeholderLabel.text = newName;
            placeholderLabel.ForceMeshUpdate();
        }
    }

    // Função renomeada em outro bloco: mantém a seleção e o texto do parâmetro coerentes.
    public void HandleFunctionRenamed(string oldName, string newName) {
        bool selectedFunction = selectedFunctionName == oldName || selectedValueKey == oldName;
        if (selectedFunction) {
            selectedFunctionName = newName;
            selectedValueKey = newName;
            if (placeholderLabel != null) {
                placeholderLabel.text = newName;
                placeholderLabel.ForceMeshUpdate();
                GetComponent<AdjustWidthByText>()?.AdjustWidth();
            }
            if (selectedValueVisual != null && selectedValueVisual.label != null) {
                selectedValueVisual.label.text = newName;
            }
        }
    }
}
