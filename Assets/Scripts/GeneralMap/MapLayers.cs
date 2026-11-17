using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Fonte única dos tilemaps das entidades do mapa (construções e recursos).
//
// O PlacementManager e o ResourceController (spawn de recursos) pintam por aqui, em
// vez de cada um guardar a própria referência pros tilemaps. O componente fica no
// GameObject "MapManagers" (ou num filho dele) e é acessado por MapManager.layers.
//
// Profundidade (depth sorting): a camada não fica no asset da entidade. Ela sai do
// próprio formato dela: a fileira de Y mais baixa (inferior) começa na menor camada,
// e as fileiras acima vão crescendo. Como o cálculo usa o offset relativo, o
// resultado não depende de onde a entidade é colocada no mapa.
public class MapLayers : MonoBehaviour
{
    // Só deve ser usado no encerramento do aplicativo. O descarregamento de uma cena
    // aditiva (ex.: puzzle) não pode marcar shutdown global: o mapa principal continua vivo.
    public static bool IsApplicationQuitting { get; private set; }

    // Máximo de fileiras de Y que uma entidade pode ter. Cada fileira usa um Tilemap
    // visual: 5 fileiras = camadas 1..5.
    public const int MaxVisualLayers = 5;

    // Verdadeiro enquanto ESTE mapa está sendo descarregado.
    public bool IsUnloading { get; private set; }

    [Header("Tilemaps do mapa")]
    [Tooltip("Camada 0: converte mouse em célula e serve de referência de posição pras entidades.")]
    [SerializeField] public Tilemap collisionTilemap;

    [Tooltip("Camada 1: fileira inferior (menor Y).")]
    [SerializeField] public Tilemap visualLayer1;
    [SerializeField] public Tilemap visualLayer2;
    [SerializeField] public Tilemap visualLayer3;
    [SerializeField] public Tilemap visualLayer4;
    [Tooltip("Camada 5: fileira superior (maior Y).")]
    [SerializeField] public Tilemap visualLayer5;

    [Tooltip("Tilemap separado do preview fantasma (posicionamento e destruição).")]
    [SerializeField] public Tilemap previewTilemap;

    [Header("Overlay (props fixos que bloqueiam construção/spawn)")]
    [Tooltip("Tilemap de overlay (ex.: montanha diagonal, rio, farlands). É puramente visual: não bloqueia colocação nem spawn. O bloqueio vem do terreno do chão (collisionTilemap) e das entidades registradas. Não define terreno — o terreno vem do chão.")]
    [SerializeField] public Tilemap overlayTilemap;

    [Header("Terreno")]
    [Tooltip("Tilemap do chão usado na checagem de terreno. Vazio = usa o collisionTilemap " +
             "(no mapa atual o chão é o próprio collisionTilemap).")]
    [SerializeField] public Tilemap terrainTilemap;

    // Camadas que já avisaram estar sem Tilemap configurado (evita spam no console).
    private readonly HashSet<int> warnedMissingLayers = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        IsApplicationQuitting = false;
    }

    private void Awake() {
        IsUnloading = false;
    }

    private void OnApplicationQuit() {
        IsApplicationQuitting = true;
    }

    private void OnDestroy() {
        IsUnloading = true;
    }

    // Chamado pelo gerenciador do puzzle antes de descarregar a cena. Antecipar a
    // marca evita que recursos tentem des-pintar tiles de um mapa que já está indo embora.
    public void MarkUnloading() {
        IsUnloading = true;
    }

    // Existe chão pintado nesta célula? A checagem evita colocar/destruir construções
    // em células vazias de um mapa reutilizável sem terreno.
    public bool HasFloor(Vector3Int pos) {
        Tilemap map = TerrainTilemap;
        return map != null && map.HasTile(pos);
    }

    // Tilemap visual de uma camada (1..MaxVisualLayers). Camada inválida cai na 1.
    public Tilemap GetVisualLayer(int layer) {
        switch (layer) {
            case 2: return visualLayer2;
            case 3: return visualLayer3;
            case 4: return visualLayer4;
            case 5: return visualLayer5;
            default: return visualLayer1;
        }
    }

    // Pinta (ou limpa, passando tile nulo) um tile na camada visual indicada.
    public void SetVisualTile(int layer, Vector3Int pos, TileBase tile, Color color) {
        Tilemap map = GetVisualLayer(layer);
        if (map == null) {
            if (tile != null && warnedMissingLayers.Add(layer)) {
                Debug.LogWarning("[MapLayers] Camada visual " + layer + " sem Tilemap configurado: os tiles dessa fileira não vão aparecer.", this);
            }
            return;
        }

        map.SetTile(pos, tile);
        map.SetColor(pos, color);
    }

    // Tilemap que representa o chão. O campo terrainTilemap é opcional: quando
    // vazio, usa o collisionTilemap (é assim que a cena atual está montada).
    public Tilemap TerrainTilemap => terrainTilemap != null ? terrainTilemap : collisionTilemap;

    // Tipo de terreno de uma célula. Tiles comuns (sem TerrainTile) devolvem
    // Nenhum, ou seja, só passam em requisitos sem exigência de terreno.
    public TerrainType GetTerrainAt(Vector3Int pos) {
        Tilemap map = TerrainTilemap;
        if (map == null) return TerrainType.Nenhum;

        TileBase tile = map.GetTile(pos);
        return tile is TerrainTile terrainTile ? terrainTile.terrain : TerrainType.Nenhum;
    }

    // Célula coberta por um prop no tilemap de overlay (montanha diagonal, rio,
    // farlands...). É puramente visual: não bloqueia colocação nem spawn. O
    // bloqueio vem do terreno do chão (collisionTilemap) e das entidades
    // registradas. Mantido para consultas de render/edição, não para ocupação.
    public bool IsOverlayTile(Vector3Int pos) {
        if (overlayTilemap == null) return false;
        return overlayTilemap.GetTile(pos) != null;
    }

    // Camada de profundidade de uma fileira de Y, relativa à própria entidade:
    // a fileira inferior (menor Y) começa na menor camada, e a camada cresce
    // conforme as fileiras sobem (maior Y). minY é o menor offset.y da entidade
    // (base do cálculo).
    public static int DepthLayer(int offsetY, int minY) {
        return Mathf.Clamp(1 + (offsetY - minY), 1, MaxVisualLayers);
    }

    // Menor offset.y de uma lista de tiles. Devolve 0 pra lista vazia/nula.
    public static int MinOffsetY(List<TileInfo> tiles) {
        if (tiles == null || tiles.Count == 0) return 0;

        int min = tiles[0].offset.y;
        for (int i = 1; i < tiles.Count; i++) {
            if (tiles[i].offset.y < min) min = tiles[i].offset.y;
        }
        return min;
    }
}
