using System.Collections.Generic;
using UnityEngine;
using TMPro;

// chama o WorkspaceJSON (LOAD)
public class WorkspaceSaveScript : MonoBehaviour
{
    private Dictionary<BlockUI, int> blockIdMap = new();
    private Dictionary<ReferenceHolder, int> paramIdMap = new();
    private SaveNodesData nodesData;
    private List<int> startBlocksId = new List<int>();
    private int blockIdCounter = 0;
    private int paramIdCounter = 0;
    private int blockRootId = 0;

    [SerializeField] private RectTransform contentWorkspace;
    [SerializeField] private ScriptManager scriptManager;
    [SerializeField] private SectionDatabase sectionDatabase;

    private string inputIntValueName;
    private string inputSignalValueName;

    void Start() {
        if (InterfaceWorkspace.instancia == null) {
            Debug.LogError("[WorkspaceSave] InterfaceWorkspace não está disponível.", this);
            return;
        }

        InterfaceWorkspace.instancia.OnScriptSaved += WorkspaceBuildGraph;

        var intValues = sectionDatabase.GetValuesForSection(ParameterSections.IntValues);
        inputIntValueName = intValues.Count > 0 ? intValues[0] : null;

        var signalValues = sectionDatabase.GetValuesForSection(ParameterSections.SignalValues);
        inputSignalValueName = signalValues.Count > 0 ? signalValues[0] : null; // "Digitar..." do signal
    }

    private void OnDestroy() {
        if (InterfaceWorkspace.instancia != null) {
            InterfaceWorkspace.instancia.OnScriptSaved -= WorkspaceBuildGraph;
        }
    }

    public void WorkspaceBuildGraph() {
        if (scriptManager == null || scriptManager.scriptOnFocus == null) {
            Debug.LogError("[WorkspaceSave] Não há um script aberto para salvar.", this);
            return;
        }

        paramIdMap.Clear();
        blockIdMap.Clear();
        startBlocksId.Clear(); // evita acumular os roots de um save pra outro
        nodesData = scriptManager.scriptOnFocus.nodes ?? new SaveNodesData();
        nodesData.Clear();
        paramIdCounter = 0;
        blockIdCounter = 0;

        // pega todos blocos no workspace (nível raiz)
        foreach (Transform child in contentWorkspace) {
            var block = child.GetComponent<BlockUI>();
            if (block != null) {
                startBlocksId.Add(
                    Traverse(block, true)
                );
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
                    position = createVarRect.anchoredPosition,
                    exported = createVarUI.IsExported
                });
            }
        }
        
        foreach (var parametro in nodesData.parametrosData) {
            Debug.Log(parametro.id);
        }
        
        scriptManager.SaveScript(nodesData, startBlocksId);
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
        } else if (parameter.type == ParameterType.StringParameter) {
            // texto livre: o valor é o conteúdo do input (vazio = parâmetro não setado;
            // o bloco de debug simplesmente não spawna balão)
            valor = parameter.inputTextInstance != null
                ? parameter.inputTextInstance.GetComponent<TMP_InputField>().text
                : null;
        } else {
            Debug.Log("valor do parametro sendo salvo: " + valor);
            if (valor == null) {
                Debug.Log("Erro no Salvamento - Parametro não settado no parametro: " + parameter);
                valor = parameter.placeholderLabel.text;
                // ???(so dar log de erro, e deixar inexecutavel, dai na hora de salvar de novo ele pula as que ja tiver salvo exceto pelas marcadas como erro)
            } else if (valor == inputIntValueName || (!string.IsNullOrEmpty(inputSignalValueName) && valor == inputSignalValueName)) {
                // sentinel "Digitar...": salva o texto digitado no input (int ou signal)
                if (parameter.inputTextInstance != null) {
                    valor = parameter.inputTextInstance.GetComponent<TMP_InputField>().text;
                }
                // talvez na verdade deixar valor como "Digitar Valor", mas salvar de outra forma o int do input, sei la
            }
        }
        
        ParameterType typeParameter = parameter.type;

        var paramNode = new ParameterNode {
            id = myId,
            type = typeParameter,
            value = valor,
            varName = variableName,
            selectedSection = parameter.selectedValueKey != null
                ? parameter.selectedValueSection
                : (parameter.selectedSection != null
                    ? parameter.selectedSection.sectionCurrent
                    : parameter.selectedValueSection),
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

        if (block is FunctionDeclareUI declareUI) {
            SaveDeclareBlock(declareUI, bloco);
        } else if (block is CallFunctionUI callUI) {
            SaveCallBlock(callUI, bloco);
        } else if (block is ImportBlockUI importUI) {
            SaveImportBlock(importUI, bloco);
        } else {
            // adiciona so os param inicial no paramIds
            foreach (var param in block.parameterInitialList) { // reference holders dos blockUIs???
                if (TryGetParameterNode(param, out int id)) {
                    bloco.paramIds.Add(id);
                }
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

    // Declare: paramIds[0] = nome da função (cru) e os demais = parâmetros (só nome + tipo);
    // as variáveis locais vão em functionLocals ligadas ao id do bloco do declare.
    private void SaveDeclareBlock(FunctionDeclareUI declareUI, BlockNode bloco) {
        bloco.exported = declareUI.IsExported;

        bloco.paramIds.Add(AddRawParamNode(declareUI.functionName));

        foreach (var param in declareUI.GetParameterEntries()) {
            bloco.paramIds.Add(AddNamedParamNode(param.name, param.type));
        }

        var locals = declareUI.GetLocalVarNodes();
        if (locals.Count > 0) {
            var entry = new FunctionLocalVars { functionBlockId = bloco.id };
            entry.vars = locals;
            nodesData.functionLocals.Add(entry);
        }
    }

    // Call: paramIds[0] = função selecionada (cru, via parameterInitialList[0]) e os demais = argumentos.
    private void SaveCallBlock(CallFunctionUI callUI, BlockNode bloco) {
        string functionName = callUI.CurrentSelection();
        bloco.paramIds.Add(AddRawParamNode(functionName));

        foreach (var arg in callUI.GetArgumentReferences()) {
            if (TryGetParameterNode(arg, out int id)) {
                bloco.paramIds.Add(id);
            }
        }
    }

    // Import: paramIds[0] = alvo (seção Robô/Construção/Script + id da instância/script),
    // paramIds[1] = o que importar (seção Variável/Função/Signal + chave do elemento).
    // O id é salvo (e não o nome) pra resolver a entidade sem ambiguidade de nomes repetidos.
    private void SaveImportBlock(ImportBlockUI importUI, BlockNode bloco) {
        bloco.paramIds.Add(AddRawParamNode(
            importUI.GetTargetKeyForSave(),
            importUI.GetTargetSectionForSave(),
            ParameterType.ImportTargetParameter));

        bloco.paramIds.Add(AddRawParamNode(
            importUI.GetElementKeyForSave(),
            importUI.GetElementSectionForSave(),
            ParameterType.ImportElementParameter));
    }

    private int AddRawParamNode(string value) {
        return AddRawParamNode(value, ParameterSections.FunctionValues, ParameterType.FunctionParameter);
    }

    private int AddRawParamNode(string value, ParameterSections section, ParameterType type) {
        int myId = paramIdCounter++;
        nodesData.parametrosData.Add(new ParameterNode {
            id = myId,
            type = type,
            value = value,
            varName = null,
            selectedSection = section,
            leftOperandId = -1,
            rightOperandId = -1
        });
        return myId;
    }

    private int AddNamedParamNode(string name, ParameterType type) {
        int myId = paramIdCounter++;
        nodesData.parametrosData.Add(new ParameterNode {
            id = myId,
            type = type,
            value = null,
            varName = name,
            selectedSection = ParameterSetup.SectionForType(type),
            leftOperandId = -1,
            rightOperandId = -1
        });
        return myId;
    }
}
