using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Gerenciador dos spawners de recurso do mapa.
//
// Cada "zona" é uma área em volta de uma célula, ligada a um tipo de recurso
// (ResourceData). A cada spawnIntervalTicks ticks a zona tenta colocar UM recurso numa
// célula livre sorteada. A área é a única região onde o recurso nasce: o footprint
// inteiro (tilesOccupied do asset) precisa caber dentro dela e todas as células do
// footprint precisam estar livres no MapManager. Com spacing > 0, nenhuma célula ocupada
// (recurso, construção ou robô) pode estar a até 'spacing' células de cada célula do
// footprint, em todas as direções (inclui diagonal).
// O intervalo conta ticks do TickSignal (o mesmo da execução dos robôs), então a taxa
// de spawn acompanha o speedTick.
public class ResourceSpawnManager : MonoBehaviour
{
    public enum AreaShape { Square, Circle }

    [System.Serializable]
    public class SpawnZone
    {
        public ResourceData resourceData;

        [Tooltip("Célula central da área de spawn.")]
        public Vector3Int centerCell;

        [Tooltip("Lado da área em células (3 = 3x3, 5 = 5x5). Só valores ímpares têm célula central.")]
        public int areaSize = 3;

        public AreaShape shape = AreaShape.Square;

        [Tooltip("Ticks entre tentativas de spawn (1 tick = 1/speedTick segundos).")]
        public int spawnIntervalTicks = 25; // 25 ticks = 5s na velocidade padrão (5 ticks/s)

        [Tooltip("Células livres exigidas ao redor do footprint, em todas as direções " +
                 "(inclui diagonal). 0 = pode encostar; N = N células vazias entre este " +
                 "recurso e qualquer outra célula ocupada (recursos, construções, robôs).")]
        public int spacing = 1;

        [System.NonSerialized] public int ticksSinceSpawn;
    }

    [Header("Zonas de spawn")]
    [SerializeField] public List<SpawnZone> zones = new List<SpawnZone>();

    [Tooltip("Pai dos recursos instanciados (organização da cena). Vazio = usa o container do MapContext.")]
    [SerializeField] public Transform resourceContainer;

    [Tooltip("Contexto do mapa deste spawner. Vazio = procura no pai / usa o mapa principal.")]
    [SerializeField] public MapContext mapContext;

    [Tooltip("Fonte dos ticks. Vazio = usa o TickSignal do ExecutionManager.")]
    [SerializeField] public TickSignal tickSignal;

    private MapLayers ResolveLayers() {
        if (mapContext == null) mapContext = GetComponentInParent<MapContext>();
        if (mapContext != null) {
            if (mapContext.layers == null) mapContext.EnsureReferences();
            if (resourceContainer == null) resourceContainer = mapContext.resourceContainer;
            if (mapContext.layers != null) return mapContext.layers;
        }

        MapLayers parentLayers = GetComponentInParent<MapLayers>();
        if (parentLayers != null) return parentLayers;
        return MapManager.instancia != null ? MapManager.instancia.layers : null;
    }

    private bool subscribed;

    // Mantém a área com célula central e o intervalo válido ao editar no Inspector.
    private void OnValidate() {
        if (zones == null) return;

        foreach (var zone in zones) {
            if (zone == null) continue;
            if (zone.areaSize < 1) zone.areaSize = 1;
            if (zone.areaSize % 2 == 0) zone.areaSize++; // 3/5: área com célula central
            if (zone.spawnIntervalTicks < 1) zone.spawnIntervalTicks = 1;
            if (zone.spacing < 0) zone.spacing = 0;
            if (zone.spacing > 10) zone.spacing = 10;
        }
    }

    // Assina já no OnEnable quando o TickSignal está setado no Inspector; o Start
    // repete a tentativa porque o ExecutionManager.instancia pode não existir ainda
    // (ordem de Awake/OnEnable entre objetos da cena).
    private void OnEnable() => TrySubscribe(false);

    private void Start() => TrySubscribe(true);

    private void OnDisable() {
        if (tickSignal != null) tickSignal.UnregisterListener(ProcessTick);
        subscribed = false;
    }

    private void TrySubscribe(bool logError) {
        if (subscribed) return;

        if (tickSignal == null && ExecutionManager.instancia != null) {
            tickSignal = ExecutionManager.instancia.tickSignal;
        }

        if (tickSignal == null) {
            if (logError) {
                Debug.LogError("[Spawn] TickSignal não encontrado: os recursos não vão spawnar.", this);
            }
            return;
        }

        tickSignal.RegisterListener(ProcessTick);
        subscribed = true;
    }

    // Um tick do jogo: cada zona acumula e tenta spawnar ao atingir o seu intervalo.
    private void ProcessTick() {
        if (zones == null) return;

        foreach (var zone in zones) {
            if (!IsValid(zone)) continue;

            zone.ticksSinceSpawn++;
            if (zone.ticksSinceSpawn < zone.spawnIntervalTicks) continue;

            zone.ticksSinceSpawn = 0;
            TrySpawn(zone);
        }
    }

    private bool IsValid(SpawnZone zone) {
        return zone != null
            && zone.resourceData != null
            && zone.resourceData.resourcePrefab != null
            && zone.spawnIntervalTicks > 0;
    }

    // Tenta colocar um recurso da zona numa célula livre sorteada.
    private void TrySpawn(SpawnZone zone) {
        MapManager map = MapManager.instancia;
        MapLayers layers = ResolveLayers();
        if (map == null || layers == null || layers.collisionTilemap == null) return;

        List<Vector3Int> anchors = GetValidAnchors(zone, map, layers);
        if (anchors.Count == 0) return; // área cheia: espera o próximo ciclo

        Vector3Int anchor = anchors[Random.Range(0, anchors.Count)];
        Vector3 worldPos = layers.collisionTilemap.CellToWorld(anchor);

        Transform parent = resourceContainer != null
            ? resourceContainer
            : (mapContext != null ? mapContext.resourceContainer : null);

        ResourceController resource = Instantiate(
            zone.resourceData.resourcePrefab, worldPos, Quaternion.identity, parent);
        if (resource != null) {
            resource.mapLayers = layers;
            resource.mapContext = mapContext;
            resource.Initialize(zone.resourceData, anchor);
        }
    }

    // Células da área onde o footprint inteiro cabe, todas as células do footprint
    // estão livres e cada uma está no terreno exigido pelo recurso. A área é
    // fechada: nada nasce fora dela.
    private List<Vector3Int> GetValidAnchors(SpawnZone zone, MapManager map, MapLayers layers) {
        var anchors = new List<Vector3Int>();
        var footprint = zone.resourceData.GetFootprintTiles();

        int half = Mathf.Max(0, (zone.areaSize - 1) / 2);
        int halfSq = half * half;

        for (int x = -half; x <= half; x++) {
            for (int y = -half; y <= half; y++) {
                if (zone.shape == AreaShape.Circle && x * x + y * y > halfSq) continue;

                Vector3Int anchor = zone.centerCell + new Vector3Int(x, y, 0);
                if (FootprintFits(anchor, footprint, zone, half, map, layers)) anchors.Add(anchor);
            }
        }

        return anchors;
    }

    // Footprint dentro da área (quadrado/círculo), sem sobrepor nenhuma célula
    // ocupada e com o terreno certo em cada célula.
    private bool FootprintFits(Vector3Int anchor, List<TileInfo> footprint, SpawnZone zone, int half, MapManager map, MapLayers layers) {
        int halfSq = half * half;

        foreach (var cell in footprint) {
            Vector3Int pos = anchor + new Vector3Int(cell.offset.x, cell.offset.y, 0);
            int dx = pos.x - zone.centerCell.x;
            int dy = pos.y - zone.centerCell.y;

            if (Mathf.Abs(dx) > half || Mathf.Abs(dy) > half) return false;
            if (zone.shape == AreaShape.Circle && dx * dx + dy * dy > halfSq) return false;
            if (map.IsTileOccupied(pos, layers)) return false;

            // Terreno: cada célula do footprint exige o que estiver no
            // TileInfo.allowedTerrain (Nenhum = qualquer terreno).
            TerrainType actualTerrain = layers.GetTerrainAt(pos);
            if (!cell.allowedTerrain.Allows(actualTerrain)) return false;

            // Espaçamento: nenhuma célula ocupada (recurso, construção ou robô) a
            // até 'spacing' células do footprint, em todas as direções (inclui
            // diagonal). A checagem é por célula, então footprint multi-tile também
            // não encosta em nada.
            if (zone.spacing > 0 && HasOccupiedWithin(pos, zone.spacing, map, layers)) return false;
        }

        return true;
    }

    // Célula ocupada em qualquer direção a até 'radius' células de 'pos'.
    private static bool HasOccupiedWithin(Vector3Int pos, int radius, MapManager map, MapLayers layers) {
        for (int dx = -radius; dx <= radius; dx++) {
            for (int dy = -radius; dy <= radius; dy++) {
                if (dx == 0 && dy == 0) continue;
                if (map.IsTileOccupied(pos + new Vector3Int(dx, dy, 0), layers)) return true;
            }
        }
        return false;
    }

    // Desenha a área de cada zona na Scene view (só com o manager selecionado).
    private void OnDrawGizmosSelected() {
        if (zones == null) return;

        MapLayers layers = ResolveLayers();
        Tilemap tilemap = layers != null ? layers.collisionTilemap : null;

        foreach (var zone in zones) {
            if (zone == null) continue;

            int half = Mathf.Max(0, (zone.areaSize - 1) / 2);
            float cell = tilemap != null ? tilemap.cellSize.x : 1f;
            float size = (half * 2 + 1) * cell;

            Vector3 min = tilemap != null
                ? tilemap.CellToWorld(zone.centerCell + new Vector3Int(-half, -half, 0))
                : new Vector3(zone.centerCell.x - half, zone.centerCell.y - half, 0f) * cell;
            Vector3 center = min + new Vector3(size / 2f, size / 2f, 0f);

            Gizmos.color = zone.resourceData != null
                ? new Color(0f, 1f, 0f, 0.5f)
                : new Color(1f, 0f, 0f, 0.5f);

            if (zone.shape == AreaShape.Square) {
                Gizmos.DrawWireCube(center, new Vector3(size, size, 0f));
            } else {
                Gizmos.DrawWireSphere(center, size / 2f);
            }
        }
    }
}
