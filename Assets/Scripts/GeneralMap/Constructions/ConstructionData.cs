using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class TileInfo {
    public Vector2Int offset;
    public TileBase tile;
    // A camada visual (profundidade) não fica aqui: o MapLayers.DepthLayer calcula
    // em runtime a partir do offset.y relativo, então a entidade se ordena sozinha.

    [Tooltip("Terreno exigido nesta célula. Nenhum = qualquer terreno.\n" +
             "Só é usado nas células de ocupação (tilesOccupied) ou, em recursos sem " +
             "tilesOccupied, no footprint do visual. Ex.: numa mina, marque Montanha " +
             "nas células que precisam ser montanha e Grama nas que precisam ser grama.")]
    public TerrainType allowedTerrain = TerrainType.Nenhum;

    [Tooltip("Esta célula é andável pelo robô? Usado em construções como a ponte: " +
             "marque só as células da linha de cima. A célula continua bloqueando " +
             "colocação de outras construções e spawn de recursos.")]
    public bool walkable = false;
}

[CreateAssetMenu(fileName = "ConstructionData", menuName = "Game/Construction")]
public class ConstructionData : ScriptableObject {
    [Tooltip("ID estável usado por objetivos, saves e desbloqueios. Se vazio, o nome do asset é usado como fallback.")]
    public string id;
    public string displayName;
    public BuildingController buildingPrefab;

    [Header("Tiles que aparecem visualmente (preview e colocação)")]
    public List<TileInfo> tilesVisual = new List<TileInfo>();

    [Header("Tiles que ocupam espaço (verificação de ocupação e de terreno)")]
    [Tooltip("Além de bloquearem as células, cada entrada define o terreno exigido " +
             "por essa célula (allowedTerrain). Offsets relativos ao canto base da construção.")]
    public List<TileInfo> tilesOccupied = new List<TileInfo>();

    [Header("Fase de obra (estado inicial da construção)")]
    [Tooltip("Layout com os materiais exigidos para erguer a construção. Slots com papel Input, travados no tipo do material. Vazio = a construção já nasce funcional.")]
    public SlotLayoutConfig constructionLayout;
    [Tooltip("Ticks necessários para concluir a obra depois que todos os materiais forem depositados.")]
    public int constructionTicks = 20;

    [Header("Configurações dos Layouts de Inventário")]
    public List<SlotLayoutConfig> availableLayouts = new List<SlotLayoutConfig>();
    public SlotLayoutConfig defaultSlotLayout;

    [Header("Movimentação durante a obra")]
    [Tooltip("Se true, a construção só se torna andável após concluída (ex.: ponte sobre água). Durante a obra, bloqueia passagem. Se false, os tiles são passáveis durante a obra.")]
    public bool walkableAfterConstruction = false;

    [Header("Extração (fonte de recurso)")]
    [Tooltip("Se marcado, a construção também responde ao bloco ExtrairMaterial, funcionando como um recurso. " +
             "Fonte de construção é infinita por definição: não tem estoque próprio, nunca esgota e nunca " +
             "é destruída pela extração (ex.: Mina de Pedra).")]
    public bool isExtractable = false;
    [Tooltip("Tipo do recurso: define com qual ferramenta ela pode ser extraída (ResourceType).")]
    public ResourceType extractableResourceType = ResourceType.None;
    [Tooltip("Material devolvido na extração (id da MaterialList). -1 = inválido.")]
    public int extractableMaterialTypeId = -1;
}
