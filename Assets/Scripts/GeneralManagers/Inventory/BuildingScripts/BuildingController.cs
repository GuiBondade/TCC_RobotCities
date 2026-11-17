using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

public enum BuildingState : byte {
    UnderConstruction, // em obra: esperando os materiais e o progresso
    Active            // funcional: layouts e craftings liberados
}

public class BuildingController : MapEntityBase, IExtractable
{
    // Tom azulado e semitransparente usado nos tiles enquanto a construção está em obra.
    // Definido aqui para manter um único ponto de verdade (mesmo tom/opacidade em todos os casos).
    [SerializeField] public InventoryManager inventoryManager;
    [SerializeField] public BoxCollider2D boxCollider;
    [Tooltip("Cor/opacidade dos tiles enquanto a construção está em obra (placement inicial).")]
    [SerializeField] public Color underConstructionTint = new Color(0.35f, 0.6f, 1f, 0.55f);

    // Célula base (canto de referência) da construção no grid, usada por construções
    // pré-posicionadas na cena para calcular as células ocupadas e se registrar no
    // MapManager. Em construções colocadas em runtime (PlacementManager) é preenchido
    // automaticamente e este campo pode ficar em zero.
    [SerializeField] public Vector3Int baseCell;

    [SerializeField] public List<SlotLayoutConfig> availableLayouts = new List<SlotLayoutConfig>();
    [SerializeField] public SlotLayoutConfig defaultSlotLayout;
    [SerializeField] public string displayName;

    [Header("Mapa")]
    [Tooltip("Tilemaps do mapa a que esta construção pertence. Vazio = usa o MapContext pai ou o mapa principal.")]
    [SerializeField] public MapLayers mapLayers;
    [Tooltip("Contexto do mapa (preenchido automaticamente pelo PlacementManager ou pelo MapContext pai).")]
    [SerializeField] public MapContext mapContext;

    public SlotLayoutConfig currentSlotLayout { get; private set; }

    // Estado do ciclo de vida da construção
    public BuildingState state { get; private set; } = BuildingState.Active;
    public bool IsActive => state == BuildingState.Active;
    public bool isInitialized { get; private set; }

    // Pausa a obra enquanto houver robô dentro do footprint. Usada apenas
    // quando o inventário está cheio (processo de construção rodando).
    private bool pausedByRobot;

    // Progresso da obra (contado em ticks)
    public int constructionTicksRequired { get; private set; }
    public int constructionTicksElapsed { get; private set; }
    public float constructionProgress => constructionTicksRequired <= 0
        ? (IsActive ? 1f : 0f)
        : Mathf.Clamp01((float)constructionTicksElapsed / constructionTicksRequired);

    [Header("Eventos (gatilho da animação no Inspector)")]
    public UnityEvent onConstructionStarted;
    public UnityEvent onConstructionCompleted;

    // Dados PlacementManager
    public ConstructionData data; // tipo de construção
    public List<PlacedTile> visualTiles = new List<PlacedTile>(); // tiles visuais
    public HashSet<Vector3Int> occupiedPositions = new HashSet<Vector3Int>(); // posições ocupadas
    
    // Células andáveis pelo robô (TileInfo.walkable). Ex.: só a linha de cima da
    // ponte. As demais continuam bloqueando colocação e movimentação.
    public Dictionary<Vector3Int, bool> walkableCells = new Dictionary<Vector3Int, bool>();

    // Sinais/variáveis e id vêm do MapEntityBase; aqui fica só o que é da construção.
    public override string EntityName => displayName;

    public override void SetEntityName(string entityName) {
        if (string.IsNullOrEmpty(entityName)) return;
        displayName = entityName;
        gameObject.name = entityName;
    }

    public override InventoryManager GetInventoryManager() {
        return inventoryManager;
    }

    private void Awake() {
        if (boxCollider == null) boxCollider = GetComponentInChildren<BoxCollider2D>(true);
        ResolveMapLayers();
    }

    // Resolve o mapa da construção sem depender da ordem de Awake entre cenas.
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

    private void Start() {
        ResolveMapLayers();

        // Prefab já na cena (construção fixa): inicializa e se registra no MapManager
        // depois dos Awakes, quando MapManager e MapLayers já existem.
        // Construções colocadas em runtime já chegam inicializadas pelo PlacementManager.
        if (!isInitialized && data != null) Initialize(data);

        if (isInitialized && occupiedPositions.Count == 0 && data != null) {
            BuildOccupiedPositions();
        }

        BuildWalkableCells();

        if (MapManager.instancia != null && occupiedPositions.Count > 0) {
            MapManager.instancia.RegisterEntity(this, occupiedPositions);
        }
    }

    // Células ocupadas = offsets do ConstructionData (tilesOccupied, ou visual como
    // fallback) aplicados sobre a célula base. Mesma regra do PlacementManager.
    private void BuildOccupiedPositions() {
        occupiedPositions.Clear();
        var source = (data.tilesOccupied != null && data.tilesOccupied.Count > 0)
            ? data.tilesOccupied : data.tilesVisual;
        if (source == null) return;
        foreach (var t in source) {
            occupiedPositions.Add(baseCell + new Vector3Int(t.offset.x, t.offset.y, 0));
        }
        if (occupiedPositions.Count == 0) occupiedPositions.Add(baseCell);
    }

    // Mapeia célula -> andável a partir do TileInfo.walkable de cada célula ocupada.
    public void BuildWalkableCells() {
        walkableCells.Clear();
        if (data == null) return;

        var source = (data.tilesOccupied != null && data.tilesOccupied.Count > 0)
            ? data.tilesOccupied : data.tilesVisual;
        if (source == null) return;

        foreach (var t in source) {
            Vector3Int pos = baseCell + new Vector3Int(t.offset.x, t.offset.y, 0);
            walkableCells[pos] = t.walkable;
        }
    }

    // O robô pode andar nesta célula? Só se ela estiver mapeada como andável.
    public bool IsCellWalkable(Vector3Int cell) {
        // Durante a obra: construções normais são passáveis; pontes (walkableAfterConstruction)
        // bloqueiam até serem concluídas.
        if (state == BuildingState.UnderConstruction) {
            return !(data != null && data.walkableAfterConstruction);
        }
        return walkableCells.TryGetValue(cell, out bool walkable) && walkable;
    }

    // A célula pertence ao footprint desta construção (ocupada ou andável)?
    // Diferente de IsCellWalkable ("o robô pode passar por aqui?"), que durante a
    // obra responde true para qualquer célula — não serve pra checar se um robô
    // está em cima da construção.
    public bool ContainsCell(Vector3Int cell) {
        return occupiedPositions.Contains(cell) || walkableCells.ContainsKey(cell);
    }

    // Robô do mesmo mapa pisando no footprint (células ocupadas + andáveis).
    // Não usa IsCellWalkable: durante a obra ele devolve true para qualquer célula.
    public bool TryGetRobotOnFootprint(out RobotScript robot) {
        robot = null;
        var map = MapManager.instancia;
        if (map == null || map.allRobots == null) return false;

        foreach (var r in map.allRobots) {
            if (r == null || r.robotMovement == null) continue;
            MapLayers robotLayers = r.robotMovement.mapLayers;
            if (robotLayers != null && mapLayers != null && robotLayers != mapLayers) continue;
            if (!ContainsCell(r.robotMovement.currentCell)) continue;
            robot = r;
            return true;
        }
        return false;
    }

    public bool HasRobotOnFootprint() => TryGetRobotOnFootprint(out _);

    public void Initialize(ConstructionData constructionData)
    {
        data = constructionData;
        displayName = constructionData.displayName;

        // Prefab sem as listas configuradas: usa as do ScriptableObject como fonte.
        if (availableLayouts.Count == 0 && data.availableLayouts.Count > 0) {
            availableLayouts = new List<SlotLayoutConfig>(data.availableLayouts);
        }
        if (defaultSlotLayout == null) defaultSlotLayout = data.defaultSlotLayout;

        // Sem InventoryManager a obra não pode funcionar (não há onde exigir os materiais).
        bool hasInventory = inventoryManager != null;
        if (!hasInventory) {
            Debug.LogError($"Construção '{displayName}' sem InventoryManager: o ciclo de vida da obra fica indisponível.", this);
        }

        // Com fase de obra configurada, a construção nasce inativa e os slots viram os
        // de materiais exigidos (SelectLayout inicial zeraria qualquer material depositado).
        if (hasInventory && HasConstructionRequirements) {
            StartConstruction();
        } else {
            state = BuildingState.Active;
            if (hasInventory) ApplyActiveLayout();
        }

        SetupSize();

        if (constructionData.isExtractable &&
            (constructionData.extractableResourceType == ResourceType.None ||
             constructionData.extractableMaterialTypeId < 0)) {
            Debug.LogWarning($"Construção '{displayName}' marcada como fonte de extração mas com configuração inválida " +
                             $"(tipo={constructionData.extractableResourceType}, material={constructionData.extractableMaterialTypeId}).", this);
        }

        isInitialized = true;
    }

    // Tem fase de obra configurada (layout com os materiais exigidos).
    public bool HasConstructionRequirements => data != null
        && data.constructionLayout != null
        && data.constructionLayout.slotConfigList.Count > 0;

    // Prepara a construção para a fase de obra: slots de materiais exigidos e contador zerado.
    private void StartConstruction()
    {
        state = BuildingState.UnderConstruction;
        currentSlotLayout = null;
        constructionTicksRequired = Mathf.Max(1, data.constructionTicks);
        constructionTicksElapsed = 0;
        pausedByRobot = false;

        inventoryManager.RebuildConstructionSlots(data.constructionLayout);

        // Sanidade: o readiness usa IsFull(), que considera todos os slots.
        // Um slot Output/Tool no layout de obra nunca ficaria "cheio" e travaria a obra.
        foreach (var slotConfig in data.constructionLayout.slotConfigList) {
            if (slotConfig.role != SlotRole.Input) {
                Debug.LogWarning($"Layout de construção '{data.constructionLayout.name}' tem slot com papel {slotConfig.role}: use apenas Input.", this);
            }
        }

        onConstructionStarted?.Invoke();
        ConstructionSignals.TriggerConstructionStarted(this);
        Debug.Log($"Construção '{displayName}' iniciada: aguardando materiais de construção.");
    }

    // Aplica o layout de operação (o que a construção usa quando está funcional).
    private void ApplyActiveLayout()
    {
        SlotLayoutConfig layout = defaultSlotLayout != null
            ? defaultSlotLayout
            : (availableLayouts.Count > 0 ? availableLayouts[0] : null);

        if (layout == null) {
            // Construções sem layout de operação (ponte, mina) não precisam manter os
            // slots da obra depois de prontas: a interface fica vazia.
            if (inventoryManager != null) inventoryManager.ClearSlots();
            return;
        }

        SelectLayout(layout); // SelectLayout já faz o RebuildSlots
    }

    // Avança 1 tick da obra. Devolve true quando a construção foi concluída.
    public bool AdvanceConstruction()
    {
        if (IsActive) return true;
        if (!isInitialized) return false;
        if (inventoryManager == null) return false;

        // Sem todos os materiais depositados, o progresso zera (o material é o combustível da obra).
        if (!inventoryManager.IsFull()) {
            constructionTicksElapsed = 0;
            pausedByRobot = false; // obra não está rodando: limpa sem log de retomada
            return false;
        }

        // Com o processo de construção rodando, pausa se houver robô dentro do canteiro.
        // Checagem por tick, mas apenas neste ramo (inventário cheio), então o custo é
        // irrelevante. O progresso congela (não zera) e continua de onde parou ao sair.
        if (TryGetRobotOnFootprint(out var robot)) {
            if (!pausedByRobot) {
                pausedByRobot = true;
                Debug.Log($"[Construção] '{displayName}': obra pausada — robô '{robot.EntityName}' " +
                          $"em {robot.robotMovement.currentCell} dentro do canteiro.", this);
            }
            return false;
        }

        if (pausedByRobot) {
            pausedByRobot = false;
            Debug.Log($"[Construção] '{displayName}': obra retomada (canteiro livre).", this);
        }

        constructionTicksElapsed++;
        if (constructionTicksElapsed < constructionTicksRequired) return false;

        CompleteConstruction();
        return true;
    }

    // Consome os materiais exigidos e libera a construção.
    private void CompleteConstruction()
    {
        // Transacional: se falhar ao consumir, a obra continua (rollback).
        inventoryManager.BackupSlots();
        if (!inventoryManager.ConsumeInputSlots()) {
            inventoryManager.RollBackSlots();
            constructionTicksElapsed = constructionTicksRequired - 1;
            Debug.LogWarning($"Construção '{displayName}': falha ao consumir os materiais exigidos.", this);
            return;
        }

        state = BuildingState.Active;

        // Troca pros slots de operação (o RebuildSlots limpa os slots de construção).
        ApplyActiveLayout();

        // Restaura a cor/opacidade padrão dos tiles visuais (branco totalmente opaco).
        ResolveMapLayers();
        MapLayers layers = mapLayers;
        if (layers != null) {
            foreach (var tile in visualTiles) {
                layers.SetVisualTile(tile.layer, tile.pos, tile.tile, Color.white);
            }
        }

        onConstructionCompleted?.Invoke();
        ConstructionSignals.TriggerBuildingActivated(this);

        if (data != null) {
            string objectiveKey = !string.IsNullOrEmpty(data.id) ? data.id : data.name;
            ObjectiveSignals.TriggerConstructionCompleted(objectiveKey, mapContext);
        }

        Debug.Log($"Construção '{displayName}' concluída e ativa.");
    }

    // Configura o colisor automaticamente com base no tamanho da construção
    private void SetupSize() {
        if (boxCollider != null && data.tilesVisual.Count > 0) {
            // Calcula a largura e altura com base nos offsets do ScriptableObject
            int minX = data.tilesVisual.Min(t => t.offset.x);
            int maxX = data.tilesVisual.Max(t => t.offset.x);
            int minY = data.tilesVisual.Min(t => t.offset.y);
            int maxY = data.tilesVisual.Max(t => t.offset.y);

            float width = (maxX - minX + 1);
            float height = (maxY - minY + 1);

            // Define o tamanho e Centraliza o colisor (no meio da estrutura multi-tile)
            boxCollider.size = new Vector2(width, height);
            boxCollider.offset = new Vector2((width / 2f) + minX, (height / 2f) + minY);
        }
    }

    public void SelectLayout(SlotLayoutConfig layout) { // chamado por clicar numa receita na interface, se ativada na interface
        if (!IsActive) return; // troca de layout só liberada quando a construção estiver funcional
        if (layout == currentSlotLayout) return;

        currentSlotLayout = layout;
        inventoryManager.RebuildSlots(layout);

    }

    // ------------------------------------------------------------------
    // IExtractable: a construção também pode funcionar como fonte de recurso
    // (ex.: Mina de Pedra). Fonte de construção é infinita por definição:
    // não há estoque próprio para debitar, então cada extração devolve a
    // quantidade pedida e a construção nunca é destruída por isso.
    // ------------------------------------------------------------------
    public bool CanExtract => isInitialized && IsActive && data != null
        && data.isExtractable
        && data.extractableResourceType != ResourceType.None
        && data.extractableMaterialTypeId >= 0;

    public int MaterialTypeId => data != null ? data.extractableMaterialTypeId : -1;
    public ResourceType ResourceType => data != null ? data.extractableResourceType : ResourceType.None;

    public int ExtractResource(int amount) {
        if (!CanExtract || amount <= 0) return 0;
        return amount;
    }

    public int ExtractTo(InventoryManager target, int amount) {
        if (target == null || !CanExtract || amount <= 0) return 0;

        target.BackupSlots();
        if (!target.AddItems(amount, MaterialTypeId, 0)) {
            target.RollBackSlots();
            return 0;
        }
        return amount;
    }

}
