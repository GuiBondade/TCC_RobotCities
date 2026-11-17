using UnityEngine;

// Contexto de um mapa carregado. O MapManager continua sendo o singleton global,
// mas cada cena de mapa (principal ou puzzle aditivo) tem o seu MapContext com os
// tilemaps e os containers das entidades daquele mapa.
public class MapContext : MonoBehaviour
{
    [Header("Identificação")]
    [Tooltip("Nome lógico do mapa (ex.: principal, puzzle_1).")]
    [SerializeField] public string mapId = "map";
    [Tooltip("Marque no contexto da MapScene. O puzzle fica desmarcado.")]
    [SerializeField] public bool isMain;

    [Header("Tilemaps")]
    [SerializeField] public MapLayers layers;

    [Header("Organização das entidades")]
    [SerializeField] public Grid grid;
    [Tooltip("Pai das construções colocadas neste mapa.")]
    [SerializeField] public Transform buildingContent;
    [Tooltip("Pai dos recursos instanciados neste mapa.")]
    [SerializeField] public Transform resourceContainer;
    [Tooltip("Pai dos robôs instanciados neste mapa.")]
    [SerializeField] public Transform robotContainer;

    public bool IsUnloading { get; private set; }

    private void Awake() {
        EnsureReferences();
    }

    // Resolve referências que podem ter ficado vazias no Inspector/prefab.
    public void EnsureReferences() {
        if (layers == null) layers = GetComponentInChildren<MapLayers>(true);
        if (grid == null) grid = GetComponentInChildren<Grid>(true);

        if (buildingContent == null) buildingContent = FindChild("BuildingContent", "Constructions", "Construcoes");
        if (resourceContainer == null) resourceContainer = FindChild("ResourceContainer", "Resources", "Recursos");
        if (robotContainer == null) robotContainer = FindChild("RobotContainer", "Robots", "Robos");
    }

    // Marca o contexto inteiro como em descarregamento. Precisa acontecer antes do
    // UnloadSceneAsync para os recursos não tentarem repintar um mapa que já saiu.
    public void MarkUnloading() {
        IsUnloading = true;
        if (layers != null) layers.MarkUnloading();
    }

    private Transform FindChild(params string[] names) {
        if (names == null) return null;

        foreach (Transform child in transform) {
            foreach (string candidate in names) {
                if (child.name == candidate) return child;
            }
        }
        return null;
    }
}
