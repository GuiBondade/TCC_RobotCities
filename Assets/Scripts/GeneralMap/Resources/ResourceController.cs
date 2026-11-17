using System.Collections.Generic;
using UnityEngine;

// Recurso do mapa (minério, lenha, etc): uma entidade com estoque de um material só.
// O estoque é um InventoryManager com um único slot travado no material do recurso.
// Como toda entidade do mapa, ocupa células no MapManager (1 ou mais, conforme os
// tiles do ResourceData): é por elas que o bloco ExtrairMaterial acha o recurso
// (MapManager.GetTileExtractable / GetAdjacentExtractable).
public class ResourceController : MapEntityBase, IExtractable
{
    [SerializeField] public InventoryManager inventoryManager;
    [SerializeField] public ResourceData data;

    // Célula âncora do recurso no grid (canto de referência dos offsets do ResourceData).
    // O spawner preenche no Initialize; num recurso colocado direto no editor dá pra setar aqui.
    [SerializeField] public Vector3Int cell;

    [Tooltip("Collider de clique: abre a interface do recurso. O tamanho sai dos tiles visuais.")]
    [SerializeField] public BoxCollider2D boxCollider;

    // Todas as células que o recurso ocupa (calculadas a partir do ResourceData).
    public HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>();

    // Tiles pintados no mapa (guardados pra des-pintar quando o recurso esgotar).
    public List<PlacedTile> visualTiles = new List<PlacedTile>();

    [Header("Mapa")]
    [Tooltip("Tilemaps do mapa ao qual este recurso pertence. Vazio = usa o MapContext pai ou o mapa principal.")]
    [SerializeField] public MapLayers mapLayers;
    [Tooltip("Contexto do mapa (preenchido pelo spawner ou pelo MapContext pai).")]
    [SerializeField] public MapContext mapContext;

    // Nome único aplicado no registro (evita dois recursos homônimos no projeto).
    [SerializeField] private string uniqueDisplayName;

    private bool initialized;
    private bool registered;
    private bool painted;
    private bool skipUnpaint;

    public override string EntityName => !string.IsNullOrEmpty(uniqueDisplayName)
        ? uniqueDisplayName
        : (data != null ? data.displayName : name);
    public int MaterialTypeId => data != null ? data.typeId : -1;
    public ResourceType ResourceType => data != null ? data.resourceType : ResourceType.None;

    public int RemainingAmount => (inventoryManager != null && inventoryManager.slotList.Count > 0)
        ? inventoryManager.slotList[0].currentAmount
        : 0;

    public bool CanExtract => initialized && inventoryManager != null && RemainingAmount > 0;

    public override void SetEntityName(string entityName) {
        if (string.IsNullOrEmpty(entityName)) return;
        uniqueDisplayName = entityName;
        gameObject.name = entityName;
    }

    public override InventoryManager GetInventoryManager() {
        return inventoryManager;
    }

    private void Awake() {
        // Fallback: o collider pode estar num filho do prefab (é o caso do ResourcePrefab).
        if (boxCollider == null) boxCollider = GetComponentInChildren<BoxCollider2D>(true);
        ResolveMapLayers();
    }

    // Resolve o mapa do recurso sem depender da ordem de Awake entre cenas.
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

    // Chamado pelo PuzzleStateManager antes de descarregar a cena: o mapa inteiro
    // está indo embora, então não há por que limpar tiles um a um.
    public void PrepareForMapUnload() {
        skipUnpaint = true;
    }

    private void Start() {
        ResolveMapLayers();

        // Prefab já na cena (editor): inicializa depois dos Awakes, quando o
        // MapManager e o MapLayers já existem. Recurso spawnado em runtime já chega
        // inicializado pelo Initialize do spawner.
        if (!initialized && data != null) Initialize(data, cell);

        // Idempotente: garante a pintura quando o MapLayers só ficou disponível depois.
        PaintTiles();
        RegisterOnMap();
    }

    private void OnDestroy() {
        bool mapUnloading = skipUnpaint
            || MapLayers.IsApplicationQuitting
            || (mapContext != null && mapContext.IsUnloading)
            || (mapLayers != null && mapLayers.IsUnloading);

        if (Application.isPlaying && !mapUnloading) {
            UnpaintTiles();
        } else {
            visualTiles.Clear();
            painted = false;
        }

        MapManager.instancia?.RemoveEntity(this);
    }

    public void Initialize(ResourceData resourceData) => Initialize(resourceData, cell);

    public void Initialize(ResourceData resourceData, Vector3Int resourceCell) {
        if (initialized) return;

        if (resourceData == null) {
            Debug.LogError("ResourceController sem ResourceData: não dá pra montar o estoque.", this);
            return;
        }
        if (inventoryManager == null) {
            Debug.LogError("ResourceController sem InventoryManager: não dá pra montar o estoque.", this);
            return;
        }

        data = resourceData;
        cell = resourceCell;
        initialized = true;
        ResolveMapLayers();

        inventoryManager.SetupSingleSlot(data.typeId, data.totalAmount);
        BuildOccupiedPositions();
        SetupSize();
        PaintTiles();

        // Spawn em runtime: o mapa já existe, então registra na hora.
        if (MapManager.instancia != null) RegisterOnMap();
    }

    // Células ocupadas = offsets do ResourceData aplicados sobre a célula âncora.
    // A fonte dos offsets fica no próprio asset (tilesOccupied, ou tilesVisual quando
    // os ocupados estão vazios): ocupação e visual nunca se misturam aqui.
    private void BuildOccupiedPositions() {
        occupiedPositions.Clear();

        foreach (var offset in data.GetFootprintOffsets()) {
            occupiedPositions.Add(cell + new Vector3Int(offset.x, offset.y, 0));
        }
    }

    // Configura o collider de clique do mesmo jeito que a construção: tamanho e
    // centro saem do menor/maior offset dos tiles visuais.
    private void SetupSize() {
        if (boxCollider == null || data == null || data.tilesVisual == null || data.tilesVisual.Count == 0) return;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var t in data.tilesVisual) {
            if (t.offset.x < minX) minX = t.offset.x;
            if (t.offset.x > maxX) maxX = t.offset.x;
            if (t.offset.y < minY) minY = t.offset.y;
            if (t.offset.y > maxY) maxY = t.offset.y;
        }

        float width = (maxX - minX + 1);
        float height = (maxY - minY + 1);

        boxCollider.size = new Vector2(width, height);
        boxCollider.offset = new Vector2((width / 2f) + minX, (height / 2f) + minY);
    }

    // Pinta os tiles visuais no MapLayers. A camada de cada tile vem do próprio
    // formato do recurso (offset.y), igual construção: a fileira inferior começa
    // na menor camada e as de cima crescem.
    private void PaintTiles() {
        if (painted || data == null || data.tilesVisual == null) return;

        MapLayers layers = Layers;
        if (layers == null) return;

        visualTiles.Clear();
        int minY = MapLayers.MinOffsetY(data.tilesVisual);

        foreach (var t in data.tilesVisual) {
            Vector3Int pos = cell + new Vector3Int(t.offset.x, t.offset.y, 0);
            int layer = MapLayers.DepthLayer(t.offset.y, minY);

            layers.SetVisualTile(layer, pos, t.tile, Color.white);
            visualTiles.Add(new PlacedTile(pos, t.tile, layer));
        }

        painted = true;
    }

    // Remove do mapa os tiles que este recurso pintou.
    private void UnpaintTiles() {
        if (!painted) return;

        MapLayers layers = Layers;
        if (layers != null) {
            foreach (var pt in visualTiles) {
                layers.SetVisualTile(pt.layer, pt.pos, null, Color.white);
            }
        }

        visualTiles.Clear();
        painted = false;
    }

    // Fonte dos tilemaps: referência explícita, MapContext pai ou mapa principal.
    private MapLayers Layers {
        get {
            if (mapLayers != null) return mapLayers;
            ResolveMapLayers();
            return mapLayers;
        }
    }

    // Ocupa a célula no mapa (dicionário de entidades + lista de recursos).
    private void RegisterOnMap() {
        if (registered || !initialized) return;

        var map = MapManager.instancia;
        if (map == null) {
            Debug.LogError("[Recurso] MapManager não encontrado: '" + EntityName + "' não vai ocupar a célula " + cell + ".", this);
            return;
        }

        // Avisa se alguma célula já estava ocupada (o registro sobrescreve).
        foreach (var pos in occupiedPositions) {
            if (!map.IsTileOccupied(pos, Layers)) continue;
            Debug.LogWarning("[Recurso] Célula " + pos + " já está ocupada: '" + EntityName + "' vai sobrescrever.", this);
            break;
        }

        map.RegisterEntity(this, occupiedPositions);
        registered = true;
    }

    // Retira do estoque do recurso. Devolve quanto foi extraído (0 se nada saiu).
    public int ExtractResource(int amount) {
        if (!initialized || amount <= 0) return 0;

        var slot = inventoryManager.slotList[0];
        int extracted = Mathf.Min(amount, slot.currentAmount);
        if (extracted <= 0) return 0;

        if (!inventoryManager.SubtractItems(extracted, slot.currentTypeId, 0)) return 0;

        if (inventoryManager.IsEmpty()) Deplete();
        return extracted;
    }

    // Transação: tira do recurso e põe no inventário de destino.
    // Credita o destino ANTES de debitar o recurso: se o estoque zerar, o recurso é
    // destruído e não daria pra desfazer a operação depois.
    // Devolve quanto foi transferido (0 se não deu pra transferir).
    public int ExtractTo(InventoryManager target, int amount) {
        if (target == null || !initialized || amount <= 0) return 0;

        int extracted = Mathf.Min(amount, RemainingAmount);
        if (extracted <= 0) return 0;

        inventoryManager.BackupSlots();
        target.BackupSlots();

        if (!target.AddItems(extracted, MaterialTypeId, 0)) {
            target.RollBackSlots();
            return 0;
        }

        if (!inventoryManager.SubtractItems(extracted, MaterialTypeId, 0)) {
            target.RollBackSlots();
            inventoryManager.RollBackSlots();
            return 0;
        }

        if (inventoryManager.IsEmpty()) Deplete();
        return extracted;
    }

    // Recurso esgotado: sai do mapa e some (o OnDestroy desocupa a célula).
    private void Deplete() {
        Destroy(gameObject);
    }
}
