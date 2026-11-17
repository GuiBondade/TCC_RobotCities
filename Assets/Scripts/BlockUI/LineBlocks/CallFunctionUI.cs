using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Bloco de chamada de função. O parâmetro de função vem no TopSlot (ParameterType.FunctionParameter,
// popup com as funções do script). Abaixo dele, um content com uma linha por parâmetro da função
// selecionada, cada uma com o rótulo do parâmetro + um parâmetro type-locked pelo tipo declarado.
// O bloco é sequência normal (BlockUINext): o corpo executado é a cópia do declare, montada na compilação.
public class CallFunctionUI : BlockUINext
{
    [SerializeField] private RectTransform argumentsContent;
    [SerializeField] private GameObject argumentRowPrefab; // linha vazia: ParamLabel (nome) + a própria raiz como slot do parâmetro

    private BlocksFactory blocksFactory; // a base (BlockUI) não expõe o factory: pega do singleton

    private readonly List<ReferenceHolder> argumentRows = new List<ReferenceHolder>();
    private readonly List<GameObject> argumentRowInstances = new List<GameObject>();
    private readonly List<string> argumentNames = new List<string>();

    [HideInInspector] public string selectedFunctionName;

    // Os argumentos vêm logo depois do parâmetro de função no paramIds do save.
    [HideInInspector] public List<ParameterNode> pendingArguments;
    [HideInInspector] public bool argumentsLoaded;

    private void Awake() {
        blocksFactory = BlocksFactory.instance;
    }

    private void OnEnable() {
        if (ExecutionManager.instancia != null) {
            ExecutionManager.instancia.OnFunctionSignatureChanged += HandleSignatureChanged;
        }
    }

    private void OnDisable() {
        if (ExecutionManager.instancia != null) {
            ExecutionManager.instancia.OnFunctionSignatureChanged -= HandleSignatureChanged;
        }
    }

    private void HandleSignatureChanged(string functionName) {
        if (functionName != CurrentSelection()) return;
        RefreshArguments();
    }

    // Lê o nome selecionado direto do parâmetro do TopSlot (fonte viva do popup).
    public string CurrentSelection() {
        if (parameterInitialList != null && parameterInitialList.Count > 0 && parameterInitialList[0] != null) {
            return parameterInitialList[0].selectedFunctionName;
        }
        return selectedFunctionName;
    }

    private void Update() {
        // o popup escreve direto no ReferenceHolder; sincroniza a cada frame (barato) e refaz as linhas quando muda
        string current = CurrentSelection();
        if (current != selectedFunctionName) {
            selectedFunctionName = current;
            if (!argumentsLoaded) RefreshArguments();
        }
    }

    // Reconstrói as linhas de argumento a partir da assinatura viva da função selecionada.
    // Argumentos já preenchidos são preservados por índice quando o count não muda.
    public void RefreshArguments() {
        var signature = ExecutionManager.instancia.GetFunctionSignature(selectedFunctionName);
        var paramNames = signature != null ? signature.paramNames : null;

        int target = paramNames != null ? paramNames.Count : 0;

        // se mudou a função (nomes diferentes), descarta os valores antigos
        bool sameFunction = argumentNames.Count == target;
        for (int i = 0; sameFunction && paramNames != null && i < target; i++) {
            if (argumentNames[i] != paramNames[i]) sameFunction = false;
        }

        if (!sameFunction) {
            ClearArgumentRows();
            for (int i = 0; i < target; i++) {
                AddArgumentRow(paramNames[i], signature.paramTypes.Count > i ? signature.paramTypes[i] : ParameterType.IntParameter);
            }
            argumentNames.Clear();
            if (paramNames != null) argumentNames.AddRange(paramNames);
        }
        else {
            // mesma função: só ajusta o count
            while (argumentRows.Count > target) RemoveLastArgumentRow();
            while (argumentRows.Count < target) {
                int i = argumentRows.Count;
                AddArgumentRow(paramNames[i], signature.paramTypes.Count > i ? signature.paramTypes[i] : ParameterType.IntParameter);
            }
        }

        RebuildArgumentsLayout();
    }

    private void AddArgumentRow(string paramName, ParameterType type) {
        if (argumentsContent == null || argumentRowPrefab == null) return;
        if (blocksFactory == null) blocksFactory = BlocksFactory.instance;
        if (blocksFactory == null) {
            Debug.Log("CallFunctionUI: BlocksFactory indisponível, não conseguiu criar o parâmetro.");
            return;
        }

        // linha vazia: ParamLabel recebe o nome do argumento e o parâmetro vem do pool como filho da raiz
        var labelInstance = Instantiate(argumentRowPrefab, argumentsContent);

        var paramLabel = labelInstance.GetComponentInChildren<TMP_Text>();
        if (paramLabel != null) {
            paramLabel.text = paramName;
            paramLabel.ForceMeshUpdate();
        }

        var slot = labelInstance.transform as RectTransform;

        // null: o parâmetro usa o defaultParamName do ParamData (o nome do argumento fica no rótulo)
        var holder = blocksFactory.InitializeParameter(null, type, slot);
        if (holder == null) {
            Debug.Log($"CallFunctionUI: não conseguiu criar o parâmetro '{paramName}'.");
            return;
        }

        argumentRowInstances.Add(labelInstance.gameObject);
        argumentRows.Add(holder);
    }

    private void RemoveLastArgumentRow() {
        if (argumentRows.Count == 0) return;

        int last = argumentRows.Count - 1;
        var holder = argumentRows[last];
        var labelInstance = argumentRowInstances[last];
        argumentRows.RemoveAt(last);
        argumentRowInstances.RemoveAt(last);

        if (holder != null) blocksFactory.CleanUpParameter(holder);
        if (labelInstance != null) Destroy(labelInstance);
    }

    private void RebuildArgumentsLayout() {
        if (argumentsContent == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(argumentsContent);
        if (blockRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(blockRect);
    }

    // Chamado pelo loader depois de tudo montado: cria as linhas conforme a assinatura e
    // distribui os argumentos salvos (na ordem) entre elas.
    public delegate ReferenceHolder InstantiateParamDelegate(int paramId, RectTransform parent);

    public void CompletePendingArguments(InstantiateParamDelegate instantiate) {
        RefreshArguments();

        if (pendingArguments != null) {
            for (int i = 0; i < pendingArguments.Count; i++) {
                if (i >= argumentRows.Count) {
                    Debug.Log($"CallFunctionUI: argumento {i} da chamada de '{selectedFunctionName}' não tem linha correspondente.");
                    continue;
                }
                var holder = argumentRows[i];
                if (holder == null) continue;
                var config = holder.GetComponent<ParameterConfig>();
                if (config != null) config.RestoreState(pendingArguments[i].value, pendingArguments[i].varName, pendingArguments[i].selectedSection);
            }
        }

        pendingArguments = null;
        argumentsLoaded = true;
        RebuildArgumentsLayout();
    }

    // Limpa tudo (também usado no cleanup do pool).
    public void ClearArgumentRows() {
        while (argumentRows.Count > 0) RemoveLastArgumentRow();
        argumentNames.Clear();
    }

    // Save: argumentos na ordem das linhas (depois do parâmetro de função).
    public List<ReferenceHolder> GetArgumentReferences() {
        var refs = new List<ReferenceHolder>();
        foreach (var row in argumentRows) if (row != null) refs.Add(row);
        return refs;
    }
}
