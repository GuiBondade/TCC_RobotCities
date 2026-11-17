using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlacementManager : MonoBehaviour {
    /* 
        AS FUNÇÕES DE INPUT COMO START BUILDING E DESTRUCT SÃO CHAMADAS PELO SCRIPT "BuildingInputManager" NA UISCENE
        (GameObject "BuildingInputManager", dentro de "BuildingLogic"), E DAI OS BOTOES DE CONSTRUIR E DESTRUIR VAO ATIVAR
        AS FUNCOES DELE. COMO O PLACEMENT MANAGER ESTA NO MAPSCENE E A UI NO UISCENE, A COMUNICACAO ACONTECE VIA SIGNALS
        (ConstructionSignals), E NAO VIA REFERENCIA DIRETA ENTRE AS CENAS.
     */
     // basicamente esse script vai só executar funções de acordo com a recepção de signals da interface de building "palette item"
     // passando os dados da construção e tudo mais


    [Header("Referências")]
    [SerializeField] public MapManager mapManager; // guarda o dicionario de tiles ocupados
    [SerializeField] public Transform buildingContent; // armazena os GO das construções (que tem collisionBox pra detectar clique)
    [Tooltip("Fonte única dos tilemaps (colisão, camadas visuais e preview). Se ficar vazio, pega do MapManager.")]
    [SerializeField] public MapLayers mapLayers;
    [Tooltip("Contexto do mapa alvo atual. O mapa principal usa o mainContext do MapManager; o puzzle troca via SetActiveContext.")]
    [SerializeField] public MapContext mapContext;

    private MapManager subscribedMapManager;
    private MapLayers defaultMapLayers;
    private Transform defaultBuildingContent;


    // Construção ativa: recebida dinamicamente via ConstructionSignals.OnConstructionSelected
    // (nenhuma construção fixa é hardcoded aqui, qualquer ConstructionData da paleta é aceita)
    public ConstructionData ActiveConstruction { get; private set; }

    // controle de posicionamento
    bool isPlacing = false;
    Vector3Int lastPreviewBaseCell; // posição inicial do último preview
    bool hasPreview = false; // se já tem preview desenhado

    // controle de destruição
    bool isDestroying = false;
    BuildingController previewDestructionTarget = null;

    // Fonte dos tilemaps: usa a referência direta ou procura um MapLayers
    // (no próprio GameObject/pai, no MapManager ou no MapContext principal).
    void Awake() {
        if (mapManager == null) mapManager = MapManager.instancia;
        if (mapManager == null) mapManager = GetComponentInParent<MapManager>();

        if (mapLayers == null && mapManager != null) mapLayers = mapManager.layers;
        if (mapLayers == null) mapLayers = GetComponentInParent<MapLayers>();

        if (mapContext == null && mapManager != null) mapContext = mapManager.MainContext;
        if (mapContext != null) {
            mapContext.EnsureReferences();
            if (mapLayers == null) mapLayers = mapContext.layers;
            if (buildingContent == null) buildingContent = mapContext.buildingContent;
        }

        // Guarda o estado do mapa principal para restaurar quando o puzzle fechar.
        defaultMapLayers = mapLayers != null ? mapLayers : (mapManager != null ? mapManager.layers : null);
        defaultBuildingContent = buildingContent;

        if (mapLayers == null) {
            Debug.LogWarning("[PlacementManager] Nenhum MapLayers encontrado. Adicione o componente no GameObject 'MapManagers' e preencha os tilemaps pra posicionar/destruir construções.", this);
        }
    }

    void Start() {
        TrySubscribeContext();
    }

    void OnEnable() {
        ConstructionSignals.OnConstructionSelected += SelectConstruction;
        ConstructionSignals.OnDestroyModeRequested += StartDestroying;
        ConstructionSignals.OnPlacementCancelled += CancelCurrentMode;
        ConstructionSignals.OnConstructionDestroyRequested += DestroyPlacedConstruction;
        TrySubscribeContext();
    }

    void OnDisable() {
        ConstructionSignals.OnConstructionSelected -= SelectConstruction;
        ConstructionSignals.OnDestroyModeRequested -= StartDestroying;
        ConstructionSignals.OnPlacementCancelled -= CancelCurrentMode;
        ConstructionSignals.OnConstructionDestroyRequested -= DestroyPlacedConstruction;
        ConstructionSignals.SetDestroyModeActive(false);
        ConstructionSignals.SetPlacementModeActive(false);

        if (subscribedMapManager != null) {
            subscribedMapManager.OnActiveContextChanged -= HandleActiveContextChanged;
            subscribedMapManager = null;
        }
    }

    private void TrySubscribeContext() {
        if (subscribedMapManager != null) return;
        if (mapManager == null) mapManager = MapManager.instancia;
        if (mapManager == null) return;

        subscribedMapManager = mapManager;
        subscribedMapManager.OnActiveContextChanged += HandleActiveContextChanged;

        // Se o puzzle já ativou o contexto antes deste Start, entra no mapa certo.
        MapContext current = mapManager.ActiveContext;
        if (current != null) SetContext(current);
    }

    private void HandleActiveContextChanged(MapContext context) {
        SetContext(context);
    }

    // Troca o alvo de colocação para o mapa do puzzle (ou de volta ao principal,
    // quando context é null / o mainContext do MapManager).
    public void SetContext(MapContext context) {
        if (context == null) context = mapManager != null ? mapManager.MainContext : null;

        if (isPlacing || isDestroying) CancelCurrentMode();

        mapContext = context;
        if (context != null) {
            context.EnsureReferences();
            mapLayers = context.layers != null ? context.layers : defaultMapLayers;
            buildingContent = context.buildingContent != null ? context.buildingContent : defaultBuildingContent;
        } else {
            mapLayers = defaultMapLayers;
            buildingContent = defaultBuildingContent;
        }
    }

    public void RestoreContext() {
        SetContext(mapManager != null ? mapManager.MainContext : null);
    }

    /// <summary>
    /// Recebe a construção escolhida pelo jogador na paleta (via signal) e entra em modo de posicionamento.
    /// O preview é limpo na hora, para ser redesenhado com a nova construção no próximo frame.
    /// </summary>
    public void SelectConstruction(ConstructionData data) {
        if (data == null) {
            Debug.LogWarning("Seleção de construção inválida: ConstructionData nulo.");
            return;
        }

        if (data.buildingPrefab == null) {
            Debug.LogWarning($"A construção '{data.displayName}' não tem prefab atribuído.");
        }

        ActiveConstruction = data;
        isPlacing = true;
        isDestroying = false; // garantir que não está em modo de destruição
        ConstructionSignals.SetDestroyModeActive(false);
        ConstructionSignals.SetPlacementModeActive(true);
        previewDestructionTarget = null;

        ClearPreview(); // reseta hasPreview, fazendo o preview ser redesenhado já no próximo frame
        Debug.Log("Construção selecionada: " + data.displayName);
    }

    // Chamado pelo Botão UI
    public void StartPlacing() {
        if (ActiveConstruction == null) {
            Debug.LogWarning("Nenhuma construção selecionada. Escolha uma construção na paleta.");
            return;
        }
        isPlacing = true;
        isDestroying = false; // garantir que não está em modo de destruição
        ConstructionSignals.SetDestroyModeActive(false);
        ConstructionSignals.SetPlacementModeActive(true);
        ClearPreview();
        Debug.Log("Modo de posicionamento ativado: " + ActiveConstruction.displayName);
    }

    public void StartDestroying() {
        isDestroying = true;
        isPlacing = false; // garantir que não está em modo de posicionamento
        ConstructionSignals.SetDestroyModeActive(true);
        ConstructionSignals.SetPlacementModeActive(false);
        ClearPreview();
        Debug.Log("Modo de destruição ativado");
    }

    // Cancela o modo de posicionamento
    public void CancelPlacing() {
        ClearPreview();
        isPlacing = false;
        ConstructionSignals.SetPlacementModeActive(false);
        Debug.Log("Posicionamento cancelado");
    }

    public void CancelDestroying() {
        ClearPreview();
        isDestroying = false;
        ConstructionSignals.SetDestroyModeActive(false);
        previewDestructionTarget = null;
        Debug.Log("Destruição cancelada");
    }

    // Cancela o modo que estiver ativo (posicionamento ou destruição)
    public void CancelCurrentMode() {
        if (isDestroying) {
            CancelDestroying();
        } else {
            CancelPlacing();
        }
    }

    void Update() {
        // Sem os tilemaps não dá pra desenhar preview nem colocar/destruir.
        if (mapLayers == null) return;

        if (isPlacing) {
            HandlePlacing();
            return;
        }

        if (isDestroying) {
            HandleDestroying();
            return;
        }
    }

    void HandlePlacing() {
        // Célula do mouse
        Vector3Int cellPos = GetMouseCell(mapLayers.collisionTilemap);

        // Atualizar preview
        if (!hasPreview || cellPos != lastPreviewBaseCell) {
            ClearPreview(); // Limpar preview antigo
            DrawPreview(cellPos); // Desenhar novo preview
            lastPreviewBaseCell = cellPos;
            hasPreview = true;
        }

        // ao clicar com botão esquerdo, tenta colocar a construção
        if (Input.GetMouseButtonDown(0)) {
            PlaceConstructionAt(cellPos);
            // Mudar linhas abaixo para dentro de PlaceConstructionAt se quiser continuar colocando mesmo apos errar a colocação
            isPlacing = false; // desativa o modo de posicionamento após colocar, pro jogador colocar 1 por vez que clica no botão UI
            ConstructionSignals.SetPlacementModeActive(false);
            ClearPreview(); // Limpar preview após colocar
        }          

        // botão direito cancela
        if (Input.GetMouseButtonDown(1)) {
            CancelPlacing();
        }
    }

    void HandleDestroying() {
        // Célula do mouse
        Vector3Int cellPos = GetMouseCell(mapLayers.collisionTilemap);

        // Verifica se há uma construção nessa célula
        IMapEntity entity = null;
        mapManager.tilesOccupied.TryGetValue(cellPos, out entity);
        BuildingController target = entity as BuildingController;
        if (target != null && target.mapLayers != null && mapLayers != null && target.mapLayers != mapLayers) {
            target = null;
        }

        // Se o alvo mudou, atualiza o preview
        if (target != previewDestructionTarget) {
            ClearPreview();
            previewDestructionTarget = target;
            
            if (previewDestructionTarget != null) {
                DrawDestructionPreview(previewDestructionTarget);
            }
        }
        // ao clicar com botão esquerdo, destrói a construção; no vazio, sai do modo
        if (Input.GetMouseButtonDown(0)) {
            if (previewDestructionTarget == null) {
                CancelDestroying(); // clique avulso no vazio desativa o modo de destruição
                return;
            }
            DestroyPlacedConstruction(previewDestructionTarget);
        }
        // ao clicar com o botão direito, cancela o modo de destruição
        if (Input.GetMouseButtonDown(1)) {
            CancelDestroying(); 
        }

    }

    // Verifica se todas as células da construção estão livres e no terreno exigido.
    // A exigência vem de cada entrada de tilesOccupied (allowedTerrain): dá pra pedir
    // tudo sobre água (ponte), tudo sobre grama (fábrica) ou uma mistura por célula
    // (mina: parte em montanha, parte em grama).
    private bool CanPlaceAt(Vector3Int baseCell) {
        if (ActiveConstruction == null) return false;

        foreach (var t in ActiveConstruction.tilesOccupied) {
            Vector3Int pos = GetTilePosition(baseCell, t);

            if (mapManager.IsTileOccupied(pos, mapLayers)) return false; // célula já ocupada
            if (!mapLayers.HasFloor(pos)) return false; // mapa sem chão: não coloca no vazio

            TerrainType actualTerrain = mapLayers.GetTerrainAt(pos);
            if (!t.allowedTerrain.Allows(actualTerrain)) return false; // terreno incompatível
        }
        return true; // todas as células livres e no terreno certo
    }

    // Desenha preview fantasma
    void DrawPreview(Vector3Int baseCell) {
        if (ActiveConstruction == null) return;

        Color previewColor = CanPlaceAt(baseCell) ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);

        foreach (var t in ActiveConstruction.tilesVisual) {
            Vector3Int pos = GetTilePosition(baseCell, t);
            SetTileWithColor(mapLayers.previewTilemap, pos, t.tile, previewColor);
    }}

    void DrawDestructionPreview(BuildingController target) {
        Color previewColor = new Color(1f, 0f, 0f, 0.5f); // vermelho transparente 

        foreach (var v in target.visualTiles) {
            SetTileWithColor(mapLayers.previewTilemap, v.pos, v.tile, previewColor);
        }
    }

    // limpa o preview
    void ClearPreview() {
        if (mapLayers == null) return; // ainda sem o MapLayers: nada desenhado pra limpar
        mapLayers.previewTilemap.ClearAllTiles();
        hasPreview = false;
    }

    // Pinta a construção no Tilemap a partir da célula base
    void PlaceConstructionAt(Vector3Int baseCell) {
        if (ActiveConstruction == null) return;

        if (!CanPlaceAt(baseCell)) {
            Debug.Log("Não é possível colocar a construção aqui.");
            return;
        }

        if (ActiveConstruction.buildingPrefab == null) {
            Debug.LogError($"A construção '{ActiveConstruction.displayName}' não tem prefab atribuído.");
            return;
        }

        // Instancia a construção no mapa
        var worldPos = mapLayers.collisionTilemap.CellToWorld(baseCell);
        BuildingController pc = Instantiate(ActiveConstruction.buildingPrefab, worldPos, Quaternion.identity, buildingContent);

        // A construção pertence ao mapa que está sendo colocado. O MapManager é
        // global, então é essa referência que evita pintar no mapa errado.
        pc.mapLayers = mapLayers;
        pc.mapContext = mapContext;

        // Setup da Construção
        pc.Initialize(ActiveConstruction);
        pc.baseCell = baseCell;

        // Cor inicial dos tiles: azulada/semitransparente se em obra, branco opaco se já ativa.
        Color initialTint = (pc.state == BuildingState.UnderConstruction)
            ? pc.underConstructionTint
            : Color.white;

        // Coloca a construção. A camada de cada tile vem do próprio formato dela
        // (offset.y): a fileira de baixo começa na menor camada e as de cima crescem.
        int minY = MapLayers.MinOffsetY(ActiveConstruction.tilesVisual);

        // Construções andáveis (ex.: ponte) são planas: todas as fileiras ficam na
        // camada 1 (abaixo do robô). Sem isso, a fileira de cima iria para a camada 2,
        // empatando com o robô e podendo cobri-lo.
        bool flatVisual = HasWalkableTile(ActiveConstruction);

        foreach (var t in ActiveConstruction.tilesVisual) {
            Vector3Int pos = GetTilePosition(baseCell, t);
            int layer = flatVisual ? 1 : MapLayers.DepthLayer(t.offset.y, minY);
            mapLayers.SetVisualTile(layer, pos, t.tile, initialTint); // cor inicial (azulada se em obra)

            // registra no objeto da instancia
            pc.visualTiles.Add(new PlacedTile(pos, t.tile, layer));
        }

        // marca as células como ocupadas e registra no objeto da instancia
        foreach (var t in ActiveConstruction.tilesOccupied) {
            Vector3Int pos = GetTilePosition(baseCell, t);
            pc.occupiedPositions.Add(pos);
        }

        // células andáveis (ex.: linha de cima da ponte)
        pc.BuildWalkableCells();

        // ocupa as células e adiciona à lista de construções colocadas
        mapManager.RegisterEntity(pc, pc.occupiedPositions);

        string objectiveKey = !string.IsNullOrEmpty(ActiveConstruction.id)
            ? ActiveConstruction.id
            : ActiveConstruction.name;
        ObjectiveSignals.TriggerConstructionPlaced(objectiveKey, mapContext);

        // Construções sem fase de obra já nascem ativas: para os objetivos, colocar
        // equivale a concluir. As que exigem materiais emitem ao terminar o canteiro.
        if (pc.state == BuildingState.Active) {
            ObjectiveSignals.TriggerConstructionCompleted(objectiveKey, mapContext);
        }

        Debug.Log($"Construção '{ActiveConstruction.displayName}' colocada em {baseCell}. Estado: {pc.state}.");
    }

    // A construção tem alguma célula andável (ex.: ponte)? Usa a mesma fonte do
    // BuildingController: tilesOccupied ou, se vazio, tilesVisual.
    private static bool HasWalkableTile(ConstructionData data) {
        if (data == null) return false;
        var source = (data.tilesOccupied != null && data.tilesOccupied.Count > 0)
            ? data.tilesOccupied : data.tilesVisual;
        if (source == null) return false;
        return source.Any(t => t.walkable);
    }

    void DestroyPlacedConstruction(BuildingController pc) {
        if (pc == null) return;
        if (pc.mapLayers != null && mapLayers != null && pc.mapLayers != mapLayers) return;

        if (pc.HasRobotOnFootprint()) {
            Debug.LogWarning($"[Construção] Não é possível destruir '{pc.data.displayName}' com um robô em cima.");
            return;
        }

        // Só destrói com os slots vazios: destruir jogaria fora os itens guardados
        // (materiais em obra, estoque, ferramenta produzida etc.).
        if (pc.inventoryManager != null && !pc.inventoryManager.IsEmpty()) {
            Debug.LogWarning($"[Construção] Não é possível destruir '{pc.displayName}': os slots ainda têm itens. Esvazie antes de destruir.");
            return;
        }

        // Remove os tiles visuais
        foreach (var pt in pc.visualTiles) {
            mapLayers.SetVisualTile(pt.layer, pt.pos, null, Color.white); // remove o tile
        }

        // Libera as células ocupadas e tira da lista de construções colocadas
        mapManager.RemoveEntity(pc);

        ClearPreview();
        Destroy(pc.gameObject); // destrói o objeto da construção
        CancelDestroying();
        Debug.Log($"Construção '{pc.data.displayName}' destruída.");
    }

    // FUNÇÕES AUXILIARES

    private Vector3Int GetTilePosition(Vector3Int baseCell, TileInfo tile) {
        return baseCell + new Vector3Int(tile.offset.x, tile.offset.y, 0);
    }

    private void SetTileWithColor(Tilemap map, Vector3Int pos, TileBase tile, Color color) {
        if (map == null) return;
        map.SetTile(pos, tile);
        map.SetColor(pos, color);
    }

    private Vector3Int GetMouseCell(Tilemap referenceTilemap) {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        // Alinha z ao do tilemap (evitar problemas com Z da Camera diferente)
        mouseWorld.z = referenceTilemap.transform.position.z;
        return referenceTilemap.WorldToCell(mouseWorld);
    }
}
