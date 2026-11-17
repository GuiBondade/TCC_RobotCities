using UnityEngine;
using System.Collections.Generic;

// Movimentação do robô: um tile por tick, com checagem de ocupação no MapManager.
// O movimento é dirigido por tick (TickSignal): a cada tick o robô tenta
// avançar um tile. Se o tile estiver ocupado, o tick é gasto do mesmo jeito
// (o robô fica onde está e tenta de novo no tick seguinte).
public class RobotMovement : MonoBehaviour
{
    [SerializeField] public TickSignal tickSignal; // opcional: se vazio, usa o do ExecutionManager
    [Tooltip("Tilemaps do mapa deste robô. Vazio = usa o MapContext pai ou o mapa principal.")]
    [SerializeField] public MapLayers mapLayers;
    [Tooltip("Contexto do mapa deste robô (preenchido pelo RobotSpawner ou pelo MapContext pai).")]
    [SerializeField] public MapContext mapContext;

    public bool isMoving = false;
    public int distanceToMove = 0; // ticks de tentativa restantes (1 tile por tick)
    public Vector2Int direction;

    public Vector2 tilePosition; // posição de mundo do tile onde o robô está
    public Vector3Int currentCell; // célula do robô no grid (mesmo espaço do MapManager.tilesOccupied)
    public Vector2 tileSize;
    public float robotSpeed; // tileSize.x * ticksPorSegundo => um tile por tick

    private Vector2 currentTile; // destino visual (mundo) da célula atual
    private IMapEntity owner;    // quem ocupa as células no MapManager (o próprio robô)
    private bool blockedLogged;  // não repete o erro a cada tick de tentativa

    // Entidades acima do robô na célula atual (sobreposição). Evita retrabalho
    // repintando tiles apenas quando o conjunto muda.
    private readonly HashSet<IMapEntity> previousOverlapped = new HashSet<IMapEntity>();
    private readonly HashSet<IMapEntity> currentOverlapped = new HashSet<IMapEntity>();

    private enum StepResult { Moved, Blocked, FatalError }

    public void MoveTo(Vector2Int dir, int ticks) {
        if (ticks <= 0) return;

        if (dir == Vector2Int.zero) {
            Debug.LogError("[Movimento] Direção inválida (zero): o robô não se move.", this);
            return;
        }

        direction = dir;
        distanceToMove = ticks; // ticks que o robô vai tentar andar
        isMoving = true;
    }

    public bool AdvanceOneStep() {
        if (!isMoving || distanceToMove <= 0) {
            isMoving = false;
            return true;
        }

        // Um tick = uma tentativa de andar um tile.
        // O tick é gasto mesmo se a tentativa falhar: o robô tenta de novo no próximo tick.
        distanceToMove--;

        var nextCell = currentCell + new Vector3Int(direction.x, direction.y, 0);
        var result = TryReserveCell(nextCell);

        if (result == StepResult.FatalError) {
            isMoving = false;
            distanceToMove = 0;
            return true;
        }

        if (result == StepResult.Blocked) {
            // Fica onde está e tenta de novo no próximo tick.
            if (distanceToMove <= 0) {
                Debug.LogWarning("[Movimento] Caminho bloqueado: o robô não completou a caminhada.", this);
                isMoving = false;
                return true;
            }
            return false;
        }

        // Moved: o novo tile já foi ocupado e o antigo desocupado (TryReserveCell).
        Vector3Int previousCell = currentCell;
        currentCell = nextCell;
        currentTile += direction * tileSize;
        tilePosition = currentTile;

        // Atualiza o highlight de sobreposição (repinta tiles da entidade acima, se houver).
        UpdateOverlapHighlight(currentCell);

        // Transições de tile alimentam objetivos sem polling: só emite quando o
        // passo realmente aconteceu (tentativas bloqueadas não geram evento).
        MapContext objectiveContext = ResolveObjectiveMapContext();
        ObjectiveSignals.TriggerRobotExitedTile(previousCell, objectiveContext);
        ObjectiveSignals.TriggerRobotEnteredTile(currentCell, objectiveContext);

        if (distanceToMove <= 0) {
            isMoving = false;
            return true;
        }
        return false;
    }

    // Mapa do robô: referência explícita, MapContext pai ou mapa principal.
    public MapLayers Layers {
        get {
            if (mapLayers != null) return mapLayers;
            ResolveMapLayers();
            return mapLayers;
        }
    }

    void Awake() {
        // O RobotScript implementa IMapEntity: é ele que ocupa as células no MapManager.
        owner = GetComponent<RobotScript>();
        ResolveMapLayers();
    }

    void Start() {
        ResolveMapLayers();
        currentTile = transform.position;
        UpdateOverlapHighlight(currentCell);

        // Cobre o caso de o robô já nascer/restaurar no tile exigido por um objetivo.
        ObjectiveSignals.TriggerRobotEnteredTile(currentCell, ResolveObjectiveMapContext());
    }

    // Contexto usado nos eventos de objetivo. O fallback é o mapa principal,
    // coerente com o ResolveMapLayers; robôs de puzzle recebem o contexto do spawner.
    private MapContext ResolveObjectiveMapContext() {
        if (mapContext != null) return mapContext;

        mapContext = GetComponentInParent<MapContext>();
        if (mapContext != null) return mapContext;

        return MapManager.instancia != null ? MapManager.instancia.MainContext : null;
    }

    private void ResolveMapLayers() {
        if (mapLayers != null) return;

        if (mapContext == null) mapContext = GetComponentInParent<MapContext>();
        if (mapContext != null) {
            if (mapContext.layers == null) mapContext.EnsureReferences();
            mapLayers = mapContext.layers;
        }
        if (mapLayers == null) mapLayers = GetComponentInParent<MapLayers>();
        if (mapLayers == null && MapManager.instancia != null) mapLayers = MapManager.instancia.layers;
    }

    void Update() {
        // Visual: o robô caminha até o tile alvo (1 tile por tick).
        // Continua mesmo com isMoving == false: o último passo termina de chegar aqui.
        var step = robotSpeed * Time.deltaTime;
        transform.position = Vector2.MoveTowards((Vector2)transform.position, currentTile, step);
    }

    // Ocupação: checa o próximo tile no MapManager; se estiver livre, ocupa o novo
    // e desocupa o anterior.
    private StepResult TryReserveCell(Vector3Int nextCell) {
        var map = MapManager.instancia;
        if (map == null) {
            Debug.LogError("[Movimento] MapManager não encontrado na cena: não dá pra checar a ocupação.", this);
            return StepResult.FatalError;
        }

        if (owner == null) {
            Debug.LogError("[Movimento] Robô sem entidade (IMapEntity): não dá pra ocupar a célula.", this);
            return StepResult.FatalError;
        }

        var occupant = map.GetTileEntity(nextCell);
        // ReferenceEquals: um objeto já destruído pelo Unity continua sendo referência válida.
        if (occupant != null && !ReferenceEquals(occupant, owner)) {
            // Construção "andável" (ex.: linha de cima da ponte): o robô pode pisar
            // nela sem sobrescrever a ocupação da construção no MapManager.
            bool walkableBridge = occupant is BuildingController bc
                && bc.IsCellWalkable(nextCell);
            if (!walkableBridge) {
                if (!blockedLogged) {
                    blockedLogged = true;
                    Debug.LogError("[Movimento] Caminho bloqueado: célula " + nextCell + " já está ocupada. Tentando de novo nos próximos ticks.", this);
                }
                return StepResult.Blocked;
            }
            // Ponte: permite o passo sem ocupar a célula (a ponte continua dona dela).
            blockedLogged = false;
            // Libera a célula anterior apenas se o próprio robô era o dono dela.
            var prevOccupant = map.GetTileEntity(currentCell);
            if (ReferenceEquals(prevOccupant, owner)) map.DesoccupyTile(currentCell);
            return StepResult.Moved;
        }

        // Célula livre: checa o tipo de terreno do chão.
        // Só grama (e células ainda não classificadas) são andáveis; água e montanha
        // bloqueiam, a menos que haja uma ponte (tratada acima).
        MapLayers movementLayers = Layers;
        TerrainType actualTerrain = movementLayers != null ? movementLayers.GetTerrainAt(nextCell) : TerrainType.Nenhum;
        bool terrainOk = actualTerrain == TerrainType.Nenhum
            || (actualTerrain & TerrainType.Grama) != 0;
        if (!terrainOk) {
            if (!blockedLogged) {
                blockedLogged = true;
                Debug.LogWarning("[Movimento] Terreno intransitável em " + nextCell + " (" + actualTerrain + ").", this);
            }
            return StepResult.Blocked;
        }

        blockedLogged = false;
        map.OccupyTile(nextCell, owner);
        // Libera a célula anterior apenas se o próprio robô era o dono dela
        // (evita apagar uma ponte sobre a qual o robô estava).
        var prev = map.GetTileEntity(currentCell);
        if (ReferenceEquals(prev, owner)) map.DesoccupyTile(currentCell);
        return StepResult.Moved;
    }

    // Atualiza o highlight de sobreposição: detecta entidade acima na célula do robô
    // e repinta seus tiles com alpha reduzido (ou restaura ao sair).
    private void UpdateOverlapHighlight(Vector3Int cell) {
        var map = MapManager.instancia;
        if (map == null) return;

        currentOverlapped.Clear();

        // Detecta entidades visualmente acima do robô na mesma coluna (layers superiores).
        // Itera pelas construções e recursos registrados e verifica se o robô está
        // dentro do footprint (bounding box) da entidade e se ela está acima (y maior).
        if (map.allBuildings != null) {
            foreach (var b in map.allBuildings) {
                if (b == null) continue;
                if (!BelongsToThisMap(b)) continue;
                // Construções andáveis (ex.: ponte) não participam do highlight:
                // o robô anda sobre elas e elas nunca devem ficar transparentes.
                if (IsWalkableConstruction(b)) continue;
                if (IsEntityOverlapping(b, cell)) currentOverlapped.Add(b);
            }
        }
        if (map.allResources != null) {
            foreach (var r in map.allResources) {
                if (r == null) continue;
                if (!BelongsToThisMap(r)) continue;
                if (IsEntityOverlapping(r, cell)) currentOverlapped.Add(r);
            }
        }

        // Restaura as que saíram de sobreposição. Entidades destruídas/removidas do
        // mapa não são repintadas: senão os tiles visuais de uma construção já
        // destruída "voltam" sem existir BuildingController por trás.
        foreach (var e in previousOverlapped) {
            if (e == null) continue;
            if (currentOverlapped.Contains(e)) continue;
            if (!IsEntityStillOnMap(map, e)) continue;
            RepaintEntity(e, 1f);
        }

        // Reduz alpha das que entraram em sobreposição.
        foreach (var e in currentOverlapped) {
            if (e == null) continue;
            if (previousOverlapped.Contains(e)) continue;
            RepaintEntity(e, 0.4f);
        }

        // Troca os conjuntos (copia pra evitar aliasing).
        previousOverlapped.Clear();
        foreach (var e in currentOverlapped) if (e != null) previousOverlapped.Add(e);
    }

    // Construção andável (ex.: ponte)? Olha o mapa runtime e, como fallback,
    // os próprios dados do asset (caso walkableCells ainda não tenha sido montado).
    private static bool IsWalkableConstruction(BuildingController building) {
        if (building == null) return false;
        // Durante a obra, nenhuma construção participa do highlight de sobreposição.
        if (building.state == BuildingState.UnderConstruction) return true;
        if (building.walkableCells != null && building.walkableCells.ContainsValue(true)) return true;
        if (building.data == null) return false;

        var source = (building.data.tilesOccupied != null && building.data.tilesOccupied.Count > 0)
            ? building.data.tilesOccupied : building.data.tilesVisual;
        if (source == null) return false;

        foreach (var tile in source) {
            if (tile.walkable) return true;
        }
        return false;
    }

    // A entidade ainda existe e está registrada no mapa? Evita repintar tiles de
    // construções/recursos já destruídos ou removidos (ex.: ponte destruída).
    private static bool IsEntityStillOnMap(MapManager map, IMapEntity entity) {
        if (map == null) return false;
        if (entity is UnityEngine.Object unityObject && unityObject == null) return false;
        if (entity is BuildingController building) {
            return map.allBuildings != null && map.allBuildings.Contains(building);
        }
        if (entity is ResourceController resource) {
            return map.allResources != null && map.allResources.Contains(resource);
        }
        return true;
    }

    // Verifica se a célula do robô está nos tiles visuais da entidade (sobreposição real).
    private bool IsEntityOverlapping(IMapEntity entity, Vector3Int cell) {
        if (entity is BuildingController bc) {
            if (bc.visualTiles == null) return false;
            foreach (var pt in bc.visualTiles) {
                if (pt.pos == cell) return true;
            }
            return false;
        }
        if (entity is ResourceController rc) {
            if (rc.visualTiles == null) return false;
            foreach (var pt in rc.visualTiles) {
                if (pt.pos == cell) return true;
            }
            return false;
        }
        return false;
    }

    // Entidades do mapa principal não devem interferir no highlight de um robô do
    // puzzle (e vice-versa), mesmo que ocupem coordenadas parecidas.
    private bool BelongsToThisMap(IMapEntity entity) {
        MapLayers myLayers = Layers;
        if (myLayers == null) return true;

        if (entity is BuildingController building) {
            return building.mapLayers == null || building.mapLayers == myLayers;
        }
        if (entity is ResourceController resource) {
            return resource.mapLayers == null || resource.mapLayers == myLayers;
        }
        return true;
    }

    // Repinta os tiles visuais de uma entidade com o alpha informado.
    private void RepaintEntity(IMapEntity entity, float alpha) {
        if (entity == null) return;
        var map = MapManager.instancia;
        MapLayers layers = Layers;
        if (map == null || layers == null) return;

        if (!IsEntityStillOnMap(map, entity)) return;

        if (entity is BuildingController building && IsWalkableConstruction(building)) {
            // Ponte (andável): nunca reduz alpha, mesmo que entre no conjunto por engano.
            alpha = 1f;
        }

        Color color = new Color(1f, 1f, 1f, alpha);

        if (entity is BuildingController bc) {
            if (bc.visualTiles != null) {
                foreach (var pt in bc.visualTiles) {
                    layers.SetVisualTile(pt.layer, pt.pos, pt.tile, color);
                }
            }
        } else if (entity is ResourceController rc) {
            if (rc.visualTiles != null) {
                foreach (var pt in rc.visualTiles) {
                    layers.SetVisualTile(pt.layer, pt.pos, pt.tile, color);
                }
            }
        }
    }

    /* // Inventário da entidade no tile adjacente (via MapManager).
    public InventoryManager GetAdjacentInventory(Vector2Int dir) {
        var map = MapManager.instancia;
        if (map == null) {
            Debug.LogError("[Movimento] MapManager não encontrado na cena.", this);
            return null;
        }

        return map.GetTileInventory(currentCell + new Vector3Int(dir.x, dir.y, 0));
    } */
}
