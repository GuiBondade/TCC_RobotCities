using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


using System;
using System.Linq;

// chama o WorkspaceJSON (LOAD)
public class WorkspaceBuilder : MonoBehaviour
{
    private Dictionary<BlockUI, int> blockIdMap = new();
    private Dictionary<ReferenceHolder, int> paramIdMap = new();
    private SaveNodesData nodesData = new();
    private int blockIdCounter = 0;
    private int paramIdCounter = 0;
    private int blockRootId = 0;

    public RectTransform workspace;

    public SectionDatabase sectionDatabase;
    private string inputIntValueName;

    void Start() {
        workspace = CanvasAreasManager.instancia.contentWorkspace;

        inputIntValueName = sectionDatabase.GetValuesForSection(ParameterSections.IntValues)[0];
    }

    public SaveNodesData WorkspaceBuildGraph() {
        paramIdMap.Clear();
        blockIdMap.Clear();
        nodesData.blocosData.Clear();
        nodesData.parametrosData.Clear();
        nodesData.variaveisData.Clear();
        nodesData.Clear();
        paramIdCounter = 0;
        blockIdCounter = 0;

        // pega todos blocos no workspace (nível raiz)
        foreach (Transform child in workspace) {
            var block = child.GetComponent<BlockUI>();
            if (block != null) {
                Traverse(block, true);
            } else if (child.TryGetComponent<CreateVariableUI>(out CreateVariableUI createVarUI)) {
                // e se na verdade pegar da lista de variables a partir do nome?
                string varName = createVarUI.variableName; 
                var varInfo = VariableManager.instancia.variables[varName];
                Debug.Log("varInfo: " + varInfo);
                var createVarRect = createVarUI.GetComponent<RectTransform>();
                nodesData.variaveisData.Add( new CreateVarNode {
                    varName = varName,
                    type = varInfo.type,
                    section = varInfo.section,
                    defaultValue = varInfo.defaultValue,
                    position = createVarRect.anchoredPosition
                });
            }
        }
        
        foreach (var parametro in nodesData.parametrosData) {
            Debug.Log(parametro.id);
        }
        
        return nodesData;
    }

    private bool TryGetParameterNode(ReferenceHolder parameter, out int id) {
        if (paramIdMap.ContainsKey(parameter)) {
            id = paramIdMap[parameter];
            return true;
        }

        int myId = paramIdCounter++;
        paramIdMap[parameter] = myId;

        string variableName = null;
        string valor = parameter.selectedValueKey;
        if (!string.IsNullOrEmpty(parameter.selectedVariableName)) {
            Debug.Log("selectedVariableName: " + parameter.selectedVariableName);
            variableName = parameter.selectedVariableName;
        } else {
            Debug.Log("valor do parametro sendo salvo: " + valor);
            if (valor == null) {
                Debug.Log("Erro no Salvamento - Parametro não settado no parametro: " + parameter);
                valor = parameter.placeholderLabel.text;
                // ???(so dar log de erro, e deixar inexecutavel, dai na hora de salvar de novo ele pula as que ja tiver salvo exceto pelas marcadas como erro)
            } else if (valor == inputIntValueName) {
                valor = parameter.inputTextInstance.GetComponent<TMP_InputField>().text;
                // talvez na verdade deixar valor como "Digitar Valor", mas salvar de outra forma o int do input, sei la
            }
        }
        
        ParameterType typeParameter = parameter.type;

        var paramNode = new ParameterNode {
            id = myId,
            type = typeParameter,
            value = valor,
            varName = variableName,
            leftOperandId = -1,
            rightOperandId = -1
        };

        if (parameter.leftOperand != null && TryGetParameterNode(parameter.leftOperand, out int leftId)) {
            paramNode.leftOperandId = leftId;
        }
        if (parameter.rightOperand != null && TryGetParameterNode(parameter.rightOperand, out int rightId)) {
            paramNode.rightOperandId = rightId;
        }

        nodesData.parametrosData.Add(paramNode);
        id = myId;
        return true;
    }

    private int Traverse(BlockUI block, bool isRoot) {
        if (blockIdMap.ContainsKey(block)) return blockIdMap[block];

        int myId = blockIdCounter++;
        blockIdMap[block] = myId;

        if (isRoot) blockRootId = myId;

        Debug.Log(myId + " " + block.data.id + " " + blockRootId);

        var bloco = new BlockNode {
            id = myId,
            functionId = block.data.id,
            variableNameReference = (block is VariableBlockUI varBlock) ? varBlock.variableName : null,
            paramIds = new List<int> (),
            rootId = blockRootId,
            next = -1,
            body = -1,
            elseBlockId = -1
        };

        // adiciona so os param inicial no paramIds
        foreach (var param in block.parameterInitialList) { // reference holders dos blockUIs???
            if (TryGetParameterNode(param, out int id)) {
                bloco.paramIds.Add(id);
            }
        }

        // Adicionar elseBlockId ?????????????????
        // seguir a conexão dele..................

        // Seguir conexões
        var next = block.GetNext();
        if (next != null) bloco.next = Traverse(next, false);

        var body = block.GetBody();
        if (body != null) bloco.body = Traverse(body, false);

        nodesData.blocosData.Add(bloco);
        return myId;
    }
}
