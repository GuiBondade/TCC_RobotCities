using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class WorkspaceLoadScript : MonoBehaviour
{
    private BlockNode[] blocoDadosArray;
    private ParameterNode[] paramDadosArray;

    private int spaceBetweenBlocks = 0;

    [SerializeField] private BlockDatas blockDatas;
    [SerializeField] private BlocksFactory blocksFactory;
    [SerializeField] private RectTransform contentWorkspace;
    [SerializeField] private GameObject visualWorkspace;
    [SerializeField] private ScriptManager scriptManager;

    void Start() {
        if (scriptManager == null) {
            scriptManager = FindObjectOfType<ScriptManager>();
        }
        if (scriptManager == null) {
            Debug.LogError("[WorkspaceLoad] ScriptManager não configurado e não encontrado na cena.", this);
            return;
        }
        scriptManager.OnScriptOpen += BuildBlocksFromData;
    }

    private void OnDestroy() {
        if (scriptManager != null) {
            scriptManager.OnScriptOpen -= BuildBlocksFromData;
        }
    }

    public void BuildBlocksFromData(SaveNodesData data) {
        if (scriptManager == null) {
            Debug.LogError("[WorkspaceLoad] Não foi possível carregar o script: ScriptManager indisponível.", this);
            return;
        }
        if (blocksFactory == null || contentWorkspace == null) {
            Debug.LogError("[WorkspaceLoad] BlocksFactory ou contentWorkspace não configurado.", this);
            return;
        }

        blocksFactory.CleanUpWorkspace(contentWorkspace);
        spaceBetweenBlocks = 0;
        if (VariableManager.instancia != null) {
            VariableManager.instancia.Clear();
        } else {
            Debug.LogWarning("[WorkspaceLoad] VariableManager não está disponível ao carregar.", this);
        }
        if (ExecutionManager.instancia != null) {
            ExecutionManager.instancia.ClearUiSignals(); // signals do script voltam a ser registrados no RestoreState dos parâmetros
            ExecutionManager.instancia.ClearUiFunctions(); // funções idem (o declare re-registra no SetupFunction)
        } else {
            Debug.LogError("[WorkspaceLoad] ExecutionManager não está disponível ao carregar.", this);
        }
        
        if (data == null) {
            if (visualWorkspace != null) visualWorkspace.SetActive(false);
            return;
        }
        
        if (visualWorkspace != null) visualWorkspace.SetActive(true);
        
        if (data.variaveisData != null) {
            foreach (var varNode in data.variaveisData) {
                if (varNode == null) continue;
                CreateVariableUI visualVar = blocksFactory.InitializeCreateVariable(contentWorkspace, false);
                visualVar.SetupWithData(varNode.varName, varNode.type, varNode.section, varNode.defaultValue, varNode.exported);
                visualVar.GetComponent<RectTransform>().localPosition = varNode.position;
            }
        }

        if (data.blocosData == null || data.blocosData.Count == 0) return;

        var validBlocks = data.blocosData
            .Where(b => b != null && b.id >= 0)
            .ToList();
        if (validBlocks.Count == 0) return;

        int maxBlockId = validBlocks.Max(b => b.id);
        int maxParamId = data.parametrosData != null && data.parametrosData.Count > 0
            ? data.parametrosData.Where(p => p != null && p.id >= 0).Select(p => p.id).DefaultIfEmpty(-1).Max()
            : -1;

        blocoDadosArray = new BlockNode[maxBlockId + 1];
        paramDadosArray = new ParameterNode[maxParamId + 1];

        foreach (var b in validBlocks) {
            if (b == null || b.id < 0 || b.id >= blocoDadosArray.Length) continue;
            blocoDadosArray[b.id] = b;
        }
        if (data.parametrosData != null) {
            foreach (var p in data.parametrosData) {
                if (p == null || p.id < 0 || p.id >= paramDadosArray.Length) continue;
                paramDadosArray[p.id] = p;
            }
        }

        RegisterDeclareSignatures();
        RegisterImportedFunctions();
        
        BuildRoot(validBlocks);
        CompletePendingCalls();
    }

    // Registra a assinatura de todos os declares ANTES de instanciar (o call pode vir antes no arquivo).
    private void RegisterDeclareSignatures() {
        foreach (var bloco in blocoDadosArray) {
            if (bloco == null || bloco.functionId != "DeclareFunction") continue;
            if (bloco.paramIds == null || bloco.paramIds.Count == 0) {
                Debug.LogWarning($"[WorkspaceLoad] DeclareFunction {bloco.id} não possui nome.", this);
                continue;
            }

            string functionName = GetRawParamValue(bloco.paramIds[0]);
            if (string.IsNullOrEmpty(functionName)) {
                Debug.LogWarning($"[WorkspaceLoad] DeclareFunction {bloco.id} possui nome vazio.", this);
                continue;
            }
            var names = new List<string>();
            var types = new List<ParameterType>();

            for (int i = 1; i < bloco.paramIds.Count; i++) {
                if (bloco.paramIds[i] < 0 || bloco.paramIds[i] >= paramDadosArray.Length) continue;
                var paramNode = paramDadosArray[bloco.paramIds[i]];
                if (paramNode == null) continue;
                names.Add(paramNode.varName);
                types.Add(paramNode.type);
            }

            ExecutionManager.instancia.UpdateFunctionSignature(functionName, names, types);
        }
    }

    private string GetRawParamValue(int paramId) {
        if (paramId < 0 || paramId >= paramDadosArray.Length || paramDadosArray[paramId] == null) return null;
        return paramDadosArray[paramId].value;
    }

    // Registra as funções importadas de outros scripts (com o nome efetivo já resolvido)
    // e os signals importados (só autocomplete: não viram signal do script atual).
    private void RegisterImportedFunctions() {
        var script = scriptManager.scriptOnFocus;
        if (script == null) return;

        var plans = scriptManager.GetImportPlans(script);
        if (plans == null) return;

        foreach (var plan in plans) {
            if (plan == null) continue;

            if (plan.functions != null) {
                foreach (var function in plan.functions) {
                    if (function?.info == null) continue;
                    ExecutionManager.instancia.UpdateFunctionSignature(
                        function.effectiveName,
                        function.info.paramNames,
                        function.info.paramTypes);
                }
            }

            if (plan.signals != null) {
                foreach (var signal in plan.signals) {
                    ExecutionManager.instancia.RegisterImportedSignal(signal);
                }
            }
        }
    }

    // vai ser algo geral a instanciação de blocos e seus parametros, ate porque vai ser feito com pool
    public void BuildRoot(List<BlockNode> blocosData) { // DA PRA FAZER SER ACESSIVEL PELO PALLETE ITEM E DAI LA SER SO CHAMADA DE FUNÇÃO, AQUI TAMBÉM SERIA
        var rootInstancedList = new HashSet<int>();
        if (blocosData == null) return;
        
        contentWorkspace.gameObject.SetActive(false);
        foreach (var bloco in blocosData) {
            if (bloco == null) continue;
            int rootBlockId = bloco.rootId; // pegar id bloco instanciado na workspace
            if (rootBlockId >= 0 && rootBlockId < blocoDadosArray.Length
                && blocoDadosArray[rootBlockId] != null) {
                if (!rootInstancedList.Contains(rootBlockId)) {// ver se esse id é de bloco ja instanciado
                    rootInstancedList.Add(rootBlockId); // se nao, instancia e adiciona na lista
                    spaceBetweenBlocks += 100;
                    InstantiateBlock(rootBlockId, true, contentWorkspace); // workspace é ref
                }
            } else {
                Debug.LogWarning($"[WorkspaceLoad] Bloco {bloco.id} possui rootId inválido ({rootBlockId}).", this);
            }
        }
        contentWorkspace.gameObject.SetActive(true);
        StartCoroutine(RebuildDepois());
    }

    private void InstantiateBlock(int blockId, bool isRoot, RectTransform parent) {
        if (blockId < 0 || blockId >= blocoDadosArray.Length || blocoDadosArray[blockId] == null) {
            Debug.LogError($"[WorkspaceLoad] Bloco {blockId} referenciado pelo script não existe.", this);
            return;
        }
        var blockNode = blocoDadosArray[blockId];
        if (blockNode.paramIds == null) blockNode.paramIds = new List<int>();
        var blockData = blockDatas.GetData(blockNode.functionId);
        if (blockData == null) {
            Debug.LogError($"[WorkspaceLoad] Não existe BlockData para '{blockNode.functionId}'.", this);
            return;
        }
        
        var blockUI = blocksFactory.InitializeBlockLine(blockData, isRoot, parent);
        if (blockUI == null) {
            Debug.LogError($"[WorkspaceLoad] BlocksFactory não criou o bloco '{blockNode.functionId}'.", this);
            return;
        }
        // vai ser guardado no BlockNode uma referencia pra position, que vai ser salva na leitura
        // isso só no root, o resto vai ser nulo, e portanto todos vão estar no mesmo lugar que estavam antes
        blockUI.parameterInitialList.Clear();
        if (isRoot) {
            blockUI.GetComponent<RectTransform>().anchoredPosition = new Vector2(spaceBetweenBlocks, 0f);
        }

        VariableBlockUI varBlockUI = blockUI is VariableBlockUI casted ? casted : null;
        if (varBlockUI != null && !string.IsNullOrEmpty(blockNode.variableNameReference)) 
        {
            varBlockUI.isLoadingState = true; 
            varBlockUI.varNameDropdown.AtualizarLista(blockNode.variableNameReference);
            
            varBlockUI.isLoadingState = false; 
        }

        if (blockUI is FunctionDeclareUI declareUI) {
            InstantiateDeclare(blockNode, declareUI);
        } else if (blockUI is CallFunctionUI callUI) {
            InstantiateCall(blockNode, callUI);
        } else if (blockUI is ImportBlockUI importUI) {
            InstantiateImport(blockNode, importUI);
        } else {
            if (blockUI.TopSlot == null) {
                Debug.LogError($"[WorkspaceLoad] Bloco '{blockNode.functionId}' não possui TopSlot.", blockUI);
            } else {
                blockUI.ClearParameterLabels();
                for (int i = 0; i < blockNode.paramIds.Count; i++) {
                    int rootParamId = blockNode.paramIds[i];
                    // Rótulo do parâmetro (BlockData.rotulosParametros), antes do parâmetro no TopSlot.
                    blockUI.CreateParameterLabel(blockUI.GetParameterLabel(i));
                    var paramReference = InstantiateParameter(rootParamId, blockUI.TopSlot);
                    if (paramReference == null) continue;
                    blockUI.parameterInitialList.Add(paramReference);

                    if (varBlockUI != null) {
                        varBlockUI.valueParameter = paramReference;
                    }
                }
                Debug.Log($"[WorkspaceLoad] Bloco '{blockNode.functionId}' carregado. parâmetros={blockNode.paramIds.Count}, root={isRoot}, id={blockNode.id}", blockUI);
            }
        }

        if (blockNode.next >= 0) { // faz sentido usar essa operação?
            if (blockUI.slotNext == null) {
                Debug.LogError($"[WorkspaceLoad] Bloco '{blockNode.functionId}' não possui slotNext.", blockUI);
                return;
            }
            var slotNextRect = blockUI.slotNext.GetComponent<RectTransform>();
            InstantiateBlock(blockNode.next, false, slotNextRect);
        }
        if (blockNode.body >= 0) {
            var bodyOwner = blockUI as BlockUIBody;
            if (bodyOwner != null && bodyOwner.slotBody != null) {
                var slotBodyRect = bodyOwner.slotBody.GetComponent<RectTransform>();
                InstantiateBlock(blockNode.body, false, slotBodyRect);
            } else {
                Debug.LogWarning($"[Load] Bloco {blockNode.id} ({blockNode.functionId}) tem body mas a UI não possui slotBody — corpo ignorado.");
            }
        }
    }

    // Declare: paramIds[0] = nome, paramIds[1..] = parâmetros; locais vêm de functionLocals.
    private void InstantiateDeclare(BlockNode blockNode, FunctionDeclareUI declareUI) {
        string functionName = blockNode.paramIds.Count > 0 ? GetRawParamValue(blockNode.paramIds[0]) : null;

        var paramNodes = new List<ParameterNode>();
        for (int i = 1; i < blockNode.paramIds.Count; i++) {
            var paramNode = paramDadosArray[blockNode.paramIds[i]];
            if (paramNode != null) paramNodes.Add(paramNode);
        }

        var locals = new List<CreateVarNode>();
        if (scriptManager.scriptOnFocus != null && scriptManager.scriptOnFocus.nodes != null) {
            foreach (var entry in scriptManager.scriptOnFocus.nodes.functionLocals) {
                if (entry == null || entry.functionBlockId != blockNode.id || entry.vars == null) continue;
                locals.AddRange(entry.vars);
            }
        }

        declareUI.SetupFunction(functionName, paramNodes, locals, blockNode.exported);
    }

    // Call: reconstrói o parâmetro de função (popup) e as linhas de argumento.
    private void InstantiateCall(BlockNode blockNode, CallFunctionUI callUI) {
        if (blockNode.paramIds.Count > 0) {
            var functionParam = InstantiateParameter(blockNode.paramIds[0], callUI.TopSlot);
            callUI.parameterInitialList.Add(functionParam);
        }

        callUI.pendingArguments = new List<ParameterNode>();
        for (int i = 1; i < blockNode.paramIds.Count; i++) {
            var argNode = paramDadosArray[blockNode.paramIds[i]];
            if (argNode != null) callUI.pendingArguments.Add(argNode);
        }
        callUI.argumentsLoaded = false;
    }

    // Import: parâmetro do alvo (seção + id) + parâmetro do elemento (seção + chave), na ordem.
    // O alvo é resolvido pelo id salvo (instance id da entidade ou id do script).
    private void InstantiateImport(BlockNode blockNode, ImportBlockUI importUI) {
        string targetKey = "";
        var targetSection = ParameterSections.Robos;
        string elementKey = ImportKeys.All;
        var elementSection = ParameterSections.Variaveis;

        if (blockNode.paramIds.Count > 0) {
            var targetNode = paramDadosArray[blockNode.paramIds[0]];
            if (targetNode != null) {
                targetKey = targetNode.value ?? "";
                targetSection = targetNode.selectedSection;
            }
        }
        if (blockNode.paramIds.Count > 1) {
            var elementNode = paramDadosArray[blockNode.paramIds[1]];
            if (elementNode != null) {
                elementKey = string.IsNullOrEmpty(elementNode.value) ? ImportKeys.All : elementNode.value;
                elementSection = elementNode.selectedSection;
            }
        }

        if (blockNode.paramIds.Count > 0) {
            var targetRef = InstantiateParameter(blockNode.paramIds[0], importUI.TopSlot);
            importUI.parameterInitialList.Add(targetRef);
        }
        if (blockNode.paramIds.Count > 1) {
            var elementRef = InstantiateParameter(blockNode.paramIds[1], importUI.TopSlot);
            importUI.parameterInitialList.Add(elementRef);
        }

        importUI.SetupImport(targetKey, targetSection, elementKey, elementSection);
    }

    // Depois de tudo montado, resolve os calls cujos argumentos dependiam da assinatura do declare.
    private void CompletePendingCalls() {
        var allBlocks = GameObject.FindObjectsOfType<CallFunctionUI>();
        foreach (var callUI in allBlocks) {
            callUI.CompletePendingArguments(InstantiateParameter);
        }
    }

    public ReferenceHolder InstantiateParameter(int paramId, RectTransform blockParent) { // DA PRA FAZER SER ACESSIVEL PELO PALLETE ITEM E DAI LA SER SO CHAMADA DE FUNÇÃO
        if (paramDadosArray == null || paramId < 0 || paramId >= paramDadosArray.Length
            || paramDadosArray[paramId] == null) {
            Debug.LogError($"[WorkspaceLoad] Parâmetro {paramId} referenciado pelo script não existe.", this);
            return null;
        }
        if (blockParent == null) {
            Debug.LogError($"[WorkspaceLoad] Parâmetro {paramId} não possui slot visual pai.", this);
            return null;
        }
        var paramNode = paramDadosArray[paramId]; 

        string paramText = paramNode.value;
        if (string.IsNullOrEmpty(paramText)) {
            paramText = paramNode.varName;
        }

        var paramReference = blocksFactory.InitializeParameter(paramText, paramNode.type, blockParent);
        var paramRect = paramReference.GetComponent<RectTransform>();
        
        //tem que settar data do param aqui? (dava pra ter tipo um blockData, pra cada, sei la) precisa??? preguiça
        // tirar os operand do refholder, e criar um ParamData meio que pra isso 
        // !!!(nao sei se precisa mesmo, ta de boa por enquanto)
        
        if (paramNode.leftOperandId >= 0) {
            var leftOperand = InstantiateParameter(paramNode.leftOperandId, paramRect);//arrumar algo?
            paramReference.leftOperand = leftOperand;
            leftOperand.transform.SetAsFirstSibling();
        }
        if (paramNode.rightOperandId >= 0) {
            var rightOperand = InstantiateParameter(paramNode.rightOperandId, paramRect);//arrumar algo?
            paramReference.rightOperand = rightOperand;
            rightOperand.transform.SetAsLastSibling();
        }

        var parameterConfig = paramReference.GetComponent<ParameterConfig>();
        if (parameterConfig != null) {
            parameterConfig.RestoreState(paramNode.value, paramNode.varName, paramNode.selectedSection);
            var widthAdjuster = paramReference.GetComponent<AdjustWidthByText>();
            if (widthAdjuster != null) widthAdjuster.AdjustWidth();
        } else {
            Debug.LogError($"[WorkspaceLoad] Parâmetro {paramId} não possui ParameterConfig.", paramReference);
        }

        return paramReference;
    }

    IEnumerator RebuildDepois()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentWorkspace);
    }
}
