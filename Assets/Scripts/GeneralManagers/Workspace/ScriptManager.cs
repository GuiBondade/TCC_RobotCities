using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

using ScriptRunner;

public struct VariableData{
    public string varName;
    public string defaultValue;
}

[System.Serializable]
public class EntitySignalImport
{
    public string entityName;
    public string entityIdString;
    public int entityInstanceId;
    public string signalName;
}

[System.Serializable]
public class EntityVariableImport
{
    public string entityName;
    public string entityIdString;
    public int entityInstanceId;
    public string varName;
    public string localName; // nome usado no script/contexto
    public bool isScriptVariable; // true = variável do script que a entidade executa (só leitura)
}

[System.Serializable]
public class ResolvedEntityImport
{
    public string section; // "Robo" | "Construcao" | "Script"
    public string entityName;
    public int entityInstanceId;
    public string entityIdString;
    public List<string> importedSignals = new List<string>();
    public List<string> importedVariables = new List<string>();
}

public class ScriptData // vai ser preenchido na inicialização e no save
{
    public int id;
    public string name;
    public SaveNodesData nodes; 
    public List<ExecutionBlock> functionsRoot; 
    public Dictionary<string, List<ExecutionBlock>> startBlocksBySignal; // preenchido na compilação: blocos de escuta por signal (chave sem valor = só emissão)
    public List<VariableData> vars;
    public Dictionary<string, FunctionDefinition> functions; // definições compiladas por função (parâmetros + locais)
    // int version; caso o id do script sendo compilado ja existir pega a version salva e seta no novo como version = oldVersion++

    // Nós mesclados: os blocos do script + as funções importadas de outros scripts.
    // É o que o compilador usa (o nodes original fica intacto pro save).
    public SaveNodesData mergedNodes;
    public bool compiled;

    // Signals importados de scripts (só autocomplete: alimentam o dropdown de signals).
    public List<string> importedSignals = new List<string>();
    // Planos de import resolvidos na última compilação.
    public List<ScriptImportPlan> importPlans = new List<ScriptImportPlan>();
    
    // Imports de entidades
    public List<EntitySignalImport> entitySignalImports = new List<EntitySignalImport>();
    public List<EntityVariableImport> entityVariableImports = new List<EntityVariableImport>();
    public List<ResolvedEntityImport> resolvedEntityImports = new List<ResolvedEntityImport>();
}

public class ScriptManager : MonoBehaviour // arquivo set/delete json
{
    public static ScriptManager instancia { get; private set; }

    [SerializeField] private ScriptSaveJSON jsonBuilder;

    public ScriptData scriptOnFocus;
    public List<ScriptData> scripts = new List<ScriptData>();

    public event Action<SaveNodesData> OnScriptOpen; 
    public event Action OnScriptClosed; 
    public event Action<string> ScriptInterfaceStateChanged; 

    // Catálogos por script (derivados dos nós): invalidado a cada compilação.
    private readonly Dictionary<int, ScriptExportCatalog> _catalogs = new Dictionary<int, ScriptExportCatalog>();
    // Scripts em compilação (guarda de ciclo entre imports).
    private readonly HashSet<int> _compiling = new HashSet<int>();
    
    void Awake() {
        if (instancia == null) instancia = this;
        else if (instancia != this) {
            Debug.Log("Duplicata desnecessária de ScriptManager destruída");
            Destroy(gameObject);
        }

        ScriptSignals.OnCreateScript += CreateScript;
    }

    private void OnDestroy()
    {
        ScriptSignals.OnCreateScript -= CreateScript;
    }

    public void SaveScript(SaveNodesData data, List<int> rootBlocksId) // save (set)
    {
        if (scriptOnFocus == null) return;

        Debug.Log("salvando script: " + scriptOnFocus.id);
        scriptOnFocus.nodes = data;
        jsonBuilder.SaveAsJson(scriptOnFocus.nodes, scriptOnFocus.id.ToString());
        _catalogs.Remove(scriptOnFocus.id);
        Compile(scriptOnFocus, rootBlocksId);
        RecompileDependents(scriptOnFocus);
    }

    public void Compile(ScriptData script, List<int> rootBlocksId) {
        if (script == null || script.nodes == null) return;

        // Dependências primeiro: o script importado precisa estar compilado
        // (e mesclado) antes de o importador copiar as funções dele.
        EnsureDependenciesCompiled(script);

        _catalogs.Remove(script.id);

        SaveNodesData merged = BuildMergedNodes(script);
        script.mergedNodes = merged;

        BlockCompiler compiler = new BlockCompiler();
        List<ExecutionBlock> roots = new List<ExecutionBlock>();
        var rootIds = (rootBlocksId != null && rootBlocksId.Count > 0) ? rootBlocksId : GetRootIds(script.nodes);
        var compiledRoots = new HashSet<int>();

        try {
            compiler.Prepare(merged); // cache de instâncias compartilhado entre todos os roots

            foreach (var rootId in rootIds) {
                if (!compiledRoots.Add(rootId)) continue; // ignora roots repetidos
                try {
                    roots.Add(compiler.Compile(rootId));
                } catch (Exception e) {
                    Debug.LogError($"[Compile] Falha ao compilar o bloco raiz {rootId} do script '{script.name}': {e.Message}");
                }
            }
        } catch (Exception e) {
            Debug.LogError($"[Compile] Falha ao preparar o script '{script.name}': {e.Message}");
            return;
        }

        List<VariableData> varList = new List<VariableData>();
        foreach(CreateVarNode var in merged.variaveisData) {
            varList.Add(new VariableData(){
                varName = var.varName,
                defaultValue = var.defaultValue
            }
            );
        } 

        script.functionsRoot = roots;
        script.vars = varList;
        script.startBlocksBySignal = compiler.BuildSignalStarts();
        script.functions = compiler.BuildFunctionDefinitions();
        script.compiled = true;
        
        // Copia imports de entidades coletados na compilação
        if (script.entitySignalImports == null) script.entitySignalImports = new List<EntitySignalImport>();
        if (script.entityVariableImports == null) script.entityVariableImports = new List<EntityVariableImport>();
        if (script.resolvedEntityImports == null) script.resolvedEntityImports = new List<ResolvedEntityImport>();
        script.entitySignalImports.Clear();
        script.entityVariableImports.Clear();
        script.resolvedEntityImports.Clear();
        if (compiler.entitySignalImports != null) script.entitySignalImports.AddRange(compiler.entitySignalImports);
        if (compiler.entityVariableImports != null) script.entityVariableImports.AddRange(compiler.entityVariableImports);
        if (compiler.resolvedEntityImports != null) script.resolvedEntityImports.AddRange(compiler.resolvedEntityImports);
        
        // Tenta marcar bools das entidades para imports detectados (antes da execução)
        TryApplyEntityImports(script);
    }

    // --- Imports entre scripts ------------------------------------------------

    // Garante que os scripts importados (e os scripts importados por eles) estejam
    // compilados antes do script atual ser mesclado.
    private void EnsureDependenciesCompiled(ScriptData script)
    {
        if (script == null || script.nodes == null) return;
        if (!_compiling.Add(script.id)) return; // já está em compilação (ciclo)

        try
        {
            foreach (var reference in EnumerateImportReferences(script))
            {
                if (reference.targetSection != ParameterSections.Scripts) continue;

                var target = FindScriptById(ParseId(reference.targetKey));
                if (target == null || target == script) continue;
                if (target.nodes == null) continue;
                if (target.compiled) continue;

                Compile(target, GetRootIds(target.nodes));
            }
        }
        finally
        {
            _compiling.Remove(script.id);
        }
    }

    // Recompila os scripts que importam deste (o conteúdo importado mudou).
    private void RecompileDependents(ScriptData script)
    {
        if (script == null) return;

        foreach (var other in scripts)
        {
            if (other == null || other == script) continue;
            if (other.nodes == null) continue;

            bool depends = false;
            foreach (var reference in EnumerateImportReferences(other))
            {
                if (reference.targetSection != ParameterSections.Scripts) continue;
                if (ParseId(reference.targetKey) == script.id) { depends = true; break; }
            }
            if (!depends) continue;

            Compile(other, GetRootIds(other.nodes));
        }
    }

    // Monta os nós mesclados: os blocos do script + as funções importadas de outros scripts.
    private SaveNodesData BuildMergedNodes(ScriptData script)
    {
        var original = script.nodes;
        var merged = new SaveNodesData {
            parametrosData = new List<ParameterNode>(original.parametrosData),
            blocosData = new List<BlockNode>(original.blocosData),
            variaveisData = new List<CreateVarNode>(original.variaveisData),
            functionLocals = new List<FunctionLocalVars>(original.functionLocals)
        };

        var plans = GetImportPlans(script);
        script.importPlans = plans;

        // Signals importados (só autocomplete).
        var importedSignals = new List<string>();
        foreach (var plan in plans) {
            if (plan.signals == null) continue;
            foreach (var signal in plan.signals) {
                if (string.IsNullOrEmpty(signal)) continue;
                if (!importedSignals.Contains(signal)) importedSignals.Add(signal);
            }
        }
        script.importedSignals = importedSignals;

        foreach (var plan in plans)
        {
            if (plan.source == null) continue; // alvo = entidade (resolvido no BlockCompiler)

            var sourceNodes = plan.source.mergedNodes ?? plan.source.nodes;
            if (sourceNodes == null) continue;

            var copier = new NodeGraphCopier(sourceNodes, merged, plan.FunctionRenames);

            foreach (var function in plan.functions)
            {
                if (function?.info == null) continue;

                int newId = copier.CopyFunction(function.info.declareBlockId, function.effectiveName);
                if (newId < 0) {
                    Debug.LogWarning($"[Import] Não foi possível copiar a função '{function.originalName}' de '{plan.sourceName}'.");
                    continue;
                }
                if (function.renamed) {
                    Debug.LogWarning($"[Import] A função '{function.originalName}' de '{plan.sourceName}' foi importada como '{function.effectiveName}' (o nome já existia no script).");
                }
            }
        }

        return merged;
    }

    // Monta os planos de import do script (usados pela UI e pela compilação).
    public List<ScriptImportPlan> GetImportPlans(ScriptData importer)
    {
        var plans = new List<ScriptImportPlan>();
        if (importer == null || importer.nodes == null) return plans;

        // Nomes já usados no script que importa (colisão -> sufixo automático).
        var takenFunctions = new HashSet<string>(GetLocalFunctionNames(importer));

        foreach (var reference in EnumerateImportReferences(importer))
        {
            var plan = new ScriptImportPlan {
                targetSection = reference.targetSection,
                targetKey = reference.targetKey,
                elementSection = reference.elementSection,
                elementKey = reference.elementKey
            };

            if (reference.targetSection == ParameterSections.Scripts)
            {
                var source = FindScriptById(ParseId(reference.targetKey));
                plan.source = source;
                plan.sourceName = source != null ? source.name : ("Script " + reference.targetKey);

                if (source != null)
                {
                    var catalog = GetCatalog(source);
                    BuildScriptFunctionPlan(plan, catalog, takenFunctions);
                    BuildScriptSignalPlan(plan, catalog);
                }
            }
            else
            {
                var entity = ResolveEntity(reference);
                plan.sourceName = entity != null ? entity.EntityName : null;
                BuildEntityPlan(plan, entity, reference);
            }

            plans.Add(plan);
        }

        return plans;
    }

    // Funções selecionadas + fechamento transitivo das dependências, com nome efetivo.
    private void BuildScriptFunctionPlan(ScriptImportPlan plan, ScriptExportCatalog catalog, HashSet<string> taken)
    {
        if (catalog == null) return;
        if (plan.elementSection != ParameterSections.Funcoes) return;

        var selected = new List<ScriptFunctionInfo>();
        if (plan.elementKey == ImportKeys.All) {
            selected.AddRange(catalog.ExportedFunctions());
        } else if (!string.IsNullOrEmpty(plan.elementKey) && plan.elementKey.StartsWith(ImportKeys.FunctionPrefix)) {
            var name = plan.elementKey.Substring(ImportKeys.FunctionPrefix.Length);
            var found = catalog.FindFunction(name);
            if (found != null && found.exported) selected.Add(found);
        }

        // Fechamento transitivo: as funções chamadas pelas selecionadas entram também
        // (mesmo não sendo exportadas), senão a chamada interna ficaria sem destino.
        var closure = new List<ScriptFunctionInfo>();
        var visited = new HashSet<string>();
        var queue = new Queue<ScriptFunctionInfo>();
        foreach (var function in selected) {
            if (function == null) continue;
            visited.Add(function.name);
            queue.Enqueue(function);
        }

        while (queue.Count > 0) {
            var current = queue.Dequeue();
            closure.Add(current);

            foreach (var called in current.calledFunctions) {
                if (!visited.Add(called)) continue;
                var dependency = catalog.FindFunction(called);
                if (dependency == null) continue;
                queue.Enqueue(dependency);
            }
        }

        foreach (var function in closure) {
            string effective = ScriptImportNaming.EffectiveName(function.name, plan.sourceName, taken);
            taken.Add(effective);
            plan.functions.Add(new ScriptImportFunction {
                effectiveName = effective,
                originalName = function.name,
                info = function
            });
            if (effective != function.name) plan.FunctionRenames[function.name] = effective;
        }
    }

    private void BuildScriptSignalPlan(ScriptImportPlan plan, ScriptExportCatalog catalog)
    {
        if (catalog == null) return;
        if (plan.elementSection != ParameterSections.Signals) return;

        if (plan.elementKey == ImportKeys.All) {
            foreach (var signal in catalog.signals) {
                if (!string.IsNullOrEmpty(signal) && !plan.signals.Contains(signal)) plan.signals.Add(signal);
            }
        } else if (!string.IsNullOrEmpty(plan.elementKey) && plan.elementKey.StartsWith(ImportKeys.SignalPrefix)) {
            var name = plan.elementKey.Substring(ImportKeys.SignalPrefix.Length);
            if (!string.IsNullOrEmpty(name) && !plan.signals.Contains(name)) plan.signals.Add(name);
        }
    }

    // Alvo = entidade: variáveis (internas e do script que a entidade executa) e signals.
    private void BuildEntityPlan(ScriptImportPlan plan, IMapEntity entity, ScriptImportReference reference)
    {
        if (entity == null) return;

        if (plan.elementSection == ParameterSections.Signals) {
            var signals = entity.GetExposedSignals();
            if (plan.elementKey == ImportKeys.All) {
                if (signals != null) {
                    foreach (var signal in signals) if (!string.IsNullOrEmpty(signal)) plan.signals.Add(signal);
                }
            } else if (!string.IsNullOrEmpty(plan.elementKey) && plan.elementKey.StartsWith(ImportKeys.SignalPrefix)) {
                var name = plan.elementKey.Substring(ImportKeys.SignalPrefix.Length);
                if (!string.IsNullOrEmpty(name)) plan.signals.Add(name);
            }
        } else if (plan.elementSection == ParameterSections.Variaveis) {
            var internalVars = entity.GetExposedVariables();
            var scriptVars = entity.GetScriptVariableNames();

            if (plan.elementKey == ImportKeys.All) {
                if (internalVars != null) {
                    foreach (var varName in internalVars) {
                        if (string.IsNullOrEmpty(varName)) continue;
                        plan.variables.Add(new ScriptImportVariable { localName = varName, varName = varName, isScriptVariable = false });
                    }
                }
                if (scriptVars != null) {
                    foreach (var varName in scriptVars) {
                        if (string.IsNullOrEmpty(varName)) continue;
                        plan.variables.Add(new ScriptImportVariable { localName = varName, varName = varName, isScriptVariable = true });
                    }
                }
            } else if (!string.IsNullOrEmpty(plan.elementKey) && plan.elementKey.StartsWith(ImportKeys.InternalVarPrefix)) {
                var name = plan.elementKey.Substring(ImportKeys.InternalVarPrefix.Length);
                if (!string.IsNullOrEmpty(name)) plan.variables.Add(new ScriptImportVariable { localName = name, varName = name, isScriptVariable = false });
            } else if (!string.IsNullOrEmpty(plan.elementKey) && plan.elementKey.StartsWith(ImportKeys.ScriptVarPrefix)) {
                var name = plan.elementKey.Substring(ImportKeys.ScriptVarPrefix.Length);
                if (!string.IsNullOrEmpty(name)) plan.variables.Add(new ScriptImportVariable { localName = name, varName = name, isScriptVariable = true });
            }
        }
    }

    // --- Leitura dos blocos de import -----------------------------------------

    // Todos os blocos de import do script (na ordem dos nós).
    public List<ScriptImportReference> EnumerateImportReferences(ScriptData script)
    {
        var result = new List<ScriptImportReference>();
        var nodes = script?.nodes;
        if (nodes?.blocosData == null) return result;

        foreach (var block in nodes.blocosData) {
            if (block == null) continue;
            if (block.functionId != "Import" && block.functionId != "ImportEntity") continue;
            result.Add(ReadImportReference(nodes, block));
        }
        return result;
    }

    private ScriptImportReference ReadImportReference(SaveNodesData nodes, BlockNode block)
    {
        var reference = new ScriptImportReference {
            blockId = block.id,
            targetSection = ParameterSections.Robos,
            targetKey = "",
            elementSection = ParameterSections.Variaveis,
            elementKey = ImportKeys.All
        };

        if (block.paramIds == null) return reference;

        if (block.paramIds.Count > 0) {
            var target = FindParam(nodes, block.paramIds[0]);
            if (target != null) {
                reference.targetKey = target.value ?? "";
                reference.targetSection = target.selectedSection;
            }
        }
        if (block.paramIds.Count > 1) {
            var element = FindParam(nodes, block.paramIds[1]);
            if (element != null) {
                reference.elementKey = element.value ?? ImportKeys.All;
                reference.elementSection = element.selectedSection;
            }
        }

        return reference;
    }

    private IMapEntity ResolveEntity(ScriptImportReference reference)
    {
        if (MapManager.instancia == null) return null;
        int id = ParseId(reference.targetKey);
        string section = reference.targetSection == ParameterSections.Construcoes
            ? MapManager.SectionConstruction
            : MapManager.SectionRobot;
        return MapManager.instancia.FindEntity(section, id, null);
    }

    // --- Catálogo e utilidades ------------------------------------------------

    public ScriptData FindScriptById(int id)
    {
        if (id == 0) return null;
        foreach (var script in scripts) {
            if (script != null && script.id == id) return script;
        }
        return null;
    }

    // Catálogo de exportações do script (derivado dos nós mesclados: o script
    // também oferece as funções que ele importou de outros).
    public ScriptExportCatalog GetCatalog(ScriptData script)
    {
        if (script == null) return null;
        if (_catalogs.TryGetValue(script.id, out var cached) && cached != null) return cached;

        var catalog = ScriptExportCatalog.Build(script.mergedNodes ?? script.nodes);
        catalog.script = script;
        _catalogs[script.id] = catalog;
        return catalog;
    }

    // Nomes das funções declaradas no próprio script (colisão com o importado).
    public static List<string> GetLocalFunctionNames(ScriptData script)
    {
        var names = new List<string>();
        var nodes = script?.nodes;
        if (nodes?.blocosData == null) return names;

        foreach (var block in nodes.blocosData) {
            if (block == null || block.functionId != "DeclareFunction") continue;
            if (block.paramIds == null || block.paramIds.Count == 0) continue;

            var param = FindParam(nodes, block.paramIds[0]);
            if (param == null || string.IsNullOrEmpty(param.value)) continue;
            names.Add(param.value);
        }
        return names;
    }

    // Roots do script: blocos de início de linha (rootId == id).
    public static List<int> GetRootIds(SaveNodesData nodes)
    {
        var roots = new List<int>();
        if (nodes?.blocosData == null) return roots;

        var seen = new HashSet<int>();
        foreach (var block in nodes.blocosData) {
            if (block == null) continue;
            if (block.rootId != block.id) continue;
            if (!seen.Add(block.id)) continue;
            roots.Add(block.id);
        }
        return roots;
    }

    private static ParameterNode FindParam(SaveNodesData nodes, int paramId)
    {
        if (paramId < 0 || nodes?.parametrosData == null) return null;
        foreach (var p in nodes.parametrosData) {
            if (p != null && p.id == paramId) return p;
        }
        return null;
    }

    private static int ParseId(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (int.TryParse(value.Trim(), out int id)) return id;
        return 0;
    }
    
    // Marca o bool dos signals importados como true (na compilação, antes da execução).
    // É esse set que gera a borda (edge): a entidade dispara o signal pra quem importou
    // o par (entidade, signal) exato. Cada par é marcado uma única vez.
    private void TryApplyEntityImports(ScriptData script)
    {
        if (script == null) return;
        if (script.entitySignalImports == null) return;

        var marcados = new HashSet<(int entityId, string signalName)>();

        foreach (var import in script.entitySignalImports)
        {
            if (string.IsNullOrEmpty(import.signalName)) continue;

            IMapEntity entity = FindEntityByImport(import.entityName, import.entityInstanceId);
            if (entity == null) continue;

            int entityId = entity.GetEntityId();
            if (!marcados.Add((entityId, import.signalName))) continue;

            // Bool do signal da entidade = true: fica marcado para o próximo trigger.
            entity.SetSignalBool(import.signalName, true);
        }
    }
    
    // Busca a entidade pelo instance id (preferencial) ou pelo nome.
    private IMapEntity FindEntityByImport(string entityName, int entityInstanceId)
    {
        return MapManager.instancia?.FindEntity(null, entityInstanceId, entityName);
    }
    
    public void OpenScript(ScriptData script)
    {
        Debug.Log("Build blocks from script: " + script?.id);

        // scriptOnFocus é setado ANTES do evento: o load (WorkspaceLoadScript) usa
        // scriptOnFocus pra achar as variáveis locais das funções.
        scriptOnFocus = script;

        SaveNodesData nodes = null;
        if (script != null) nodes = script.nodes;
        OnScriptOpen?.Invoke(nodes);
        ChangeInterfaceScriptName(script?.name);
    }

    public void ChangeInterfaceScriptName(string name) {
        ScriptInterfaceStateChanged?.Invoke(name);
    }

    public void DeleteScript(ScriptData script)
    {
        if (script == scriptOnFocus) OnScriptClosed?.Invoke();
        scripts.Remove(script);
        jsonBuilder.DeleteJson(script.id.ToString());
    }

    public void CreateScript(ScriptData script)
    {
        scripts.Add(script);
    }
}
