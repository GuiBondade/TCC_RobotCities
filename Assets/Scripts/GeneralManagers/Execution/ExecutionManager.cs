using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

using ScriptRunner;

// Assinatura viva de uma função (parâmetros na ordem, com tipo), usada pelo popup do bloco de chamada.
[System.Serializable]
public class FunctionSignatureInfo
{
    public List<string> paramNames = new List<string>();
    public List<ParameterType> paramTypes = new List<ParameterType>();
}

public class ExecutionManager : MonoBehaviour // arquivo set/delete json
{
    // Signal de entrada disparado quando a execução do robô é ativada pela interface.
    public const string ActivationSignalName = "inicio";

    /* 
        EXECUTAR TODOS OS ROBOS A CADA TICK
        ROBOS ADICIONADOS/RETIRADOS NA LISTA DE EXECUÇÃO A PARTIR DA ATIVAÇÃO/DESATIVAÇÃO PELO PLAYER
        
        (VAI SER CRIADO UMA LISTA DE SCRIPTS SALVOS COM ID E REFERENCIA PRO ARQUIVO JSON(a qual vai ser exposta no dropdown da interface dos robos),
            compiledScripts no scriptManager, dicionario de struct a ser melhorado)

     */
    public static ExecutionManager instancia { get; private set; }

    private float acumuladorTempo = 0f;
    public float tickTime = 0.2f; // Ex: 100ms por tick

    private List<ExecutionContext> functionsInExecution = new List<ExecutionContext>(); 
    private List<ExecutionContext> functionsToRemove = new List<ExecutionContext>(); 
    private List<ExecutionContext> contextsToActivate = new List<ExecutionContext>(); 

    // Signal -> contextos (robôs) que escutam esse signal.
    // Cada contexto guarda, no seu próprio listenBlocksBySignal, o bloco de escuta daquele signal.
    public Dictionary<string, List<ExecutionContext>> signalContexts = new Dictionary<string, List<ExecutionContext>>();
    
    // Entity signal -> contextos que importaram (entityId, signalName) exato
    private Dictionary<(int entityId, string signalName), List<ExecutionContext>> entitySignalContexts = new Dictionary<(int entityId, string signalName), List<ExecutionContext>>();

    // Lista extra de strings de signal voltada para a UI (além das chaves do signalContexts).
    // Guarda signals digitados nos parâmetros como "novos signals" e alimenta o dropdown dos parâmetros.
    public List<string> uiSignals = new List<string>();

    // Signals importados de outros scripts (só autocomplete: não viram signal do script atual).
    public List<string> importedSignals = new List<string>();

    public event Action<string, string> OnSignalRenamed;

    // --- Funções (espelho do bloco de signals acima) ---
    // Lista viva de funções do script em edição, alimenta o popup do parâmetro de função do call.
    public List<string> uiFunctions = new List<string>();

    // Assinatura viva (parâmetros) por função, mantida pelos blocos de declaração enquanto edita.
    // O ScriptData.functions compilado só atualiza no save: aqui é a fonte pra UI do bloco de chamada.
    public Dictionary<string, FunctionSignatureInfo> uiFunctionSignatures = new Dictionary<string, FunctionSignatureInfo>();

    public event Action<string, string> OnFunctionRenamed;
    public event Action OnFunctionListChanged;
    public event Action<string> OnFunctionSignatureChanged;

    // Listas reaproveitadas pra evitar alocação (padrão variablesNamesTemp do VariableManager).
    private readonly HashSet<string> signalOptionsTemp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> signalOptionsList = new List<string>();
    private readonly List<string> functionOptionsTemp = new List<string>();
    private readonly List<string> functionOptionsList = new List<string>();

    // Registra uma função declarada (novo declare ou load): adiciona na lista UI se ainda não existir.
    public string RegisterFunction(string functionName) {
        functionName = functionName?.Trim();
        if (string.IsNullOrEmpty(functionName)) return null;

        if (!uiFunctions.Contains(functionName)) {
            uiFunctions.Add(functionName);
            OnFunctionListChanged?.Invoke();
        }
        return functionName;
    }

    // Altera o nome de uma função na lista UI e propaga (OnFunctionRenamed), atualizando os
    // parâmetros de função que usavam o nome antigo. Move a assinatura junto.
    public string RenameFunction(string oldName, string newName) {
        if (string.IsNullOrEmpty(oldName)) return RegisterFunction(newName);

        newName = newName?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == oldName) return oldName;

        int index = uiFunctions.IndexOf(oldName);
        if (index < 0) return RegisterFunction(newName); // não estava na lista UI

        var otherNames = new HashSet<string>(uiFunctions, StringComparer.OrdinalIgnoreCase);
        otherNames.Remove(oldName);

        string uniqueName = StringUtils.EnsureUniqueString(newName, otherNames, null);
        uiFunctions[index] = uniqueName;

        if (uiFunctionSignatures.TryGetValue(oldName, out var signature)) {
            uiFunctionSignatures.Remove(oldName);
            uiFunctionSignatures[uniqueName] = signature;
        }

        OnFunctionRenamed?.Invoke(oldName, uniqueName);
        OnFunctionListChanged?.Invoke();
        return uniqueName;
    }

    // Remove uma função (declare deletado/limpo): sai da lista e o popup do call deixa de listá-la.
    public void RemoveFunction(string functionName) {
        if (string.IsNullOrEmpty(functionName)) return;

        bool removed = uiFunctions.Remove(functionName);
        uiFunctionSignatures.Remove(functionName);
        if (removed) OnFunctionListChanged?.Invoke();
    }

    // Atualiza a assinatura viva de uma função (chamado pelo bloco de declaração ao editar parâmetros).
    public void UpdateFunctionSignature(string functionName, List<string> paramNames, List<ParameterType> paramTypes) {
        if (string.IsNullOrEmpty(functionName)) return;

        RegisterFunction(functionName);

        uiFunctionSignatures[functionName] = new FunctionSignatureInfo {
            paramNames = paramNames != null ? new List<string>(paramNames) : new List<string>(),
            paramTypes = paramTypes != null ? new List<ParameterType>(paramTypes) : new List<ParameterType>()
        };

        OnFunctionSignatureChanged?.Invoke(functionName);
    }

    // Limpa a lista UI de funções (chamado ao abrir script; as funções voltam via RegisterFunction no load).
    public void ClearUiFunctions() {
        uiFunctions.Clear();
        uiFunctionSignatures.Clear();
        OnFunctionListChanged?.Invoke();
    }

    // Opções de função pro popup do parâmetro do call (padrão do GetSignalOptions).
    public List<string> GetFunctionOptions() {
        functionOptionsTemp.Clear();
        functionOptionsList.Clear();

        foreach (string function in uiFunctions) {
            if (!functionOptionsTemp.Contains(function)) {
                functionOptionsTemp.Add(function);
                functionOptionsList.Add(function);
            }
        }
        return functionOptionsList;
    }

    // Assinatura viva da função (nomes + tipos dos parâmetros), ou null se não registrada.
    public FunctionSignatureInfo GetFunctionSignature(string functionName) {
        if (string.IsNullOrEmpty(functionName)) return null;
        return uiFunctionSignatures.TryGetValue(functionName, out var signature) ? signature : null;
    }

    // Registra um signal digitado num parâmetro: adiciona na lista UI se ainda não existir
    // (nem na lista UI nem nas chaves do dicionário geral). Retorna a string final (ou null se vazia).
    public string RegisterSignal(string signalName) {
        signalName = signalName?.Trim();
        if (string.IsNullOrEmpty(signalName)) return null;

        if (!uiSignals.Contains(signalName) && !signalContexts.ContainsKey(signalName)) {
            uiSignals.Add(signalName);
        }
        return signalName;
    }

    // Altera a string de um signal na lista UI e propaga o evento (OnSignalRenamed),
    // atualizando os outros parâmetros que usavam o nome antigo.
    public string RenameSignal(string oldName, string newName) {
        if (string.IsNullOrEmpty(oldName)) return RegisterSignal(newName);

        newName = newName?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == oldName) return oldName;

        int index = uiSignals.IndexOf(oldName);
        if (index < 0) return RegisterSignal(newName); // não estava na lista UI (ex.: signal que só existe no dicionário)

        // garante nome único em relação aos outros signals UI e às chaves do dicionário geral
        var otherNames = new HashSet<string>(uiSignals, StringComparer.OrdinalIgnoreCase);
        otherNames.Remove(oldName);
        foreach (var key in signalContexts.Keys) otherNames.Add(key);

        string uniqueName = StringUtils.EnsureUniqueString(newName, otherNames, null);
        uiSignals[index] = uniqueName;

        OnSignalRenamed?.Invoke(oldName, uniqueName);
        return uniqueName;
    }

    // Limpa a lista UI (chamado ao abrir script; os signals do script voltam via RegisterSignal no load).
    public void ClearUiSignals() {
        uiSignals.Clear();
        importedSignals.Clear();
    }

    // Registra um signal importado de outro script: entra no autocomplete dos dropdowns,
    // mas não passa a ser um signal do script atual (só o runtime do importador usa).
    public void RegisterImportedSignal(string signalName) {
        signalName = signalName?.Trim();
        if (string.IsNullOrEmpty(signalName)) return;
        if (!importedSignals.Contains(signalName)) importedSignals.Add(signalName);
    }

    // Opções de signal pra popular o dropdown do parâmetro: união da lista UI com as
    // chaves do dicionário geral (signalContexts), sem duplicatas.
    public List<string> GetSignalOptions() {
        signalOptionsTemp.Clear();
        signalOptionsList.Clear();

        foreach (string signal in uiSignals) {
            if (signalOptionsTemp.Add(signal)) signalOptionsList.Add(signal);
        }
        foreach (var key in signalContexts.Keys) {
            if (signalOptionsTemp.Add(key)) signalOptionsList.Add(key);
        }
        foreach (string signal in importedSignals) {
            if (signalOptionsTemp.Add(signal)) signalOptionsList.Add(signal);
        }
        return signalOptionsList;
    }

    public TickSignal tickSignal;
    [SerializeField] public TickSpeedSO tickSpeedSO;

    [Header("Inventário")]
    [SerializeField] public ToolDatabase toolDatabase;      // catálogo de ferramentas (id usado no slot Tool do robô)
    [SerializeField] public MaterialDatabase materialDatabase; // opcional: só pro fallback de material por nome

    // Ferramenta pelo id (o id fica no currentTypeId do slot Tool do robô).
    public ToolData GetTool(int toolId) {
        if (toolDatabase == null) {
            Debug.LogError("ToolDatabase não configurado no ExecutionManager!");
            return null;
        }
        return toolDatabase.GetById(toolId);
    }

    // Resolve o id de uma ferramenta vindo do parâmetro (pelo nome exibido).
    public int GetToolTypeId(string toolName) {
        if (string.IsNullOrEmpty(toolName)) return -1;
        if (int.TryParse(toolName, out int parsed)) return parsed;
        if (toolDatabase == null) return -1;
        var tool = toolDatabase.TryGetByName(toolName);
        return tool != null ? tool.id : -1;
    }

    // Resolve o id de um material vindo do parâmetro.
    // Padrão: o SectionDatabase guarda o id (string numérica). Fallback: nome do material.
    public int GetMaterialTypeId(string materialName) {
        if (string.IsNullOrEmpty(materialName)) return -1;

        if (int.TryParse(materialName, out int parsed)) return parsed;

        if (materialDatabase != null && materialDatabase.TryGetByName(materialName, out var data)) {
            return data.typeId;
        }

        Debug.LogWarning("Material desconhecido: '" + materialName + "'");
        return -1;
    }

    private void Awake() {
        if (instancia != null && instancia != this) {
            Destroy(gameObject);
            return;
        }
        instancia = this;
    }

    private void OnEnable() {
        RobotSignals.OnActivateExecution += ActivateExecution;
        RobotSignals.OnDeactivateExecution += DeactivateExecution;
    }

    private void OnDisable() {
        RobotSignals.OnActivateExecution -= ActivateExecution;
        RobotSignals.OnDeactivateExecution -= DeactivateExecution;

        tickSpeedSO?.UnregisterListener(SetTickSpeed);
    }

    private void OnDestroy() {
        if (instancia == this) instancia = null;
    }

    private void Start() {
        tickSpeedSO.RegisterListener(SetTickSpeed);
        SetTickSpeed(tickSpeedSO.speedTick); // aplica o valor atual já de início
    }

    // chamado pelo signal do TickSpeedSO (signal de saída)
    public void SetTickSpeed(float newSpeed) {
        if (newSpeed <= 0f) {
            Debug.LogError("[Execução] A velocidade do tick deve ser maior que zero.", this);
            return;
        }

        tickTime = 1f / newSpeed;
        acumuladorTempo = Mathf.Min(acumuladorTempo, tickTime);
    }

    public void ActivateExecution(RobotScript robot) {
        if (robot == null || robot.scriptData == null) {
            Debug.LogWarning("[Execução] Não foi possível ativar o robô: script não selecionado.", this);
            return;
        }

        // Idempotente: a interface pode pedir ativação mais de uma vez (ex.: reabrir o painel).
        // Criar outro contexto aqui deixaria o antigo órfão em signalContexts/functionsInExecution.
        if (robot.executionContext != null) {
            Debug.LogWarning("[Execução] O robô já está com execução ativa; a ativação foi ignorada.", robot);
            return;
        }

        var context = new ExecutionContext();
        context.robotScript = robot;
        
        ScriptData scriptData = robot.scriptData;

        if (scriptData.vars != null)
        {
            foreach (var var in scriptData.vars)
            {
                object valorInicial = ValueParser.ParseRawString(var.defaultValue);
                
                context.SetVariable(var.varName, valorInicial);
            }
        }
        
        // Não define current nem adiciona em functionsInExecution ainda:
        // só começa a executar ao receber um signal (TriggerSignal).
        // Cada signal escutado vira uma chave no contexto (com o bloco de escuta) e o contexto entra na lista daquele signal.
        if (scriptData.startBlocksBySignal != null)
        {
            foreach (var kvp in scriptData.startBlocksBySignal)
            {
                if (kvp.Value == null || kvp.Value.Count == 0) continue; // chave só de emissão: nenhum bloco para escutar

                context.listenBlocksBySignal[kvp.Key] = kvp.Value[0];

                if (!signalContexts.TryGetValue(kvp.Key, out var contexts))
                {
                    contexts = new List<ExecutionContext>();
                    signalContexts[kvp.Key] = contexts;
                }
                if (!contexts.Contains(context)) contexts.Add(context);
            }
        }
        
        // Monta a lista de entidades importadas no contexto: referência gerenciada direta
        // + variáveis expostas + signals importados.
        if (scriptData.resolvedEntityImports != null && MapManager.instancia != null)
        {
            foreach (var resolved in scriptData.resolvedEntityImports)
            {
                IMapEntity entity = FindEntityByImportRuntime(resolved.entityName, resolved.entityInstanceId);
                if (entity == null) continue;

                var entityData = new ImportedEntityData
                {
                    entityId = entity.GetEntityId(),
                    entityRef = entity, // referência gerenciada (C# Reference)
                    section = resolved.section,
                    entityName = entity.EntityName
                };

                // Variáveis expostas da entidade: guarda a referência direta (managed reference)
                if (resolved.importedVariables != null)
                {
                    foreach (var varName in resolved.importedVariables)
                    {
                        if (string.IsNullOrEmpty(varName)) continue;
                        if (entity.TryGetExposedVariable(varName, out object varRef))
                        {
                            entityData.importedVariables[varName] = varRef;
                            context.importedEntityVarRefs[(entityData.entityId, varName)] = varRef;
                        }
                    }
                }

                // Signals importados da entidade
                if (resolved.importedSignals != null)
                {
                    foreach (var signalName in resolved.importedSignals)
                    {
                        if (string.IsNullOrEmpty(signalName)) continue;
                        entityData.importedSignals.Add(signalName);
                    }
                }

                context.importedEntities.Add(entityData);
            }
        }
        
        // Registra os entity signals importados: cada contexto que importou (entidade, signal) exato
        // passa a receber o trigger daquela entidade.
        RegisterEntitySignalImports(context, scriptData);
        
        if (scriptData.entityVariableImports != null && MapManager.instancia != null)
        {
            foreach (var import in scriptData.entityVariableImports)
            {
                IMapEntity entity = FindEntityByImportRuntime(import.entityName, import.entityInstanceId);
                if (entity == null) continue;

                string localName = string.IsNullOrEmpty(import.localName) ? import.varName : import.localName;

                if (import.isScriptVariable)
                {
                    // Variável do script que a entidade executa: leitura ao vivo, escrita bloqueada.
                    var capturedEntity = entity;
                    var capturedVar = import.varName;
                    context.BindReadOnly(localName, () => capturedEntity.GetScriptVariableValue(capturedVar));
                    continue;
                }

                if (!entity.TryGetExposedVariable(import.varName, out object varRef)) continue;

                int entityId = entity.GetEntityId();
                context.importedEntityVarRefs[(entityId, import.varName)] = varRef;

                // A referência direta (managed reference) da variável da entidade fica
                // disponível no contexto como variável do script (nome local do import).
                context.SetVariable(localName, varRef);
            }
        }

        robot.executionContext = context;

        // Ponto de entrada padrão do script: signal "inicio" (selecionável nas opções do SectionDatabase).
        // Inicia SOMENTE o contexto recém-criado: TriggerSignal faria broadcast e reiniciaria
        // outros robôs que escutam o mesmo signal.
        if (context.listenBlocksBySignal.TryGetValue(ActivationSignalName, out var startBlock)) {
            context.current = startBlock;
            RequestActivation(context);
        }

    }
    
    // Registra no contexto os signals de entidade importados: associa o bloco de escuta
    // do signal (no script) ao par (entidade, signal).
    private void RegisterEntitySignalImports(ExecutionContext context, ScriptData scriptData)
    {
        if (scriptData == null || scriptData.resolvedEntityImports == null) return;
        if (scriptData.startBlocksBySignal == null) return;
        
        foreach (var resolved in scriptData.resolvedEntityImports)
        {
            IMapEntity entity = FindEntityByImportRuntime(resolved.entityName, resolved.entityInstanceId);
            if (entity == null) continue;
            if (resolved.importedSignals == null) continue;
            
            foreach (var signalName in resolved.importedSignals)
            {
                if (string.IsNullOrEmpty(signalName)) continue;
                
                // Bloco de escuta do signal no script (o bloco que roda quando o signal chega)
                if (!scriptData.startBlocksBySignal.TryGetValue(signalName, out var listenBlocks)) continue;
                if (listenBlocks == null || listenBlocks.Count == 0) continue;
                
                RegisterEntitySignalImport(entity, signalName, context, listenBlocks[0]);
            }
        }
    }

    public void DeactivateExecution(RobotScript robot) {
        if (robot == null) return;
        var ctx = robot.executionContext;
        if (ctx == null) return;

        functionsInExecution.Remove(ctx);
        functionsToRemove.Remove(ctx);
        contextsToActivate.Remove(ctx);
        ctx.current = null;
        ctx.ticksOcupado = 0;
        ctx.tickActionBlock = null;
        ctx.tickActionTotal = 0;
        ctx.ClearExecutionState();

        if (robot.robotMovement != null) {
            robot.robotMovement.isMoving = false;
            robot.robotMovement.distanceToMove = 0;
        }

        ctx.listenBlocksBySignal.Clear();
        ctx.listenBlocksByEntitySignal.Clear();
        ctx.importedEntityVarRefs.Clear();
        ctx.importedEntities.Clear();

        // Remove o contexto de todos os signals; apaga a key se a lista esvaziar.
        List<string> emptyKeys = new List<string>();
        foreach (var kvp in signalContexts) {
            kvp.Value.Remove(ctx);
            if (kvp.Value.Count == 0) emptyKeys.Add(kvp.Key);
        }
        foreach (var k in emptyKeys) signalContexts.Remove(k);
        
        // Remove o contexto de todos os entity signals
        List<(int entityId, string signalName)> emptyEntityKeys = new List<(int entityId, string signalName)>();
        foreach (var kvp in entitySignalContexts) {
            kvp.Value.Remove(ctx);
            if (kvp.Value.Count == 0) emptyEntityKeys.Add(kvp.Key);
        }
        foreach (var k in emptyEntityKeys) entitySignalContexts.Remove(k);

        robot.executionContext = null;
    }

    // Futuro sistema runtime chama isso ao receber um signal.
    // Também chamado pelos blocos de emissão de signal durante a execução.
    // Redisparável: seta o bloco de escuta como current e reativa o contexto.
    public void TriggerSignal(string signalName) {
        TriggerSignal(signalName, null);
    }

    // Sobrecarga com contexto para separar rigidamente mapa principal e puzzles.
    // Objetivos recebem o signal antes do retorno antecipado quando não há listeners.
    public void TriggerSignal(string signalName, MapContext originContext) {
        if (string.IsNullOrWhiteSpace(signalName)) return;

        ObjectiveSignals.TriggerSignal(signalName, 1, originContext);

        if (!signalContexts.TryGetValue(signalName, out var contexts)) return;

        foreach (var context in contexts) {
            if (!context.listenBlocksBySignal.TryGetValue(signalName, out var listenBlock)) continue;

            context.current = listenBlock;
            RequestActivation(context);
        }
    }
    
    // Registra import de entity signal (chamado na compilação/ativação)
    public void RegisterEntitySignalImport(IMapEntity entity, string signalName, ExecutionContext ctx, ScriptRunner.ExecutionBlock listenBlock)
    {
        if (entity == null || ctx == null || listenBlock == null || string.IsNullOrEmpty(signalName)) return;
        int entityId = entity.GetEntityId();
        
        var key = (entityId, signalName);
        if (!entitySignalContexts.TryGetValue(key, out var contexts))
        {
            contexts = new List<ExecutionContext>();
            entitySignalContexts[key] = contexts;
        }
        if (!contexts.Contains(ctx)) contexts.Add(ctx);
        
        var entityKey = new ScriptRunner.EntitySignalKey(entityId, signalName);
        ctx.listenBlocksByEntitySignal[entityKey] = listenBlock;
    }
    
    // Edge-trigger: dispara para contextos que importaram (entity, signal) exato.
    // O bool só é limpo quando a borda foi entregue a alguém: assim o import feito na
    // compilação (antes do robô executar) deixa o bool marcado como true, e a entidade
    // precisa setar o bool de novo pra gerar a próxima borda.
    public void TriggerEntitySignalFromEntity(IMapEntity entity, string signalName)
    {
        if (entity == null || string.IsNullOrEmpty(signalName)) return;

        // Entity signals também alimentam objetivos, com o contexto do mapa de origem.
        ObjectiveSignals.TriggerSignal(signalName, 1, ResolveEntityMapContext(entity));

        int entityId = entity.GetEntityId();
        
        bool delivered = false;
        var key = (entityId, signalName);
        if (entitySignalContexts.TryGetValue(key, out var contexts))
        {
            var entityKey = new ScriptRunner.EntitySignalKey(entityId, signalName);
            foreach (var context in contexts)
            {
                if (!context.listenBlocksByEntitySignal.TryGetValue(entityKey, out var listenBlock)) continue;
                context.current = listenBlock;
                RequestActivation(context);
                delivered = true;
            }
        }
        
        // Edge-trigger: limpa o bool só depois de entregar a borda.
        if (delivered) entity.ClearSignalBool(signalName);
    }

    private static MapContext ResolveEntityMapContext(IMapEntity entity)
    {
        if (entity is BuildingController building) return building.mapContext;
        if (entity is ResourceController resource) return resource.mapContext;
        if (entity is RobotScript robot && robot.robotMovement != null) return robot.robotMovement.mapContext;
        return null;
    }

    // Adição deferida: um TriggerSignal pode acontecer durante o foreach do tick,
    // então não dá pra mutar functionsInExecution na hora.
    private void RequestActivation(ExecutionContext context) {
        if (!functionsInExecution.Contains(context) && !contextsToActivate.Contains(context)) {
            contextsToActivate.Add(context);
        }
    }

    private IMapEntity FindEntityByImportRuntime(string entityName, int entityInstanceId)
    {
        return MapManager.instancia?.FindEntity(null, entityInstanceId, entityName);
    }
    
    void Update() {
        acumuladorTempo += Time.deltaTime;
        while (acumuladorTempo >= tickTime) {
            TickExecution();
            acumuladorTempo -= tickTime;
        }
    }

    // chamado a cada tick
    public void TickExecution() {
        tickSignal.InvokeTick();

        foreach (var context in functionsInExecution) {
            if (context.ticksOcupado > 0) {
                context.ticksOcupado--; // Reduz o tempo restante da animação/ação

                // Debug contínuo da ação que está ocupando o robô (ex.: ExtrairRecurso).
                if (context.ticksOcupado > 0) {
                    if (context.tickActionBlock != null) DebugContinuo.Emitir(context, context.tickActionBlock);
                } else {
                    context.tickActionBlock = null;
                    context.tickActionTotal = 0;
                }
                continue; // Pula o script dele neste tick (ele está ocupado trabalhando)
            }
            if (context.current == null) {
                functionsToRemove.Add(context);
                continue;
            }

            ExecutionBlock executed = context.current;
            ExecutionBlock next = executed.Execute(context);
            // Balão de debug do bloco executado (o bloco Registro mostra o próprio texto).
            DebugContinuo.Emitir(context, executed);
            // Se um signal no meio da execução já trocou o current, respeita a sequência dele.
            if (context.current == executed) context.current = next;
        }

        foreach (var context in functionsToRemove) functionsInExecution.Remove(context);
        functionsToRemove.Clear();

        foreach (var context in contextsToActivate) functionsInExecution.Add(context);
        contextsToActivate.Clear();
    }
}
