using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Entrada de parâmetro do declare (nome + tipo) usada no save.
[System.Serializable]
public struct FunctionParamEntry {
    public string name;
    public ParameterType type;
}

// Bloco de declaração de função (início de linha, com body normal).
// Interface dinâmica: [Função] [Nome] + linhas de Parâmetros e Variáveis Internas ([Nome] [Tipo] ([Default]) [x]) com botões [+].
// Mantém a assinatura viva no ExecutionManager enquanto edita; o corpo da função é o slot body padrão.
public class FunctionDeclareUI : BlockUIBody
{
    [SerializeField] private TMP_InputField functionNameInput;
    [SerializeField] private RectTransform parametersContent;
    [SerializeField] private RectTransform localsContent;
    [SerializeField] private Button addParamButton;
    [SerializeField] private Button addLocalButton;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private SectionDatabase sectionDatabase;
    // Toggle "Exportar": função pode ser importada por outro script.
    [SerializeField] private ExportToggleUI exportToggle;

    [SerializeField] private string defaultFunctionName = "nova função";
    [SerializeField] private string defaultParamName = "novo parâmetro";
    [SerializeField] private string defaultLocalName = "nova variável";

    private readonly List<FunctionRowUI> parameterRows = new List<FunctionRowUI>();
    private readonly List<FunctionRowUI> localRows = new List<FunctionRowUI>();

    [HideInInspector] public string functionName;

    // Função exportada: pode ser importada por outro script.
    public bool IsExported => exportToggle != null && exportToggle.IsExported;

    public override void SetupUI(BlockData newData) {
        base.SetupUI(newData);

        // Bloco vem do pool: garante que nenhuma row do uso anterior sobre.
        ClearRows();

        functionName = ExecutionManager.instancia.RegisterFunction(
            StringUtils.EnsureUniqueString(defaultFunctionName, ExecutionManager.instancia.uiFunctions, defaultFunctionName));

        if (functionNameInput != null) functionNameInput.SetTextWithoutNotify(functionName);
        if (addParamButton != null) {
            addParamButton.onClick.RemoveAllListeners();
            addParamButton.onClick.AddListener(AddParameterRow);
        }
        if (addLocalButton != null) {
            addLocalButton.onClick.RemoveAllListeners();
            addLocalButton.onClick.AddListener(AddLocalRow);
        }
        if (exportToggle != null) exportToggle.Setup(false);

        PushSignature();
    }

    // --- Setup/load ---
    public void SetupFunction(string functionNameSaved, List<ParameterNode> parameterNodes, List<CreateVarNode> locals, bool exported = false) {
        if (!string.IsNullOrEmpty(functionNameSaved)) {
            functionName = ExecutionManager.instancia.RegisterFunction(functionNameSaved);
            if (functionNameInput != null) functionNameInput.SetTextWithoutNotify(functionName);
        }

        if (exportToggle != null) exportToggle.SetExported(exported);

        if (parameterNodes != null) {
            foreach (var param in parameterNodes) {
                var row = CreateRow(parametersContent, parameterRows, defaultParamName);
                if (row == null) continue;
                row.SetupParameter(param.varName, param.type, RemoveRow, OnRowChanged);
            }
        }

        if (locals != null) {
            foreach (var local in locals) {
                var row = CreateRow(localsContent, localRows, defaultLocalName);
                if (row == null) continue;
                row.SetupLocal(local.varName, local.type, local.defaultValue, RemoveRow, OnRowChanged);
            }
        }

        PushSignature();
    }

    // --- Nome da função ---
    public void OnFunctionNameEndEdit(string text) {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) {
            text = defaultFunctionName;
            if (functionNameInput != null) functionNameInput.SetTextWithoutNotify(text);
        }

        functionName = ExecutionManager.instancia.RenameFunction(functionName, text);
        if (functionNameInput != null) functionNameInput.SetTextWithoutNotify(functionName);
        PushSignature();
    }

    // --- Rows ---
    public void AddParameterRow() {
        PruneDeadRows(parameterRows);
        var row = CreateRow(parametersContent, parameterRows, defaultParamName);
        if (row == null) {
            Debug.LogWarning("[FunctionDeclareUI] Não foi possível criar a row de parâmetro (parametersContent/rowPrefab nulos).");
            return;
        }
        row.SetupParameter(NextUniqueRowName(defaultParamName), ParameterType.IntParameter, RemoveRow, OnRowChanged);
        OnRowChanged();
        RebuildLayout();
    }

    public void AddLocalRow() {
        PruneDeadRows(localRows);
        var row = CreateRow(localsContent, localRows, defaultLocalName);
        if (row == null) {
            Debug.LogWarning("[FunctionDeclareUI] Não foi possível criar a row de variável (localsContent/rowPrefab nulos).");
            return;
        }
        row.SetupLocal(NextUniqueRowName(defaultLocalName), ParameterType.IntParameter, null, RemoveRow, OnRowChanged);
        OnRowChanged();
        RebuildLayout();
    }

    // Remove referências mortas (DestroyImmediate/fake-null) antes de operar nas listas.
    private static void PruneDeadRows(List<FunctionRowUI> rows) {
        for (int i = rows.Count - 1; i >= 0; i--) {
            if (rows[i] == null) rows.RemoveAt(i);
        }
    }

    private FunctionRowUI CreateRow(RectTransform content, List<FunctionRowUI> list, string defaultName) {
        if (content == null || rowPrefab == null) return null;
        var go = Instantiate(rowPrefab, content);
        var row = go.GetComponent<FunctionRowUI>();
        list.Add(row);
        return row;
    }

    // Nome default único dentro da própria função (parâmetros + locais, como no validador do compile).
    private string NextUniqueRowName(string baseName) {
        var existing = new List<string>();
        foreach (var row in parameterRows) if (row != null) existing.Add(row.Name);
        foreach (var row in localRows) if (row != null) existing.Add(row.Name);
        return StringUtils.EnsureUniqueString(baseName, existing, baseName);
    }

    private void RemoveRow(FunctionRowUI row) {
        if (row == null) return;
        parameterRows.Remove(row);
        localRows.Remove(row);
        row.CleanUp();
        row.gameObject.SetActive(false);
        DestroyImmediate(row.gameObject); // Remove já: Destroy adiado deixava rows fake-null no container
        PruneDeadRows(parameterRows);
        PruneDeadRows(localRows);
        OnRowChanged();
        RebuildLayout();
    }

    private void OnRowChanged() {
        PushSignature();
        RebuildLayout();
    }

    // Limpa todas as rows (parâmetros + variáveis locais). Usado ao apagar o bloco e ao reutilizar do pool.
    public void ClearRows() {
        ClearRowList(parameterRows, parametersContent);
        ClearRowList(localRows, localsContent);

        // Bloco no pool não é destruído (OnDestroy não roda): remove a função antiga explicitamente.
        if (!string.IsNullOrEmpty(functionName) && ExecutionManager.instancia != null) {
            ExecutionManager.instancia.RemoveFunction(functionName);
            functionName = null;
        }

        RebuildLayout();
    }

    private void ClearRowList(List<FunctionRowUI> rows, RectTransform content) {
        foreach (var row in rows) {
            if (row == null) continue;
            row.CleanUp();
            row.gameObject.SetActive(false);
            DestroyImmediate(row.gameObject); // Remove já: evita fake-null no container
        }
        rows.Clear();

        // Defesa: destrói qualquer row órfã que ainda esteja no container.
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) {
            var child = content.GetChild(i);
            if (child == null) continue; // transform já destruída (fake-null) não pode receber TryGetComponent
            if (child.TryGetComponent<FunctionRowUI>(out var stray)) {
                stray.CleanUp();
                stray.gameObject.SetActive(false);
                DestroyImmediate(stray.gameObject);
            }
        }
    }

    private void RebuildLayout() {
        if (parametersContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parametersContent);
        if (localsContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(localsContent);
        if (blockRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockRect);
    }

    // --- Assinatura viva ---
    public List<string> GetParameterNames() {
        var names = new List<string>();
        foreach (var row in parameterRows) if (row != null) names.Add(row.Name);
        return names;
    }

    public List<ParameterType> GetParameterTypes() {
        var types = new List<ParameterType>();
        foreach (var row in parameterRows) if (row != null) types.Add(row.Type);
        return types;
    }

    public List<CreateVarNode> GetLocalVarNodes() {
        var vars = new List<CreateVarNode>();
        foreach (var row in localRows) {
            if (row == null) continue;
            vars.Add(new CreateVarNode {
                varName = row.Name,
                type = row.Type,
                section = row.Section,
                defaultValue = row.DefaultValue
            });
        }
        return vars;
    }

    // Parâmetros (nome + tipo) para o save do declare.
    public List<FunctionParamEntry> GetParameterEntries() {
        var entries = new List<FunctionParamEntry>();
        foreach (var row in parameterRows) {
            if (row == null) continue;
            entries.Add(new FunctionParamEntry { name = row.Name, type = row.Type });
        }
        return entries;
    }

    private void PushSignature() {
        ExecutionManager.instancia.UpdateFunctionSignature(functionName, GetParameterNames(), GetParameterTypes());
    }

    private void OnDestroy() {
        if (!string.IsNullOrEmpty(functionName) && ExecutionManager.instancia != null) {
            ExecutionManager.instancia.RemoveFunction(functionName);
        }
    }
}
