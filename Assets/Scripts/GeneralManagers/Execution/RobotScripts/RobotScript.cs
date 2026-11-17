using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

using ScriptRunner;

public class RobotScript : MapEntityBase // arquivo set/delete json
{
    [SerializeField] public RobotMovement robotMovement;
    [SerializeField] public RobotInventory robotInventory;
    [SerializeField] public RobotDebug robotDebug;

    public ScriptData scriptData;// pura referencia pro script anexado como código(que sepa da até de tirar)
    public ExecutionContext executionContext;// contexto exato em execução

    public RobotData robotData { get; private set; }// pra setar interface do robo

    private TickSpeedSO tickSpeedSO;
    
    // Signals/variáveis e id vêm do MapEntityBase; aqui fica o que é do robô
    // (nome, inventário e as variáveis do script que ele executa).
    public override string EntityName => robotData != null && !string.IsNullOrEmpty(robotData.robotName)
        ? robotData.robotName
        : gameObject.name;
    public override InventoryManager GetInventoryManager() => robotInventory != null ? robotInventory.inventoryManager : null;

    // Debug do robô (balões): referência explícita no prefab ou busca no filho (DebugContainer).
    public RobotDebug GetRobotDebug() {
        if (robotDebug == null) robotDebug = GetComponentInChildren<RobotDebug>(true);
        return robotDebug;
    }

    // Variáveis do script que este robô executa (exportadas por esse script):
    // quem importa o robô lê em modo somente leitura.
    public override IReadOnlyList<string> GetScriptVariableNames() {
        if (scriptData == null) return Array.Empty<string>();

        var catalog = ScriptManager.instancia?.GetCatalog(scriptData);
        if (catalog == null) return Array.Empty<string>();

        var names = new List<string>();
        foreach (var variable in catalog.ExportedVariables()) {
            if (variable == null || string.IsNullOrEmpty(variable.name)) continue;
            names.Add(variable.name);
        }
        return names;
    }
    
    public override object GetScriptVariableValue(string varName) {
        if (string.IsNullOrEmpty(varName)) return null;

        // Valor ao vivo: o contexto em execução do robô.
        var context = executionContext;
        if (context != null) {
            var live = context.GetVariable(varName);
            if (live != null) return live;
        }

        // Fallback: valor inicial compilado do script.
        if (scriptData?.vars != null) {
            foreach (var variable in scriptData.vars) {
                if (variable.varName != varName) continue;
                return ValueParser.ParseRawString(variable.defaultValue);
            }
        }

        return null;
    }

    public void Awake() {
        robotData = ScriptableObject.CreateInstance<RobotData>();
        robotData.robotName = gameObject.name;
        robotData.name = gameObject.name;
    }

    // Define um nome estável para o robô. Mantém RobotData e o nome do asset
    // sincronizados para que os robôs não compartilhem o mesmo nome.
    public void SetDisplayName(string displayName) {
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        if (string.IsNullOrEmpty(displayName)) return;
        if (robotData == null) robotData = ScriptableObject.CreateInstance<RobotData>();
        robotData.robotName = displayName;
        robotData.name = displayName;
    }

    public override void SetEntityName(string entityName) {
        if (string.IsNullOrEmpty(entityName)) return;
        SetDisplayName(entityName);
        gameObject.name = entityName;
    }

    public void Start() {
        RobotSignals.OnScriptAttached += OnScriptChosen;    
    }

    public void OnScriptChosen(ScriptData script) {
        scriptData = script;
    }

    public void TestExecution()// SetExecution => vai ser chamado pelos robos, e vai retornar o context
    {
        if (scriptData == null) return;
        // essa funcao vai ser chamada quando tentar ativar a execução do script anexado, 
        // porém somente se tiver selecionado ãlgum script no dropdown
        Debug.Log("Executando");

        RobotSignals.TriggerExecutionActivation(this); 
    } 

    public void StartSetup(Vector3Int position, Vector2 tileSize, TickSpeedSO tickSpeedSO) {
        if (robotMovement != null) {
            robotMovement.currentCell = position; // célula de spawn (mesmo espaço do MapManager)
            robotMovement.tileSize = tileSize;
        }

        robotInventory?.Initialize(); // monta os slots do inventário do robô

        RegisterOnMap(position);

        this.tickSpeedSO = tickSpeedSO;
        tickSpeedSO.RegisterListener(UpdateSpeed);
        UpdateSpeed(tickSpeedSO.speedTick); // velocidade inicial
    }

    // Registra o robô no mapa ocupando a célula de spawn: é o que permite ao
    // RobotMovement ocupar/desocupar células durante a caminhada.
    private void RegisterOnMap(Vector3Int position) {
        var map = MapManager.instancia;
        if (map == null) {
            Debug.LogError("[Robô] MapManager não encontrado na cena: o robô não consegue ocupar a célula de spawn.", this);
            return;
        }

        MapLayers layers = robotMovement != null ? robotMovement.Layers : null;
        var occupant = map.GetTileEntity(position);
        bool walkableBridge = occupant is BuildingController bc && bc.IsCellWalkable(position);

        if (map.IsTileOccupied(position, layers) && !walkableBridge) {
            Debug.LogError("[Robô] Célula de spawn " + position + " já está ocupada.", this);
        }

        // Spawn sobre ponte: não sobrescreve a ocupação da ponte (o robô só "pisa" nela).
        if (!walkableBridge) {
            map.RegisterEntity(this, new[] { position });
        }
    }

    // chamado pelo signal do TickSpeedSO (signal de saída)
    public void UpdateSpeed(float newSpeed) {
        robotMovement.robotSpeed = robotMovement.tileSize.x * newSpeed;
    }

    public void OnDestroy() {
        RobotSignals.OnScriptAttached -= OnScriptChosen;
        tickSpeedSO?.UnregisterListener(UpdateSpeed);
        MapManager.instancia?.RemoveEntity(this); // libera a célula que o robô ocupa
    }
}
