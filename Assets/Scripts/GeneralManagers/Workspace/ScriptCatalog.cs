using System.Collections.Generic;
using System.Linq;

// ---------------------------------------------------------------------------
// Catálogo de exportações de um script.
// Tudo é derivado do SaveNodesData do script (nada é salvo à parte):
//   - funções declaradas (exportadas ou não) + suas dependências (calls)
//   - variáveis globais (exportadas ou não)
//   - signals usados (blocos de emissão/escuta)
// ---------------------------------------------------------------------------

// Uma função declarada no script.
public class ScriptFunctionInfo
{
    public string name;
    public bool exported;
    public int declareBlockId;          // id do BlockNode do DeclareFunction
    public List<string> paramNames = new List<string>();
    public List<ParameterType> paramTypes = new List<ParameterType>();
    public List<string> calledFunctions = new List<string>(); // funções chamadas no corpo

    public ScriptFunctionInfo Clone() {
        var copy = new ScriptFunctionInfo {
            name = name,
            exported = exported,
            declareBlockId = declareBlockId,
            paramNames = new List<string>(paramNames),
            paramTypes = new List<ParameterType>(paramTypes),
            calledFunctions = new List<string>(calledFunctions)
        };
        return copy;
    }
}

// Uma variável global do script.
public class ScriptVariableInfo
{
    public string name;
    public ParameterType type;
    public ParameterSections section;
    public string defaultValue;
    public bool exported;
}

// O que um script oferece para ser importado por outro.
public class ScriptExportCatalog
{
    public ScriptData script;
    public List<ScriptFunctionInfo> functions = new List<ScriptFunctionInfo>();
    public List<ScriptVariableInfo> variables = new List<ScriptVariableInfo>();
    public List<string> signals = new List<string>();

    public ScriptFunctionInfo FindFunction(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var f in functions) {
            if (f != null && f.name == name) return f;
        }
        return null;
    }

    public List<ScriptFunctionInfo> ExportedFunctions() {
        return functions.Where(f => f != null && f.exported).ToList();
    }

    public List<ScriptVariableInfo> ExportedVariables() {
        return variables.Where(v => v != null && v.exported).ToList();
    }

    // Monta o catálogo a partir dos nós salvos do script.
    public static ScriptExportCatalog Build(SaveNodesData nodes) {
        var catalog = new ScriptExportCatalog();
        if (nodes == null) return catalog;

        // --- funções declaradas ---
        foreach (var block in nodes.blocosData) {
            if (block == null || block.functionId != "DeclareFunction") continue;

            var info = new ScriptFunctionInfo {
                exported = block.exported,
                declareBlockId = block.id
            };

            if (block.paramIds != null && block.paramIds.Count > 0) {
                var nameParam = FindParam(nodes, block.paramIds[0]);
                if (nameParam != null) info.name = nameParam.value;
            }

            for (int i = 1; i < (block.paramIds?.Count ?? 0); i++) {
                var param = FindParam(nodes, block.paramIds[i]);
                if (param == null) continue;
                info.paramNames.Add(param.varName);
                info.paramTypes.Add(param.type);
            }

            info.calledFunctions = CollectCalledFunctions(nodes, block.body);
            catalog.functions.Add(info);
        }

        // --- variáveis globais ---
        foreach (var var in nodes.variaveisData) {
            if (var == null) continue;
            catalog.variables.Add(new ScriptVariableInfo {
                name = var.varName,
                type = var.type,
                section = var.section,
                defaultValue = var.defaultValue,
                exported = var.exported
            });
        }

        // --- signals usados no script ---
        foreach (var block in nodes.blocosData) {
            if (block == null) continue;
            if (block.functionId != "EmitSignal" && block.functionId != "ListenSignal") continue;
            if (block.paramIds == null || block.paramIds.Count == 0) continue;

            var param = FindParam(nodes, block.paramIds[0]);
            if (param == null || string.IsNullOrEmpty(param.value)) continue;
            if (!catalog.signals.Contains(param.value)) catalog.signals.Add(param.value);
        }

        return catalog;
    }

    private static ParameterNode FindParam(SaveNodesData nodes, int paramId) {
        if (paramId < 0 || nodes?.parametrosData == null) return null;
        foreach (var p in nodes.parametrosData) {
            if (p != null && p.id == paramId) return p;
        }
        return null;
    }

    // Varre a subárvore do corpo (body + next + else) procurando blocos de chamada de função.
    private static List<string> CollectCalledFunctions(SaveNodesData nodes, int startBlockId) {
        var result = new List<string>();
        if (nodes == null || startBlockId < 0) return result;

        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(startBlockId);

        while (stack.Count > 0) {
            int id = stack.Pop();
            if (id < 0 || !visited.Add(id)) continue;

            var block = FindBlock(nodes, id);
            if (block == null) continue;

            if (block.functionId == "CallFunction" && block.paramIds != null && block.paramIds.Count > 0) {
                var param = FindParam(nodes, block.paramIds[0]);
                if (param != null && !string.IsNullOrEmpty(param.value) && !result.Contains(param.value)) {
                    result.Add(param.value);
                }
            }

            if (block.body >= 0) stack.Push(block.body);
            if (block.elseBlockId >= 0) stack.Push(block.elseBlockId);
            if (block.next >= 0) stack.Push(block.next);
        }

        return result;
    }

    private static BlockNode FindBlock(SaveNodesData nodes, int blockId) {
        if (blockId < 0 || nodes?.blocosData == null) return null;
        foreach (var b in nodes.blocosData) {
            if (b != null && b.id == blockId) return b;
        }
        return null;
    }
}
