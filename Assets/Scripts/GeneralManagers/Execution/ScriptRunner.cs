using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ScriptRunner
{
    // Rótulo + provider de um parâmetro do bloco, presos na compilação.
    // O valor é lido no runtime (variável pega o valor atual).
    public sealed class DebugParamInfo
    {
        public string rotulo;
        public IValueProvider provider;
    }

    // Debug contínuo: um balão por bloco executado (toggled por "Exibir Ações Recentes"
    // na interface do robô -> RobotData.isDebugging).
    public static class DebugContinuo
    {
        // Emite o balão do bloco recém-executado. O bloco de debug (Registro) já mostra
        // o próprio texto, então não recebe a mensagem automática.
        public static void Emitir(ExecutionContext context, ExecutionBlock bloco)
        {
            if (context == null || bloco == null) return;
            if (bloco.EmiteDebugProprio) return;
            if (!DebugAtivo(context)) return;

            string texto = MontarMensagem(context, bloco);
            if (string.IsNullOrEmpty(texto)) return;

            context.robotScript?.GetRobotDebug()?.DebugSpawner(texto);
        }

        // O debug contínuo é ligado/desligado pelo toggle da interface do robô.
        public static bool DebugAtivo(ExecutionContext context)
        {
            var robot = context?.robotScript;
            return robot != null && robot.robotData != null && robot.robotData.isDebugging;
        }

        private static string MontarMensagem(ExecutionContext context, ExecutionBlock bloco)
        {
            string nome = string.IsNullOrEmpty(bloco.debugNomeBloco) ? "Bloco" : bloco.debugNomeBloco;

            // total <= 0 = bloco de 1 tick: não mostra x/y.
            var progresso = bloco.GetDebugTickProgress(context);
            string cabecalho = progresso.total > 0
                ? $"executou {nome} {progresso.atual}/{progresso.total}"
                : $"executou {nome}";

            if (bloco.debugParams == null || bloco.debugParams.Count == 0) return cabecalho;

            var sb = new System.Text.StringBuilder();
            sb.Append(cabecalho).Append(" com parâmetros:\n");

            bool primeiro = true;
            foreach (var param in bloco.debugParams)
            {
                if (param == null) continue;

                object valor = null;
                try { valor = param.provider?.GetValue(context); }
                catch { valor = null; }

                if (!primeiro) sb.Append(", ");
                primeiro = false;
                sb.Append(param.rotulo).Append(": ").Append(FormatarValor(valor));
            }

            return primeiro ? cabecalho : sb.ToString();
        }

        public static string FormatarValor(object valor)
        {
            switch (valor)
            {
                case null: return "vazio";
                case bool b: return b ? "verdadeiro" : "falso";
                case Vector2Int v:
                    if (v == Vector2Int.up) return "cima";
                    if (v == Vector2Int.down) return "baixo";
                    if (v == Vector2Int.left) return "esquerda";
                    if (v == Vector2Int.right) return "direita";
                    return $"({v.x}, {v.y})";
                default: return valor.ToString();
            }
        }
    }

    public interface IValueProvider { object GetValue(ExecutionContext context); }

    public class LiteralValue : IValueProvider
    {
        private readonly object _value;
        public LiteralValue(object value) => _value = value;
        public object GetValue(ExecutionContext context) => _value;
    }

    public class VariableValue : IValueProvider
    {
        private readonly string _varName;
        public VariableValue(string varName) => _varName = varName;
        public object GetValue(ExecutionContext context) => context.GetVariable(_varName);
    }

    public class OperationValue : IValueProvider
    {
        private readonly IValueProvider _left;
        private readonly IValueProvider _right;
        private readonly Func<object, object, object> _compiledOp; // Modificado para object (suporta matemática e lógica)

        public OperationValue(IValueProvider left, IValueProvider right, Func<object, object, object> compiledOp)
        {
            _left = left;
            _right = right;
            _compiledOp = compiledOp;
        }

        public object GetValue(ExecutionContext context)
        {
            object leftValue = _left != null ? _left.GetValue(context) : null;
            object rightValue = _right != null ? _right.GetValue(context) : null;
            return _compiledOp(leftValue, rightValue);
        }
    }

    public class EntityContainsValue : IValueProvider
    {
        private readonly IValueProvider _direction;
        private readonly IValueProvider _entityKey;

        public EntityContainsValue(IValueProvider direction, IValueProvider entityKey) {
            _direction = direction;
            _entityKey = entityKey;
        }

        public object GetValue(ExecutionContext context) {
            if (context == null || context.robotScript == null || context.robotScript.robotMovement == null) return false;

            object directionValue = _direction != null ? _direction.GetValue(context) : null;
            if (!(directionValue is Vector2Int direction)) return false;

            string key = _entityKey != null ? _entityKey.GetValue(context) as string : null;
            if (string.IsNullOrEmpty(key)) return false;

            var map = MapManager.instancia;
            if (map == null) return false;

            Vector3Int target = context.robotScript.robotMovement.currentCell
                + new Vector3Int(direction.x, direction.y, 0);
            return EntityFilter.Matches(key, map.GetTileEntity(target));
        }
    }

    public struct EntitySignalKey
    {
        public int entityId;
        public string signalName;
        
        public EntitySignalKey(int id, string name)
        {
            entityId = id;
            signalName = name;
        }
    }
    
    [System.Serializable]
    public class ImportedEntityData
    {
        public int entityId;
        public IMapEntity entityRef; // referência gerenciada (C# Reference)
        public string section;       // "Robo" | "Construcao" | "Script"
        public string entityName;
        // Mapeamentos rápidos para esse contexto
        public Dictionary<string, object> importedVariables = new Dictionary<string, object>();
        public List<string> importedSignals = new List<string>();
    }
    
    // Estado mutável de um WalkBlock durante a execução de UM robô.
    // Fica no contexto (e não no bloco compilado) porque o grafo é compartilhado
    // entre todos os robôs que usam o mesmo ScriptData.
    public sealed class WalkState
    {
        public bool initialized;
        public Vector2Int direction;
        // Progresso do debug contínuo (1 tick = 1 tentativa de passo).
        public int passosTotais;
        public int passosDados;
    }

    public class ExecutionContext
    {
        public RobotScript robotScript { get; set; }
        public int ticksOcupado = 0; // poe em ingles
        // Ação que está ocupando o robô por N ticks (ex.: ExtrairRecurso) — usada pelo debug contínuo.
        public ExecutionBlock tickActionBlock;
        public int tickActionTotal = 0;
        public ExecutionBlock current;
        // signal -> bloco de escuta daquele signal (um por chave: garante um bloco por contexto/signal).
        public Dictionary<string, ExecutionBlock> listenBlocksBySignal = new Dictionary<string, ExecutionBlock>();

        // Estado dos WalkBlocks deste robô. O grafo compilado é compartilhado,
        // então qualquer estado de execução precisa viver no contexto.
        private readonly Dictionary<ExecutionBlock, WalkState> _walkStates = new Dictionary<ExecutionBlock, WalkState>();

        public WalkState GetWalkState(ExecutionBlock block)
        {
            if (block == null) return null;

            if (!_walkStates.TryGetValue(block, out var state))
            {
                state = new WalkState();
                _walkStates[block] = state;
            }
            return state;
        }

        public void ClearExecutionState()
        {
            _walkStates.Clear();
        }
        
        // Entity signal imports: (entityId, signalName) -> bloco de escuta
        public Dictionary<EntitySignalKey, ExecutionBlock> listenBlocksByEntitySignal = new Dictionary<EntitySignalKey, ExecutionBlock>();
        
        // Referências diretas de variáveis importadas da entidade (entityId, varName) -> referência direta
        public Dictionary<(int entityId, string varName), object> importedEntityVarRefs = new Dictionary<(int entityId, string varName), object>();
        
        // Lista de objetos de dados de entidades que tiveram variáveis/signals importados
        public List<ImportedEntityData> importedEntities = new List<ImportedEntityData>();
        
        private readonly Dictionary<string, object> _vars = new Dictionary<string, object>();
        // Variáveis importadas de outra entidade/script: somente leitura.
        // O valor é buscado ao vivo (getter) e a escrita é ignorada.
        private readonly Dictionary<string, Func<object>> _readOnlyVars = new Dictionary<string, Func<object>>();
        // Pilha de escopos locais de função (topo = função em execução).
        // Parâmetros e variáveis locais dividem o mesmo dicionário: unificados após o setup da chamada.
        private readonly Stack<Dictionary<string, object>> _localScopes = new();

        public void PushLocalScope(Dictionary<string, object> scope) => _localScopes.Push(scope);
        public void PopLocalScope() { if (_localScopes.Count > 0) _localScopes.Pop(); }

        // Liga uma variável importada (só leitura): o valor vem do getter, sempre fresco.
        public void BindReadOnly(string name, Func<object> getter) {
            if (string.IsNullOrEmpty(name)) return;
            _readOnlyVars[name] = getter;
            _vars[name] = getter != null ? getter() : null;
        }

        public bool IsReadOnly(string name) => !string.IsNullOrEmpty(name) && _readOnlyVars.ContainsKey(name);

        // Locais sombreiam globais na leitura; na escrita, só cai pro global se o nome não existir no escopo local.
        public void SetVariable(string name, object val)
        {
            // Variável importada: quem importa não altera (só leitura).
            if (!string.IsNullOrEmpty(name) && _readOnlyVars.ContainsKey(name)) return;

            if (_localScopes.Count > 0)
            {
                var scope = _localScopes.Peek();
                if (scope.ContainsKey(name)) { scope[name] = val; return; }
            }
            _vars[name] = val;
        }

        public object GetVariable(string name)
        {
            if (!string.IsNullOrEmpty(name) && _readOnlyVars.TryGetValue(name, out var getter)) {
                return getter != null ? getter() : null;
            }
            if (_localScopes.Count > 0 && _localScopes.Peek().TryGetValue(name, out var local)) return local;
            return _vars.TryGetValue(name, out var v) ? v : null;
        }
    }

    public abstract class ExecutionBlock
    {
        public ExecutionBlock Next { get; set; }
        public ExecutionBlock Body { get; set; }

        // Debug contínuo: preenchido uma vez na compilação (nome + parâmetros do bloco).
        public string debugNomeBloco;
        public List<DebugParamInfo> debugParams;

        // Blocos que já mostram o próprio debug (Registro) não recebem a mensagem automática.
        public virtual bool EmiteDebugProprio => false;

        // Progresso em ticks do bloco: (atual, total). total <= 0 = bloco de 1 tick (sem x/y).
        public virtual (int atual, int total) GetDebugTickProgress(ExecutionContext context) => (0, 0);

        public abstract ExecutionBlock Execute(ExecutionContext context);
    }

    public class IfBlock : ExecutionBlock
    {
        private readonly IValueProvider _condition;
        public ExecutionBlock ElseBody { get; set; }

        public IfBlock(IValueProvider condition) => _condition = condition;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            bool result = (bool)_condition.GetValue(context);
            return result ? Body : (ElseBody ?? Next);
        }
    }

    public class WhileBlock : ExecutionBlock
    {
        private readonly IValueProvider _condition;

        public WhileBlock(IValueProvider condition) => _condition = condition;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            bool result = (bool)_condition.GetValue(context);

            if (result && Body != null)
            {
                // REDE DE SEGURANÇA: Espera o próximo frame antes de reavaliar.
                // Isso impede que loops mal estruturados travem a Thread principal da Unity.
                /* await Task.Yield();  */
                // ESPERAR UM FRAME, DA PRA SUBSTITUIR POR COROUTINE
                return Body;
            }
            
            return Next;
        }
    }

    public class WalkBlock : ExecutionBlock
    {
        private readonly IValueProvider _distProvider;
        private readonly IValueProvider _dirProvider;

        public WalkBlock(IValueProvider dist, IValueProvider dir) { _distProvider = dist; _dirProvider = dir; }

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            if (context == null || context.robotScript == null) {
                Debug.LogWarning("[Execução] Bloco Walk executado sem robô no contexto.");
                return Next;
            }

            RobotMovement robotMovement = context.robotScript.robotMovement;
            if (robotMovement == null) return Next;

            if (!TryGetDirection(context, out Vector2Int dir)) return Next;
            if (_distProvider == null) {
                Debug.LogWarning("[Execução] Bloco Walk sem parâmetro de distância.");
                return Next;
            }

            int dist = (int)_distProvider.GetValue(context); // tem que ser inteiro
            WalkState state = context.GetWalkState(this);

            if (dist <= 0) {
                state.initialized = false;
                state.passosTotais = 0;
                state.passosDados = 0;
                return Next;
            }

            if (!state.initialized || !robotMovement.isMoving
                || state.direction != dir || robotMovement.direction != dir) {
                robotMovement.direction = dir;
                robotMovement.distanceToMove = dist;
                robotMovement.isMoving = true;
                state.initialized = true;
                state.direction = dir;
                state.passosTotais = dist;
                state.passosDados = 0;
            }

            bool finished = robotMovement.AdvanceOneStep();
            state.passosDados++;
            if (!finished) {
                return this;
            }

            state.initialized = false;
            robotMovement.isMoving = false;
            return Next;
        }

        // Debug contínuo: mostra o passo atual da caminhada (1 tentativa = 1 tick).
        public override (int atual, int total) GetDebugTickProgress(ExecutionContext context) {
            var state = context?.GetWalkState(this);
            if (state == null || state.passosTotais <= 0) return (0, 0);
            return (Mathf.Clamp(state.passosDados, 1, state.passosTotais), state.passosTotais);
        }

        private bool TryGetDirection(ExecutionContext context, out Vector2Int direction) {
            direction = Vector2Int.zero;
            if (_dirProvider == null) {
                Debug.LogWarning("[Execução] Bloco Walk sem parâmetro de direção.");
                return false;
            }

            object value = _dirProvider.GetValue(context);
            if (value is Vector2Int vector) {
                direction = vector;
                return true;
            }

            if (value is string rawDirection) {
                object parsed = ValueParser.ParseRawString(rawDirection.Trim().ToLowerInvariant());
                if (parsed is Vector2Int parsedVector) {
                    direction = parsedVector;
                    return true;
                }
            }

            Debug.LogWarning("[Execução] Parâmetro de direção inválido no bloco Walk: "
                + (value != null ? value.ToString() : "null"));
            return false;
        }
    }

    // Bloco de debug (id "Log"): mostra o texto num balão do robô (RobotDebug/DebugContainer).
    // Aceita texto livre (input) ou variável — no caso de variável exibe "nome: valor".
    public class DebugBlock : ExecutionBlock
    {
        private readonly IValueProvider _valueProvider;
        private readonly string _varName; // null = texto livre

        public DebugBlock(IValueProvider valueProvider, string varName = null)
        {
            _valueProvider = valueProvider;
            _varName = varName;
        }

        // O bloco já mostra o texto do player: o debug contínuo não adiciona mensagem.
        public override bool EmiteDebugProprio => true;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            string texto = _varName != null
                ? $"{_varName}: {DebugContinuo.FormatarValor(_valueProvider?.GetValue(context))}"
                : _valueProvider?.GetValue(context) as string;

            // Texto vazio: o DebugSpawner já retorna sem instanciar nada.
            context?.robotScript?.GetRobotDebug()?.DebugSpawner(texto);
            return Next;
        }
    }

    // Bloco Utilitário adicionado para modificar variáveis dentro do fluxo
    public class SetVarBlock : ExecutionBlock
    {
        private readonly string _varName;
        private readonly IValueProvider _valueProvider;

        public SetVarBlock(string varName, IValueProvider valueProvider)
        {
            _varName = varName;
            _valueProvider = valueProvider;
        }

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            context.SetVariable(_varName, _valueProvider.GetValue(context));
            return Next;
        }
    }

    // Emite um signal: dispara os contextos que escutam esse signal (setando seus blocos de escuta como current)
    // e continua a própria sequência.
    public class SignalEmitBlock : ExecutionBlock
    {
        public readonly string signalName;
        public SignalEmitBlock(string signalName) => this.signalName = signalName;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            MapContext originContext = null;
            if (context != null && context.robotScript != null && context.robotScript.robotMovement != null)
            {
                originContext = context.robotScript.robotMovement.mapContext;
            }

            ExecutionManager.instancia.TriggerSignal(signalName, originContext);
            return Next;
        }
    }

    // Escuta um signal: só roda quando algum emissor o define como current.
    // Ao rodar, simplesmente segue para a própria sequência (Next pode ser null: escutar e não fazer nada é válido).
    public class SignalListenBlock : ExecutionBlock
    {
        public readonly string signalName;
        public SignalListenBlock(string signalName) => this.signalName = signalName;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            return Next;
        }
    }

    // Bloco de import: o trabalho real acontece na compilação (resolver a entidade
    // via MapManager, marcar os bools dos signals e coletar as variáveis expostas;
    // no caso de script, o ScriptManager copia as funções no script que importa).
    // Em runtime ele só segue a sequência: a referência da entidade já está no ExecutionContext.
    public class ImportBlock : ExecutionBlock
    {
        public readonly string section;
        public readonly string entityName;
        public readonly int entityInstanceId;

        public ImportBlock(string section, string entityName, int entityInstanceId)
        {
            this.section = section;
            this.entityName = entityName;
            this.entityInstanceId = entityInstanceId;
        }

        public override ExecutionBlock Execute(ExecutionContext context) => Next;
    }

    // Definição compilada de uma função: nomes dos parâmetros (ordem posicional dos argumentos)
    // e variáveis locais (nome + valor default). Compartilhada (read-only) entre todos os contextos.
    public class FunctionDefinition
    {
        public int bodyBlockId;                // id do bloco inicial do slot body do DeclareFunction
        public List<string> parameterNames = new List<string>();
        public List<VariableData> localVars = new List<VariableData>();
    }

    // Declara uma função: bloco de início de linha com sequência Next padrão.
    // O slot Body guarda a cadeia da função (ligada pelo compilador a partir do dado salvo);
    // em runtime a cadeia do declare não roda por aqui — ela só é copiada para cada CallFunction.
    public class DeclareFunctionBlock : ExecutionBlock
    {
        public readonly string functionName;
        public DeclareFunctionBlock(string functionName) => this.functionName = functionName;

        public override ExecutionBlock Execute(ExecutionContext context) => Next;
    }

    // Chama uma função declarada: empilha o escopo local (inicializando locais com o default e
    // recebendo os argumentos da chamada por posição), retorna o Body (cópia isolada do corpo,
    // uma por call site) pra ser executado como current; o sentinel FunctionScopeEndBlock no fim
    // do corpo desempilha o escopo e segue pro Next deste bloco (costura feita pelo compilador).
    // Body nulo = função inexistente (ou recursão auto-cancelada na compilação): loga debug e segue o Next.
    public class CallFunctionBlock : ExecutionBlock
    {
        public readonly string functionName;
        public readonly FunctionDefinition definition;
        private readonly List<IValueProvider> _arguments;

        public CallFunctionBlock(string functionName, FunctionDefinition definition, List<IValueProvider> arguments)
        {
            this.functionName = functionName;
            this.definition = definition;
            _arguments = arguments;
        }

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            if (Body == null)
            {
                Debug.Log($"[DEBUG] CallFunction: função '{functionName}' não encontrada (ou sem corpo) — chamada ignorada.");
                return Next;
            }

            context.PushLocalScope(BuildScope(context));
            return Body;
        }

        // Setup inicial do escopo: locais com o valor default; depois os parâmetros recebem os
        // argumentos da chamada (ordem posicional). A partir daqui são um único dicionário.
        private Dictionary<string, object> BuildScope(ExecutionContext context)
        {
            var scope = new Dictionary<string, object>();

            foreach (var local in definition.localVars)
                scope[local.varName] = ValueParser.ParseRawString(local.defaultValue);

            for (int i = 0; i < definition.parameterNames.Count; i++)
            {
                object arg = null;
                if (i < _arguments.Count && _arguments[i] != null)
                    arg = _arguments[i].GetValue(context);
                else
                    Debug.Log($"[DEBUG] CallFunction: argumento faltando para o parâmetro '{definition.parameterNames[i]}' na chamada de '{functionName}'.");

                scope[definition.parameterNames[i]] = arg;
            }

            if (_arguments.Count > definition.parameterNames.Count)
                Debug.Log($"[DEBUG] CallFunction: chamada de '{functionName}' tem argumentos extras ({_arguments.Count} > {definition.parameterNames.Count}) — ignorados.");

            return scope;
        }
    }

    // Sentinel colocado pelo compilador no fim da cópia do corpo de uma função:
    // desempilha o escopo local da chamada e segue pro Next do CallFunction (pode ser null).
    public class FunctionScopeEndBlock : ExecutionBlock
    {
        public override ExecutionBlock Execute(ExecutionContext context)
        {
            context.PopLocalScope();
            return Next;
        }
    }

    // ------------------------------------------------------------------
    // Blocos de inventário (RetirarItem / GuardarItem / ExtrairMaterial)
    // ------------------------------------------------------------------

    // Base dos blocos que agem no inventário do robô (ator) e num container do mapa.
    // O robô é o ator: tem inventário próprio e uma posição no mapa (célula do grid).
    public abstract class InventoryActionBlock : ExecutionBlock
    {
        // Inventário do robô (ator).
        protected bool TryGetActorInventory(ExecutionContext context, out InventoryManager actorInventory) {
            actorInventory = null;

            if (context == null || context.robotScript == null) {
                Debug.LogWarning("[Inventário] Bloco executado sem robô no contexto.");
                return false;
            }

            var robotInventory = context.robotScript.robotInventory;
            if (robotInventory == null || robotInventory.inventoryManager == null) {
                Debug.LogWarning("[Inventário] Robô sem inventário configurado.");
                return false;
            }

            actorInventory = robotInventory.inventoryManager;
            return true;
        }

        // Parâmetro de direção (Vector2Int).
        protected bool TryGetDirection(ExecutionContext context, IValueProvider provider, out Vector2Int direction) {
            direction = Vector2Int.zero;
            if (provider == null) return false;

            object value = provider.GetValue(context);
            if (value is Vector2Int v) {
                direction = v;
                return true;
            }

            Debug.LogWarning("[Inventário] Parâmetro de direção inválido: " + (value != null ? value.ToString() : "null"));
            return false;
        }

        // Parâmetro de quantidade (int).
        protected bool TryGetAmount(ExecutionContext context, IValueProvider provider, out int amount) {
            amount = 0;
            if (provider == null) return false;

            object value = provider.GetValue(context);
            if (value is int i) {
                amount = i;
                return true;
            }
            if (value is float f) {
                amount = (int)f;
                return true;
            }

            Debug.LogWarning("[Inventário] Parâmetro de quantidade inválido: " + (value != null ? value.ToString() : "null"));
            return false;
        }

        // Parâmetro de material (id do MaterialDatabase).
        // Padrão do projeto: o SectionDatabase guarda o id (string numérica).
        protected bool TryGetMaterialTypeId(ExecutionContext context, IValueProvider provider, out int typeId) {
            typeId = -1;
            if (provider == null) return false;

            object value = provider.GetValue(context);

            if (value is int i) {
                typeId = i;
                return true;
            }

            if (value is string s) {
                if (int.TryParse(s, out int parsed)) {
                    typeId = parsed;
                    return true;
                }

                // fallback: o parâmetro guardou o nome do material
                var executionManager = ExecutionManager.instancia;
                if (executionManager != null) {
                    typeId = executionManager.GetMaterialTypeId(s);
                    if (typeId >= 0) return true;
                }
            }

            Debug.LogWarning("[Inventário] Parâmetro de material inválido: " + (value != null ? value.ToString() : "null"));
            return false;
        }

        // Parâmetro de ferramenta (id ou nome do ToolDatabase).
        protected bool TryGetToolTypeId(ExecutionContext context, IValueProvider provider, out int typeId) {
            typeId = -1;
            if (provider == null) return false;
            object value = provider.GetValue(context);
            if (value is int i) { typeId = i; return true; }
            if (value is string s) {
                var executionManager = ExecutionManager.instancia;
                if (executionManager != null) {
                    typeId = executionManager.GetToolTypeId(s);
                    if (typeId >= 0) return true;
                }
            }
            return false;
        }

        // Célula atual do robô (ator do bloco).
        protected bool TryGetActorCell(ExecutionContext context, out Vector3Int cell) {
            cell = Vector3Int.zero;

            var robot = context != null ? context.robotScript : null;
            if (robot == null || robot.robotMovement == null) {
                Debug.LogWarning("[Inventário] Robô sem movimento (célula desconhecida).");
                return false;
            }

            cell = robot.robotMovement.currentCell;
            return true;
        }

        // Célula alvo: a célula do robô + a direção informada.
        protected bool TryGetTargetCell(ExecutionContext context, Vector2Int direction, out Vector3Int cell) {
            cell = Vector3Int.zero;
            if (!TryGetActorCell(context, out var origin)) return false;

            cell = origin + new Vector3Int(direction.x, direction.y, 0);
            return true;
        }

        // Contexto de mapa do ator, usado para separar objetivos do mapa principal
        // e do puzzle ativo nos eventos de inventário/extração.
        protected MapContext GetActorMapContext(ExecutionContext context) {
            if (context != null && context.robotScript != null && context.robotScript.robotMovement != null) {
                MapContext robotContext = context.robotScript.robotMovement.mapContext;
                if (robotContext != null) return robotContext;
            }

            return MapManager.instancia != null ? MapManager.instancia.MainContext : null;
        }

        // Container do tile vizinho (null se não houver nada com inventário ali).
        protected InventoryManager GetContainer(Vector3Int cell, string blockName) {
            var mapManager = MapManager.instancia;
            if (mapManager == null) {
                Debug.LogWarning("[" + blockName + "] MapManager não encontrado na cena.");
                return null;
            }

            // Tile sem container é condição normal do jogo (ex.: guardar onde não há
            // armazém): usa a variante silenciosa para não poluir o console com erros.
            if (!mapManager.TryGetTileInventory(cell, out var container)) {
                Debug.LogWarning("[" + blockName + "] Não há container na célula " + cell + ".");
                return null;
            }

            return container;
        }
    }

    // RetirarItem: tira do container do tile vizinho e põe no inventário do robô.
    public class TakeItemBlock : InventoryActionBlock
    {
        private readonly IValueProvider _dirProvider;
        private readonly IValueProvider _materialProvider;
        private readonly IValueProvider _amountProvider;
        private readonly IValueProvider _toolProvider;

        public TakeItemBlock(IValueProvider dir, IValueProvider material, IValueProvider amount, IValueProvider tool) {
            _dirProvider = dir;
            _materialProvider = material;
            _amountProvider = amount;
            _toolProvider = tool;
        }

        public override ExecutionBlock Execute(ExecutionContext context) {
            if (!TryGetActorInventory(context, out var actorInventory)) return Next;
            if (!TryGetDirection(context, _dirProvider, out var direction)) return Next;

            // Valor bruto do parâmetro de ferramenta (pode ser "Nenhum", "Qualquer" ou nome).
            string toolRaw = null;
            if (_toolProvider != null) {
                var tv = _toolProvider.GetValue(context);
                toolRaw = tv != null ? tv.ToString() : null;
            }

            bool isTool = false;
            int typeId = -1;
            if (!string.IsNullOrEmpty(toolRaw) && !string.Equals(toolRaw, "Nenhum", StringComparison.OrdinalIgnoreCase)) {
                isTool = true;
                if (string.Equals(toolRaw, "Qualquer", StringComparison.OrdinalIgnoreCase)) {
                    typeId = -1; // any: primeira compatível na ordem de índice
                } else {
                    if (!TryGetToolTypeId(context, _toolProvider, out typeId)) {
                        Debug.LogWarning("[RetirarItem] Ferramenta inválida: " + toolRaw);
                        return Next;
                    }
                }
            } else {
                if (!TryGetMaterialTypeId(context, _materialProvider, out typeId)) {
                    if (TryGetToolTypeId(context, _materialProvider, out typeId)) {
                        isTool = true;
                    } else {
                        return Next;
                    }
                }
            }

            int amount = 0;
            if (!isTool) {
                if (!TryGetAmount(context, _amountProvider, out amount) || amount <= 0) return Next;
            } else {
                // Quantidade é ignorada para ferramenta (retira a ferramenta inteira).
                if (!TryGetAmount(context, _amountProvider, out amount)) amount = 1;
                if (amount <= 0) amount = 1;
            }

            if (!TryGetTargetCell(context, direction, out var cell)) return Next;

            var container = GetContainer(cell, "RetirarItem");
            if (container == null) return Next;

            // Ferramenta: retira do slot Tool do container e equipa no robô.
            if (isTool) {
                int toolSlotIndex = -1;
                for (int k = 0; k < container.slotList.Count; k++) {
                    var s = container.slotList[k];
                    if (s.role != SlotRole.Tool) continue;
                    if (s.currentAmount <= 0) continue;
                    if (typeId >= 0 && s.currentTypeId != typeId) continue;
                    toolSlotIndex = k;
                    break; // primeira compatível na ordem de índice
                }
                if (toolSlotIndex < 0) {
                    Debug.LogWarning("[RetirarItem] Ferramenta não encontrada no container.");
                    return Next;
                }
                int takenId = container.slotList[toolSlotIndex].currentTypeId;
                bool ok = false;
                var robot = context != null ? context.robotScript : null;
                if (robot != null && robot.robotInventory != null) {
                    ok = robot.robotInventory.TakeToolFrom(container, toolSlotIndex);
                } else {
                    ok = container.slotList[toolSlotIndex].currentAmount > 0;
                }
                if (!ok) {
                    Debug.LogWarning("[RetirarItem] Não conseguiu retirar a ferramenta da célula " + cell + ".");
                } else {
                    ObjectiveSignals.TriggerItemTaken(takenId, 1, GetActorMapContext(context));
                }
                return Next;
            }

            // Transação: tira do container e põe no robô. Se falhar, desfaz tudo.
            container.BackupSlots();
            actorInventory.BackupSlots();

            if (container.SubtractItems(amount, typeId) && actorInventory.AddItems(amount, typeId)) {
                ObjectiveSignals.TriggerItemTaken(typeId, amount, GetActorMapContext(context));
                return Next;
            }

            actorInventory.RollBackSlots();
            container.RollBackSlots();
            Debug.LogWarning("[RetirarItem] Não conseguiu retirar " + amount + " x material " + typeId + " da célula " + cell + ".");
            return Next;
        }
    }

    // GuardarItem: tira do inventário do robô e põe no container do tile vizinho.
    public class StoreItemBlock : InventoryActionBlock
    {
        private readonly IValueProvider _dirProvider;
        private readonly IValueProvider _materialProvider;
        private readonly IValueProvider _amountProvider;
        private readonly IValueProvider _toolProvider;

        public StoreItemBlock(IValueProvider dir, IValueProvider material, IValueProvider amount, IValueProvider tool) {
            _dirProvider = dir;
            _materialProvider = material;
            _amountProvider = amount;
            _toolProvider = tool;
        }

        public override ExecutionBlock Execute(ExecutionContext context) {
            if (!TryGetActorInventory(context, out var actorInventory)) return Next;
            if (!TryGetDirection(context, _dirProvider, out var direction)) return Next;

            string toolRaw = null;
            if (_toolProvider != null) {
                var tv = _toolProvider.GetValue(context);
                toolRaw = tv != null ? tv.ToString() : null;
            }

            bool isTool = !string.IsNullOrEmpty(toolRaw) && !string.Equals(toolRaw, "Nenhum", StringComparison.OrdinalIgnoreCase);

            int amount = 0;
            int typeId = -1;
            if (!isTool) {
                if (!TryGetMaterialTypeId(context, _materialProvider, out typeId)) return Next;
                if (!TryGetAmount(context, _amountProvider, out amount) || amount <= 0) return Next;
            }

            if (!TryGetTargetCell(context, direction, out var cell)) return Next;

            var container = GetContainer(cell, "GuardarItem");
            if (container == null) return Next;

            if (isTool) {
                bool isAny = string.Equals(toolRaw, "Qualquer", StringComparison.OrdinalIgnoreCase);
                int toolId = -1;
                if (!isAny) {
                    if (!TryGetToolTypeId(context, _toolProvider, out toolId)) {
                        Debug.LogWarning("[GuardarItem] Ferramenta inválida: " + toolRaw);
                        return Next;
                    }
                }

                var robot = context != null ? context.robotScript : null;
                if (robot == null || robot.robotInventory == null) {
                    Debug.LogWarning("[GuardarItem] Robô sem inventário.");
                    return Next;
                }
                int equippedId = robot.robotInventory.GetToolId();
                if (equippedId < 0) {
                    Debug.LogWarning("[GuardarItem] Robô sem ferramenta equipada.");
                    return Next;
                }
                int storeId = isAny ? equippedId : toolId;
                if (!isAny && equippedId != storeId) {
                    Debug.LogWarning("[GuardarItem] Robô não tem a ferramenta selecionada equipada.");
                    return Next;
                }

                int uses = robot.robotInventory.GetToolUses();
                int max = 0;
                var robotSlot = robot.robotInventory.GetToolSlot();
                if (robotSlot != null) max = robotSlot.maxAmount;
                if (max <= 0) max = uses;

                int targetSlot = -1;
                for (int k = 0; k < container.slotList.Count; k++) {
                    var s = container.slotList[k];
                    if (s.role != SlotRole.Tool) continue;
                    if (s.currentAmount > 0 && s.currentTypeId != storeId) continue;
                    targetSlot = k;
                    break;
                }
                if (targetSlot < 0) {
                    Debug.LogWarning("[GuardarItem] Container sem slot de ferramenta.");
                    return Next;
                }

                container.BackupSlots();
                robot.robotInventory.inventoryManager.BackupSlots();
                bool ok = container.TryAddTool(storeId, uses, max);
                if (!ok) {
                    container.RollBackSlots();
                    robot.robotInventory.inventoryManager.RollBackSlots();
                    Debug.LogWarning("[GuardarItem] Não coube a ferramenta no container.");
                    return Next;
                }
                robotSlot.currentTypeId = -1;
                robotSlot.currentAmount = 0;
                robotSlot.hasTypeLocked = false;
                container.NotifyInventoryChanged();
                robot.robotInventory.inventoryManager.NotifyInventoryChanged();
                ObjectiveSignals.TriggerItemStored(storeId, 1, GetActorMapContext(context));
                return Next;
            }

            // Transação: tira do robô e põe no container. Se falhar, desfaz tudo.
            actorInventory.BackupSlots();
            container.BackupSlots();

            if (actorInventory.SubtractItems(amount, typeId) && container.AddItems(amount, typeId)) {
                ObjectiveSignals.TriggerItemStored(typeId, amount, GetActorMapContext(context));
                return Next;
            }

            container.RollBackSlots();
            actorInventory.RollBackSlots();
            Debug.LogWarning("[GuardarItem] Não conseguiu guardar " + amount + " x material " + typeId + " na célula " + cell + ".");
            return Next;
        }
    }

    // ExtrairMaterial: usa a ferramenta equipada no robô para extrair o recurso do tile vizinho.
    // A quantidade é em material: a ferramenta decide quantos usos e quantos ticks isso custa.
    public class ExtractMaterialBlock : InventoryActionBlock
    {
        private readonly IValueProvider _dirProvider;
        private readonly IValueProvider _amountProvider;

        public ExtractMaterialBlock(IValueProvider dir, IValueProvider amount) {
            _dirProvider = dir;
            _amountProvider = amount;
        }

        public override ExecutionBlock Execute(ExecutionContext context) {
            if (!TryGetActorInventory(context, out var actorInventory)) return Next;
            if (!TryGetDirection(context, _dirProvider, out var direction)) return Next;
            if (!TryGetAmount(context, _amountProvider, out int amount) || amount <= 0) return Next;
            if (!TryGetActorCell(context, out var robotCell)) return Next;

            var mapManager = MapManager.instancia;
            if (mapManager == null) {
                Debug.LogWarning("[ExtrairMaterial] MapManager não encontrado na cena.");
                return Next;
            }

            // Recurso do tile vizinho: o MapManager resolve a célula pela direção
            // (tile -> IMapEntity -> IExtractable) e a extração fica no próprio recurso.
            var extractable = mapManager.GetAdjacentExtractable(robotCell, direction);
            if (extractable == null) {
                Debug.LogWarning("[ExtrairMaterial] Não há recurso extraível na célula "
                    + (robotCell + new Vector3Int(direction.x, direction.y, 0)) + ".");
                return Next;
            }

            // Ferramenta equipada
            var toolSlot = actorInventory.GetSlotByRole(SlotRole.Tool);
            if (toolSlot == null || toolSlot.currentTypeId < 0 || toolSlot.currentAmount <= 0) {
                Debug.LogWarning("[ExtrairMaterial] O robô não tem ferramenta equipada.");
                return Next;
            }

            var executionManager = ExecutionManager.instancia;
            var toolData = executionManager != null ? executionManager.GetTool(toolSlot.currentTypeId) : null;
            if (toolData == null) {
                Debug.LogWarning("[ExtrairMaterial] Ferramenta desconhecida (id " + toolSlot.currentTypeId + ").");
                return Next;
            }

            if (toolData.acceptedResourceTypes == null || !toolData.acceptedResourceTypes.Contains(extractable.ResourceType)) {
                var acceptedTypes = toolData.acceptedResourceTypes ?? new List<ResourceType>();
                Debug.LogWarning("[ExtrairMaterial] '" + toolData.displayName + "' não extrai este recurso (tipos aceitos: "
                    + string.Join(", ", acceptedTypes) + ").");
                return Next;
            }

            // Custo: quantos usos da ferramenta a quantidade pedida exige.
            int yieldPerUse = Mathf.Max(1, toolData.yieldPerUse);
            int uses = Mathf.CeilToInt(amount / (float)yieldPerUse);

            if (uses > toolSlot.currentAmount) {
                Debug.LogWarning("[ExtrairMaterial] Ferramenta sem usos suficientes (" + toolSlot.currentAmount
                    + " usos, precisaria de " + uses + ").");
                return Next;
            }

            // Transação: consome os usos da ferramenta e extrai o material.
            // Se a extração falhar, devolve os usos.
            int usesBefore = toolSlot.currentAmount;
            int toolIdBefore = toolSlot.currentTypeId;

            for (int i = 0; i < uses; i++) {
                actorInventory.UseTool();
            }

            int extracted = extractable.ExtractTo(actorInventory, amount);
            if (extracted <= 0) {
                if (toolSlot != null) {
                    toolSlot.currentAmount = usesBefore;
                    toolSlot.currentTypeId = toolIdBefore;
                    actorInventory.NotifyInventoryChanged();
                }
                Debug.LogWarning("[ExtrairMaterial] Falhou ao extrair (inventário do robô cheio?).");
                return Next;
            }

            if (extracted < amount) {
                // Reembolsa os usos da ferramenta que não foram efetivamente necessários.
                int extractedUses = Mathf.CeilToInt(extracted / (float)Mathf.Max(1, toolData.yieldPerUse));
                int refund = uses - extractedUses;
                if (refund > 0 && toolSlot != null) {
                    if (toolSlot.currentTypeId < 0) toolSlot.currentTypeId = toolIdBefore;
                    int cap = toolSlot.maxAmount > 0 ? toolSlot.maxAmount : (toolSlot.currentAmount + refund);
                    toolSlot.currentAmount = Mathf.Min(toolSlot.currentAmount + refund, cap);
                    actorInventory.NotifyInventoryChanged();
                }
                Debug.LogWarning("[ExtrairMaterial] O recurso só tinha " + extracted + " de " + amount + ".");
            }

            ObjectiveSignals.TriggerResourceExtracted(
                extractable.MaterialTypeId, extracted, GetActorMapContext(context));

            // Tempo de trabalho: segura o contexto por N ticks (mesmo mecanismo do WalkBlock).
            context.ticksOcupado = uses * Mathf.Max(1, toolData.ticksPerUse);
            context.tickActionBlock = this;
            context.tickActionTotal = context.ticksOcupado;
            return Next;
        }

        // Debug contínuo: progresso do trabalho (execução = 1/N; ticks ocupados = 2/N..N/N).
        public override (int atual, int total) GetDebugTickProgress(ExecutionContext context) {
            if (context == null || context.tickActionBlock != this || context.tickActionTotal <= 0) return (0, 0);
            int atual = context.tickActionTotal - context.ticksOcupado + 1;
            return (Mathf.Clamp(atual, 1, context.tickActionTotal), context.tickActionTotal);
        }
    }

    public class BlockCompiler
    {
        private BlockNode[] _blockCache;
        private ParameterNode[] _paramCache;

        // Memoização: 1 BlockNode.id -> 1 instância de ExecutionBlock (entre todos os roots).
        private Dictionary<int, ExecutionBlock> _instanceCache;
        // Ids atualmente na pilha de linkagem do 'next' (detecção de ciclo).
        private HashSet<int> _linking;

        // signal -> blocos de escuta compilados (preenchido durante a compilação).
        public Dictionary<string, List<ExecutionBlock>> signalStarts = new Dictionary<string, List<ExecutionBlock>>();
        // signals que possuem pelo menos um bloco de emissão (chave sem bloco de escuta).
        private readonly HashSet<string> emittedSignals = new HashSet<string>();
        
        // Imports de entidades coletados durante compilação
        public List<EntitySignalImport> entitySignalImports = new List<EntitySignalImport>();
        public List<EntityVariableImport> entityVariableImports = new List<EntityVariableImport>();
        public List<ResolvedEntityImport> resolvedEntityImports = new List<ResolvedEntityImport>();

        // nome -> definição compilada do DeclareFunction (preenchido no Prepare: completo antes de
        // qualquer Compile, então a ordem dos roots no save não importa pro bind do CallFunction).
        private Dictionary<string, FunctionDefinition> _functionDeclarations;

        // Monta os caches uma vez por ScriptData; depois Compile() pode ser chamado por root.
        public void Prepare(SaveNodesData data)
        {
            int maxBlockId = data.blocosData.Count > 0 ? data.blocosData.Max(b => b.id) : -1;
            int maxParamId = data.parametrosData.Count > 0 ? data.parametrosData.Max(p => p.id) : -1;

            _blockCache = new BlockNode[maxBlockId + 1];
            _paramCache = new ParameterNode[maxParamId + 1];

            foreach (var b in data.blocosData) _blockCache[b.id] = b;
            foreach (var p in data.parametrosData) _paramCache[p.id] = p;

            _instanceCache = new Dictionary<int, ExecutionBlock>();
            _linking = new HashSet<int>();
            entitySignalImports.Clear();
            entityVariableImports.Clear();
            if (resolvedEntityImports == null) resolvedEntityImports = new List<ResolvedEntityImport>();
            resolvedEntityImports.Clear();

            RegisterFunctionDeclarations(data);
            ValidateSlotTargets();
        }

        // Passa única pelos blocos registrando nome -> FunctionDefinition.
        // Nome duplicado impede o compile (um script só tem uma instância de uma dada função).
        private void RegisterFunctionDeclarations(SaveNodesData data)
        {
            _functionDeclarations = new Dictionary<string, FunctionDefinition>();

            foreach (var b in data.blocosData)
            {
                if (b.functionId != "DeclareFunction") continue;

                string nome = ResolveRawParamName(b, 0, "função");
                if (_functionDeclarations.ContainsKey(nome))
                    throw new Exception($"Função '{nome}' declarada mais de uma vez (bloco {b.id}).");

                var definition = new FunctionDefinition { bodyBlockId = b.body };

                // Parâmetros e variáveis locais compartilham a mesma lista de nomes:
                // não pode repetir nome entre eles.
                var nomes = new HashSet<string>();

                if (b.paramIds != null)
                {
                    for (int i = 1; i < b.paramIds.Count; i++)
                    {
                        string paramName = ResolveFunctionParamName(b, i, nome);
                        if (!nomes.Add(paramName))
                            throw new Exception($"Nome '{paramName}' duplicado entre parâmetros e variáveis locais da função '{nome}' (bloco {b.id}).");

                        definition.parameterNames.Add(paramName);
                    }
                }

                definition.localVars = CollectFunctionLocalVars(data, b.id, nome, nomes);

                _functionDeclarations[nome] = definition;
            }
        }

        // Nome (texto cru, via varName) de um parâmetro do declare: o valor fica por conta da chamada.
        private string ResolveFunctionParamName(BlockNode declareNode, int paramIndex, string functionName)
        {
            int paramId = declareNode.paramIds[paramIndex];
            if (paramId < 0 || paramId >= _paramCache.Length || _paramCache[paramId] == null || string.IsNullOrEmpty(_paramCache[paramId].varName))
                throw new Exception($"Parâmetro {paramIndex} da função '{functionName}' (bloco {declareNode.id}) não tem nome.");

            return _paramCache[paramId].varName;
        }

        // Variáveis locais salvas (functionLocals) do declare com esse id, validadas contra a
        // mesma lista de nomes usada pelos parâmetros.
        private List<VariableData> CollectFunctionLocalVars(SaveNodesData data, int functionBlockId, string functionName, HashSet<string> nomes)
        {
            var locals = new List<VariableData>();
            if (data.functionLocals == null) return locals;

            foreach (var f in data.functionLocals)
            {
                if (f == null || f.functionBlockId != functionBlockId || f.vars == null) continue;

                foreach (var v in f.vars)
                {
                    if (string.IsNullOrEmpty(v.varName))
                        throw new Exception($"Variável local sem nome na função '{functionName}' (bloco {functionBlockId}).");

                    if (!nomes.Add(v.varName))
                        throw new Exception($"Nome '{v.varName}' duplicado entre parâmetros e variáveis locais da função '{functionName}' (bloco {functionBlockId}).");

                    locals.Add(new VariableData { varName = v.varName, defaultValue = v.defaultValue });
                }
            }
            return locals;
        }

        public ExecutionBlock Compile(int startBlockId)
        {
            if (_blockCache == null)
                throw new Exception("BlockCompiler.Compile chamado antes de Prepare.");

            if (startBlockId < 0 || startBlockId >= _blockCache.Length || _blockCache[startBlockId] == null)
                throw new Exception($"ID inicial {startBlockId} não foi encontrado nos BlockNodes, de SaveNodesData, de tamanho {_blockCache.Length}.");

            return LinkBlock(_blockCache[startBlockId]);
        }

        // Impede que body/elseBlockId/next do mesmo bloco apontem para o mesmo id
        // (evitaria uma sequência ambígua compartilhada entre dois slots).
        private void ValidateSlotTargets()
        {
            foreach (var blockNode in _blockCache)
            {
                if (blockNode == null) continue;

                int body = ValidId(blockNode.body) ? blockNode.body : -1;
                int elseId = blockNode.functionId == "If" && ValidId(blockNode.elseBlockId) ? blockNode.elseBlockId : -1;
                int next = ValidId(blockNode.next) ? blockNode.next : -1;

                if (body >= 0 && next >= 0 && body == next)
                    throw new Exception($"Bloco {blockNode.id} ({blockNode.functionId}): 'body' e 'next' apontam para o mesmo id ({body}).");

                if (elseId >= 0 && next >= 0 && elseId == next)
                    throw new Exception($"Bloco {blockNode.id} ({blockNode.functionId}): 'else' e 'next' apontam para o mesmo id ({elseId}).");

                if (body >= 0 && elseId >= 0 && body == elseId)
                    throw new Exception($"Bloco {blockNode.id} ({blockNode.functionId}): 'body' e 'else' apontam para o mesmo id ({body}).");
            }
        }

        private bool ValidId(int id) => id >= 0 && id < _blockCache.Length && _blockCache[id] != null;

        // Monta o dicionário final de signals: chaves de emissão e chaves de escuta (com seus blocos).
        public Dictionary<string, List<ExecutionBlock>> BuildSignalStarts()
        {
            foreach (var signal in emittedSignals)
            {
                if (!signalStarts.ContainsKey(signal)) signalStarts[signal] = new List<ExecutionBlock>();
            }
            return signalStarts;
        }

        // Dicionário final de funções declaradas (nome -> parâmetros + variáveis locais compilados).
        public Dictionary<string, FunctionDefinition> BuildFunctionDefinitions() => _functionDeclarations;
        
        public void RegisterEntitySignalImport(string entityName, string signalName, int entityInstanceId)
        {
            if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(signalName)) return;
            entitySignalImports.Add(new EntitySignalImport
            {
                entityName = entityName,
                entityIdString = entityInstanceId.ToString(),
                entityInstanceId = entityInstanceId,
                signalName = signalName
            });
        }
        
        public void RegisterEntityVariableImport(string entityName, string varName, string localName, int entityInstanceId, bool isScriptVariable = false)
        {
            if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(varName)) return;
            entityVariableImports.Add(new EntityVariableImport
            {
                entityName = entityName,
                entityIdString = entityInstanceId.ToString(),
                entityInstanceId = entityInstanceId,
                varName = varName,
                localName = string.IsNullOrEmpty(localName) ? varName : localName,
                isScriptVariable = isScriptVariable
            });
        }
        
        public void RegisterResolvedEntityImport(string section, string entityName, int entityInstanceId, List<string> signals, List<string> variables)
        {
            if (string.IsNullOrEmpty(entityName) && entityInstanceId == 0) return;
            if (resolvedEntityImports == null) resolvedEntityImports = new List<ResolvedEntityImport>();
            resolvedEntityImports.Add(new ResolvedEntityImport
            {
                section = section ?? "",
                entityName = entityName ?? "",
                entityInstanceId = entityInstanceId,
                entityIdString = entityInstanceId.ToString(),
                importedSignals = signals != null ? new List<string>(signals) : new List<string>(),
                importedVariables = variables != null ? new List<string>(variables) : new List<string>()
            });
        }

        private void RegisterEmit(string signal)
        {
            emittedSignals.Add(signal);
        }

        private void RegisterListen(string signal, ExecutionBlock listenBlock)
        {
            if (!signalStarts.TryGetValue(signal, out var list))
            {
                list = new List<ExecutionBlock>();
                signalStarts[signal] = list;
            }
            // Com memoização o mesmo bloco pode ser alcançado por mais de um root:
            // não registra a mesma instância duas vezes.
            if (!list.Contains(listenBlock)) list.Add(listenBlock);
        }

        // Texto cru de um parâmetro (nome de signal, nome de função): não passa pelo ValueParser
        // (que converteria "cima"/"verdadeiro" em Vector2Int/bool).
        private string ResolveRawParamName(BlockNode blockNode, int paramIndex, string kindLabel)
        {
            if (blockNode.paramIds == null || paramIndex < 0 || paramIndex >= blockNode.paramIds.Count)
                throw new Exception($"Bloco '{blockNode.functionId}' (id {blockNode.id}) não tem parâmetro de {kindLabel}.");

            int paramId = blockNode.paramIds[paramIndex];
            if (paramId < 0 || paramId >= _paramCache.Length || _paramCache[paramId] == null || string.IsNullOrEmpty(_paramCache[paramId].value))
                throw new Exception($"Nome de {kindLabel} inválido no bloco '{blockNode.functionId}' (id {blockNode.id}).");

            return _paramCache[paramId].value;
        }

        private string ResolveSignalName(BlockNode blockNode) => ResolveRawParamName(blockNode, 0, "signal");

        private ExecutionBlock LinkBlock(BlockNode blockNode)
        {
            if (blockNode == null) return null;

            // Memoização: um nó só é instanciado e costurado uma vez.
            if (_instanceCache.TryGetValue(blockNode.id, out var cached))
            {
                // Já está na pilha de linkagem => ciclo de 'next' (o cache devolveria a instância em construção).
                if (_linking.Contains(blockNode.id))
                    throw new Exception($"Ciclo detectado na cadeia 'next': o bloco {blockNode.id} já está sendo ligado.");
                return cached;
            }

            ExecutionBlock current = CreateBlockInstance(blockNode, null);

            // Guard de ciclo no encadeamento de 'next' (5.next=5 ou 5->6->5).
            _linking.Add(blockNode.id);
            _instanceCache[blockNode.id] = current;

            // O body do CallFunction vem do DeclareFunction (copiado por call site em CreateCallFunctionBlock),
            // nunca do slot body salvo no próprio call.
            if (blockNode.functionId != "CallFunction" && ValidId(blockNode.body))
                current.Body = LinkBlock(_blockCache[blockNode.body]);

            ExecutionBlock elseBody = null;
            if (blockNode.functionId == "If" && ValidId(blockNode.elseBlockId))
                elseBody = LinkBlock(_blockCache[blockNode.elseBlockId]);

            if (ValidId(blockNode.next))
                current.Next = LinkBlock(_blockCache[blockNode.next]);

            _linking.Remove(blockNode.id);

            // Regras de Costura
            if (current is WhileBlock)
            {
                if (current.Body != null)
                {
                    ExecutionBlock lastInBody = GetLastBlockInChain(current.Body);
                    lastInBody.Next = current; 
                }
            }
            else if (blockNode.functionId == "If")
            {
                var ifBlock = (IfBlock)current;
                if (elseBody != null) ifBlock.ElseBody = elseBody;

                if (current.Body != null && current.Next != null)
                {
                    GetLastBlockInChain(current.Body).Next = current.Next;
                }
                if (elseBody != null && current.Next != null)
                {
                    GetLastBlockInChain(elseBody).Next = current.Next;
                }
            }
            else if (current is CallFunctionBlock callBlock)
            {
                // Sentinel no fim da cópia do corpo: desempilha o escopo local e segue pro
                // caminho padrão do call. Anexado mesmo com Next null (senão o contexto
                // dormiria com o escopo preso na pilha).
                if (callBlock.Body != null)
                {
                    var scopeEnd = new FunctionScopeEndBlock();
                    GetLastBlockInChain(callBlock.Body).Next = scopeEnd;
                    scopeEnd.Next = callBlock.Next;
                }
            }

            return current;
        }

        private ExecutionBlock GetLastBlockInChain(ExecutionBlock block)
        {
            ExecutionBlock current = block;
            while (current.Next != null && current.Next != block) // Evita loop infinito na busca se já costurado
            {
                current = current.Next;
            }
            return current;
        }

        // Instancia a cadeia de blocos a partir de um id de forma ISOLADA (não usa nem alimenta o
        // _instanceCache compartilhado entre roots): cada CallFunction recebe a sua própria cópia do
        // corpo da função, pra costura de retorno (fim do corpo -> Next do call) não vazar entre call sites.
        // Seguro porque ExecutionBlock não guarda estado de execução (tudo vive no ExecutionContext).
        private ExecutionBlock CopyChainFrom(int startBlockId, HashSet<string> copyingFunctions)
        {
            if (!ValidId(startBlockId)) return null;
            return CopyChainNode(_blockCache[startBlockId], new Dictionary<int, ExecutionBlock>(), copyingFunctions);
        }

        private ExecutionBlock CopyChainNode(BlockNode blockNode, Dictionary<int, ExecutionBlock> copyMap, HashSet<string> copyingFunctions)
        {
            if (blockNode == null) return null;

            // Identidade dentro da cópia: preserva ciclos de 'next' e a costura do While.
            if (copyMap.TryGetValue(blockNode.id, out var cached)) return cached;

            ExecutionBlock current = CreateBlockInstance(blockNode, copyingFunctions);
            copyMap[blockNode.id] = current;

            if (blockNode.functionId != "CallFunction" && ValidId(blockNode.body))
                current.Body = CopyChainNode(_blockCache[blockNode.body], copyMap, copyingFunctions);

            ExecutionBlock elseBody = null;
            if (blockNode.functionId == "If" && ValidId(blockNode.elseBlockId))
                elseBody = CopyChainNode(_blockCache[blockNode.elseBlockId], copyMap, copyingFunctions);

            if (ValidId(blockNode.next))
                current.Next = CopyChainNode(_blockCache[blockNode.next], copyMap, copyingFunctions);

            // Mesmas regras de costura do LinkBlock, aplicadas localmente na cópia.
            if (current is WhileBlock)
            {
                if (current.Body != null)
                {
                    GetLastBlockInChain(current.Body).Next = current;
                }
            }
            else if (blockNode.functionId == "If")
            {
                var ifBlock = (IfBlock)current;
                if (elseBody != null) ifBlock.ElseBody = elseBody;

                if (current.Body != null && current.Next != null)
                    GetLastBlockInChain(current.Body).Next = current.Next;
                if (elseBody != null && current.Next != null)
                    GetLastBlockInChain(elseBody).Next = current.Next;
            }
            else if (current is CallFunctionBlock callBlock)
            {
                if (callBlock.Body != null)
                {
                    var scopeEnd = new FunctionScopeEndBlock();
                    GetLastBlockInChain(callBlock.Body).Next = scopeEnd;
                    scopeEnd.Next = callBlock.Next;
                }
            }

            return current;
        }

        private ExecutionBlock CreateBlockInstance(BlockNode blockNode, HashSet<string> copyingFunctions)
        {
            ExecutionBlock block = blockNode.functionId switch
            {
                "If" => new IfBlock(ResolveValue(blockNode.paramIds[0])),
                "While" => new WhileBlock(ResolveValue(blockNode.paramIds[0])),
                "Walk" => new WalkBlock(ResolveValue(blockNode.paramIds[0]), ResolveValue(blockNode.paramIds[1])),
                "Log" => CreateDebugBlock(blockNode),
                "SetVar" => new SetVarBlock(blockNode.variableNameReference, ResolveValue(blockNode.paramIds[0])),
                "EmitSignal" => CreateEmitBlock(blockNode),
                "ListenSignal" => CreateListenBlock(blockNode),
                "DeclareFunction" => new DeclareFunctionBlock(ResolveRawParamName(blockNode, 0, "função")),
                "CallFunction" => CreateCallFunctionBlock(blockNode, copyingFunctions ?? new HashSet<string>()),
                "TakeItem" => new TakeItemBlock(ResolveValueAt(blockNode, 0), ResolveValueAt(blockNode, 1), ResolveValueAt(blockNode, 2), ResolveValueAt(blockNode, 3)),
                "StoreItem" => new StoreItemBlock(ResolveValueAt(blockNode, 0), ResolveValueAt(blockNode, 1), ResolveValueAt(blockNode, 2), ResolveValueAt(blockNode, 3)),
                "ExtractMaterial" => new ExtractMaterialBlock(ResolveValueAt(blockNode, 0), ResolveValueAt(blockNode, 1)),
                "Import" => CreateImportBlock(blockNode),
                "ImportEntity" => CreateImportBlock(blockNode),
                //"Importar" => CreateImportBlock(blockNode),
                _ => throw new Exception($"Bloco desconhecido: {blockNode.functionId}")
            };

            AttachDebugInfo(block, blockNode);
            return block;
        }

        // Metadados do debug contínuo: nome do bloco (BlockData) e parâmetros (rótulo + provider).
        // Presos na compilação: o valor é lido no runtime, então variável pega o valor atual.
        private void AttachDebugInfo(ExecutionBlock block, BlockNode blockNode)
        {
            if (block == null || blockNode == null) return;

            BlockData data = BlocksFactory.instance != null && BlocksFactory.instance.blockRegistry != null
                ? BlocksFactory.instance.blockRegistry.GetBlockData(blockNode.functionId)
                : null;

            block.debugNomeBloco = data != null && !string.IsNullOrEmpty(data.blockName)
                ? data.blockName
                : blockNode.functionId;

            if (blockNode.paramIds == null || blockNode.paramIds.Count == 0) return;

            block.debugParams = new List<DebugParamInfo>();
            for (int i = 0; i < blockNode.paramIds.Count; i++)
            {
                int paramId = blockNode.paramIds[i];
                if (paramId < 0 || paramId >= _paramCache.Length || _paramCache[paramId] == null) continue;

                var paramNode = _paramCache[paramId];

                IValueProvider provider;
                try { provider = ResolveValue(paramId); }
                catch { provider = new LiteralValue(paramNode.value); }

                block.debugParams.Add(new DebugParamInfo
                {
                    rotulo = RotuloParametro(data, i, paramNode.type),
                    provider = provider
                });
            }
        }

        // Rótulo do parâmetro: nome específico do bloco (BlockData.rotulosParametros) ou o tipo.
        private static string RotuloParametro(BlockData data, int index, ParameterType type)
        {
            if (data != null && data.rotulosParametros != null
                && index >= 0 && index < data.rotulosParametros.Count
                && !string.IsNullOrEmpty(data.rotulosParametros[index]))
            {
                return data.rotulosParametros[index];
            }
            return ParameterSetup.ParameterTypeLabel(type);
        }

        // Valor do parâmetro na posição (null se o bloco não tem aquele parâmetro).
        private IValueProvider ResolveValueAt(BlockNode blockNode, int index) {
            if (blockNode.paramIds == null || index < 0 || index >= blockNode.paramIds.Count) return null;
            return ResolveValue(blockNode.paramIds[index]);
        }

        // Liga o call à função declarada: definição (parâmetros + locais) e argumentos (paramIds[1..])
        // resolvidos na compilação; o corpo (slot body do declare) é copiado de forma isolada pro call site.
        // Função inexistente deixa definition/Body null (loga debug e segue Next em runtime);
        // função já em cópia (recursão) também: o call interno se auto-cancela com o mesmo log.
        private ExecutionBlock CreateImportBlock(BlockNode blockNode)
        {
            // Bloco de import: param[0] = alvo (seção + id da instância/script),
            // param[1] = o que importar (seção + chave do elemento).
            ParameterSections targetSection = ParameterSections.Robos;
            string targetKey = "";
            ParameterSections elementSection = ParameterSections.Variaveis;
            string elementKey = ImportKeys.All;

            if (blockNode.paramIds != null && blockNode.paramIds.Count > 0) {
                var target = GetParamNode(blockNode.paramIds[0]);
                if (target != null) {
                    targetKey = target.value ?? "";
                    targetSection = target.selectedSection;
                }
            }
            if (blockNode.paramIds != null && blockNode.paramIds.Count > 1) {
                var element = GetParamNode(blockNode.paramIds[1]);
                if (element != null) {
                    elementKey = string.IsNullOrEmpty(element.value) ? ImportKeys.All : element.value;
                    elementSection = element.selectedSection;
                }
            }

            // Import de script: as funções são copiadas no script que importa pelo
            // ScriptManager (mesclagem dos nós). Aqui o bloco só segue a sequência.
            if (targetSection == ParameterSections.Scripts) {
                int scriptId = int.TryParse((targetKey ?? "").Trim(), out int parsedId) ? parsedId : 0;
                return new ImportBlock(MapManager.SectionScript, null, scriptId);
            }

            // Import de entidade: seção + instance id.
            string section = targetSection == ParameterSections.Construcoes
                ? MapManager.SectionConstruction
                : MapManager.SectionRobot;
            int entityInstanceId = int.TryParse((targetKey ?? "").Trim(), out int parsedInstance) ? parsedInstance : 0;
            string entityName = "";

            IMapEntity resolvedEntity = MapManager.instancia?.FindEntity(section, entityInstanceId, null);

            if (resolvedEntity != null)
            {
                entityInstanceId = resolvedEntity.GetEntityId();
                entityName = resolvedEntity.EntityName;

                var selectedSignals = new List<string>();
                var selectedInternalVars = new List<string>();
                var selectedScriptVars = new List<string>();

                // Só o que foi selecionado no parâmetro "o que importar" entra no import.
                if (elementSection == ParameterSections.Signals)
                {
                    if (elementKey == ImportKeys.All) {
                        var exposed = resolvedEntity.GetExposedSignals();
                        if (exposed != null) selectedSignals.AddRange(exposed);
                    } else if (elementKey.StartsWith(ImportKeys.SignalPrefix)) {
                        selectedSignals.Add(elementKey.Substring(ImportKeys.SignalPrefix.Length));
                    }
                }
                else if (elementSection == ParameterSections.Variaveis)
                {
                    if (elementKey == ImportKeys.All) {
                        var exposed = resolvedEntity.GetExposedVariables();
                        if (exposed != null) selectedInternalVars.AddRange(exposed);
                        var scriptVars = resolvedEntity.GetScriptVariableNames();
                        if (scriptVars != null) selectedScriptVars.AddRange(scriptVars);
                    } else if (elementKey.StartsWith(ImportKeys.InternalVarPrefix)) {
                        selectedInternalVars.Add(elementKey.Substring(ImportKeys.InternalVarPrefix.Length));
                    } else if (elementKey.StartsWith(ImportKeys.ScriptVarPrefix)) {
                        selectedScriptVars.Add(elementKey.Substring(ImportKeys.ScriptVarPrefix.Length));
                    }
                }

                RegisterResolvedEntityImport(section, entityName, entityInstanceId, selectedSignals, selectedInternalVars);
                foreach (var s in selectedSignals)
                {
                    if (string.IsNullOrEmpty(s)) continue;
                    // O bool do signal é marcado na compilação (ScriptManager.TryApplyEntityImports):
                    // marcar aqui dispararia a mesma borda duas vezes.
                    RegisterEntitySignalImport(entityName, s, entityInstanceId);
                }
                foreach (var v in selectedInternalVars)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    RegisterEntityVariableImport(entityName, v, v, entityInstanceId, false);
                }
                foreach (var v in selectedScriptVars)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    RegisterEntityVariableImport(entityName, v, v, entityInstanceId, true);
                }
            }
            else if (entityInstanceId != 0 || !string.IsNullOrEmpty(targetKey))
            {
                // Registra mesmo sem resolver agora (será resolvido em runtime)
                RegisterResolvedEntityImport(section, entityName, entityInstanceId, new List<string>(), new List<string>());
            }

            return new ImportBlock(section, entityName, entityInstanceId);
        }

        // Parâmetro salvo pelo id (cache do Prepare).
        private ParameterNode GetParamNode(int paramId)
        {
            if (paramId < 0 || paramId >= _paramCache.Length) return null;
            return _paramCache[paramId];
        }
        
        private ExecutionBlock CreateCallFunctionBlock(BlockNode blockNode, HashSet<string> copyingFunctions)
        {
            string nome = ResolveRawParamName(blockNode, 0, "função");

            if (!_functionDeclarations.TryGetValue(nome, out var definition))
                return new CallFunctionBlock(nome, null, null);

            if (copyingFunctions.Contains(nome))
                return new CallFunctionBlock(nome, null, null);

            var block = new CallFunctionBlock(nome, definition, ResolveArguments(blockNode));
            var nextCopying = new HashSet<string>(copyingFunctions) { nome };
            block.Body = CopyChainFrom(definition.bodyBlockId, nextCopying);
            return block;
        }

        // Argumentos da chamada: paramIds[1..] resolvidos como valor (literal, variável ou operação).
        private List<IValueProvider> ResolveArguments(BlockNode blockNode)
        {
            var args = new List<IValueProvider>();
            if (blockNode.paramIds == null) return args;

            for (int i = 1; i < blockNode.paramIds.Count; i++)
                args.Add(ResolveValue(blockNode.paramIds[i]));

            return args;
        }

        private ExecutionBlock CreateEmitBlock(BlockNode blockNode)
        {
            string signal = ResolveSignalName(blockNode);
            RegisterEmit(signal);
            return new SignalEmitBlock(signal);
        }

        private ExecutionBlock CreateListenBlock(BlockNode blockNode)
        {
            string signal = ResolveSignalName(blockNode);
            var block = new SignalListenBlock(signal);
            RegisterListen(signal, block);
            return block;
        }

        // Bloco de debug: param[0] = texto livre (input) ou variável.
        // Texto livre usa o valor cru (o ValueParser converteria "10"/"verdadeiro" em int/bool).
        private ExecutionBlock CreateDebugBlock(BlockNode blockNode)
        {
            if (blockNode.paramIds == null || blockNode.paramIds.Count == 0) return new DebugBlock(null);

            int paramId = blockNode.paramIds[0];
            if (paramId < 0 || paramId >= _paramCache.Length || _paramCache[paramId] == null) return new DebugBlock(null);

            var paramNode = _paramCache[paramId];
            if (!string.IsNullOrEmpty(paramNode.varName))
                return new DebugBlock(ResolveValue(paramId), paramNode.varName);

            return new DebugBlock(new LiteralValue(paramNode.value));
        }

        private IValueProvider ResolveValue(int id)
        {
            if (id < 0 || id >= _paramCache.Length || _paramCache[id] == null) return null;

            var paramNode = _paramCache[id];
            if (paramNode.value == "NÃO")
            {
                if (paramNode.rightOperandId < 0 || paramNode.leftOperandId >= 0)
                {
                    throw new Exception("Estrutura inválida para a operação NÃO.");
                }

                IValueProvider right = ResolveValue(paramNode.rightOperandId);
                return new OperationValue(null, right, (_, value) => !(bool)value);
            }

            if (paramNode.value == "contém")
            {
                if (paramNode.leftOperandId < 0 || paramNode.rightOperandId < 0)
                    throw new Exception("Estrutura inválida para a operação contém.");
                return new EntityContainsValue(
                    ResolveValue(paramNode.leftOperandId),
                    ResolveValue(paramNode.rightOperandId));
            }

            if (paramNode.leftOperandId >= 0 && paramNode.rightOperandId >= 0)
            {
                IValueProvider left = ResolveValue(paramNode.leftOperandId);
                IValueProvider right = ResolveValue(paramNode.rightOperandId);

                Func<object, object, object> opDelegate = paramNode.value switch
                {
                    "=="  => (l, r) =>  Equals(l, r),
                    "<="  => (l, r) =>  (int) l <= (int)  r,
                    "<"   => (l, r) =>  (int) l <  (int)  r,
                    ">="  => (l, r) =>  (int) l >= (int)  r,
                    ">"   => (l, r) =>  (int) l >  (int)  r,
                    "+"   => (l, r) =>  (int) l +  (int)  r,
                    "-"   => (l, r) =>  (int) l -  (int)  r,
                    "*"   => (l, r) =>  (int) l *  (int)  r,
                    "/"   => (l, r) =>  (int) l /  (int)  r,//divisão por 0???? (impede aqui ou no digitar? acho melhor aqui na execução)
                    "E"   => (l, r) => (bool) l && (bool) r,
                    "OU"  => (l, r) => (bool) l || (bool) r,
                    _ => throw new Exception($"Operador inválido: {paramNode.value}")
                };

                return new OperationValue(left, right, opDelegate);
            }

            if (!string.IsNullOrEmpty(paramNode.varName)) 
                return new VariableValue(paramNode.varName);

            // LINHA ATUALIZADA: Usando o parser unificado para os literais
            object valorConvertido = ValueParser.ParseRawString(paramNode.value);
            return new LiteralValue(valorConvertido);
        }
    }

    public static class ValueParser
    {
        public static object ParseRawString(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return null;

            switch (rawValue) {
               case "esquerda": return new Vector2Int(-1, 0);
               case "direita": return new Vector2Int(1, 0);
               case "baixo": return new Vector2Int(0, -1);
               case "cima": return new Vector2Int(0, 1);
               case "verdadeiro": return true;
               case "falso": return false;
               // Material: o SectionDatabase guarda o id (string numérica), então cai no
               // parse numérico abaixo. Nome de material é resolvido pelo bloco (fallback).
            }

            // 2. Tenta converter para Float/Numérico
            if (int.TryParse(rawValue, out int numericValue))
            {
                return numericValue;
            }

            // 4. Se não for nenhum dos acima, assume que é uma String pura
            return rawValue;
        }
    }
} 
