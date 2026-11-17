using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotSpawner : MonoBehaviour
{
    [SerializeField] public ExecutionManager executionManager;
    [SerializeField] public Grid tileGrid;

    [SerializeField] public Transform robotContainer;
    [SerializeField] public GameObject robotPrefab;

    [Tooltip("Contexto do mapa deste spawner. Vazio = procura no pai / usa o mapa principal.")]
    [SerializeField] public MapContext mapContext;

    [SerializeField] public Vector3 spawnPosition;
    [SerializeField] public TickSpeedSO tickSpeedSO;

    private int nextRobotNumber = 1;

    public Vector2 tileSize;

    private MapLayers ResolveLayers() {
        if (mapContext == null) mapContext = GetComponentInParent<MapContext>();
        if (mapContext != null) {
            if (mapContext.layers == null) mapContext.EnsureReferences();
            if (mapContext.layers != null) return mapContext.layers;
        }

        MapLayers parentLayers = GetComponentInParent<MapLayers>();
        if (parentLayers != null) return parentLayers;
        return MapManager.instancia != null ? MapManager.instancia.layers : null;
    }

    private Grid ResolveGrid() {
        if (tileGrid != null) return tileGrid;
        if (mapContext != null && mapContext.grid != null) return mapContext.grid;
        return GetComponentInParent<Grid>();
    }

    private void OnEnable() {
        RobotSignals.OnSpawnRobotRequested += SpawnRobot;
    }

    private void OnDisable() {
        RobotSignals.OnSpawnRobotRequested -= SpawnRobot;
    }

    private void Start() {
        Grid grid = ResolveGrid();
        if (grid == null) {
            Debug.LogError("[RobotSpawner] Grid não configurado.", this);
            return;
        }
        tileSize = new Vector2(grid.cellSize.x, grid.cellSize.y);
    }

    private void SpawnRobot() {
        if (robotPrefab == null || tickSpeedSO == null) {
            Debug.LogError("[RobotSpawner] Não é possível criar o robô: configuração incompleta.", this);
            return;
        }

        MapManager map = MapManager.instancia;
        MapContext context = mapContext != null ? mapContext : GetComponentInParent<MapContext>();
        if (context == null && map != null) context = map.MainContext;
        MapLayers layers = ResolveLayers();
        Grid grid = ResolveGrid();
        Transform parent = robotContainer != null
            ? robotContainer
            : (context != null ? context.robotContainer : null);

        if (grid == null || parent == null) {
            Debug.LogError("[RobotSpawner] Grid/container não configurado.", this);
            return;
        }

        // O signal é global. Quando há um mapa ativo (puzzle), só o spawner daquele
        // contexto responde; o spawner principal continua existindo, mas fica quieto.
        if (map != null && map.ActiveContext != null) {
            if (context != null && context != map.ActiveContext) return;
            if (context == null && layers != null && layers != map.ActiveLayers) return;
        }

        Vector3Int spawnCell = grid.WorldToCell(spawnPosition);
        if (map != null && map.IsTileOccupied(spawnCell, layers)) {
            Debug.LogWarning("[RobotSpawner] A célula de spawn já está ocupada.", this);
            return;
        }

        GameObject robot = Instantiate(robotPrefab, parent);
        robot.transform.position = spawnPosition;
        var robotScript = robot.GetComponent<RobotScript>();
        if (robotScript == null) {
            Debug.LogError("[RobotSpawner] O prefab não possui RobotScript.", robot);
            Destroy(robot);
            return;
        }

        string displayName = BuildUniqueRobotName();
        robot.name = displayName;
        robotScript.SetDisplayName(displayName);

        if (robotScript.robotMovement != null) {
            robotScript.robotMovement.mapLayers = layers;
            robotScript.robotMovement.mapContext = context;
        }

        robotScript.StartSetup(spawnCell, tileSize, tickSpeedSO);

        RobotSignals.TriggerRobotSpawned(robotScript);
    }

    private string BuildUniqueRobotName() {
        string candidate = "Robô " + nextRobotNumber++;
        MapManager map = MapManager.instancia;
        return map != null ? map.GetUniqueEntityName(candidate) : candidate;
    }
}
