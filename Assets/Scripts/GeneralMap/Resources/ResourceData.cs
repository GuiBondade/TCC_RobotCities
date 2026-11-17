using System.Collections.Generic;
using UnityEngine;

// Tipo do recurso: define com qual ferramenta ele pode ser extraído.
// Fica em código (e não em asset) pra não ter id solto digitado à mão.
public enum ResourceType {
    None = 0,
    Minerio = 1,
    Lenha = 2,
    Pedra = 3,
}

// Tipo de recurso (minério, lenha, etc). O asset descreve o tipo; a instância no
// mapa é um GameObject com ResourceController (estoque, ocupação e extração).
[CreateAssetMenu(fileName = "ResourceData", menuName = "Game/Resource")]
public class ResourceData : ScriptableObject {
    public string displayName;

    [Header("Identidade")]
    public ResourceType resourceType = ResourceType.None; // tipo do recurso — define a compatibilidade com a ferramenta
    public int typeId;         // material devolvido na extração (0=madeira, 1=pedra, 2=ferro, 3=carvao)

    [Header("Estoque")]
    public int totalAmount = 100;

    [Header("Prefab e tiles")]
    public ResourceController resourcePrefab;

    [Tooltip("Tiles que aparecem no mapa. Os offsets são relativos à célula âncora do recurso " +
             "(a célula passada no Initialize).")]
    public List<TileInfo> tilesVisual = new List<TileInfo>();

    [Tooltip("Células ocupadas. Vazio = o recurso ocupa exatamente as células do tilesVisual.")]
    public List<TileInfo> tilesOccupied = new List<TileInfo>();

    // Células do footprint com as exigências de terreno. Usa tilesOccupied quando
    // configurado; senão cai nos tiles visuais. Lista vazia = só a célula âncora.
    // É a mesma regra usada pelo ResourceController e pelo ResourceSpawnManager.
    public List<TileInfo> GetFootprintTiles() {
        var source = (tilesOccupied != null && tilesOccupied.Count > 0) ? tilesOccupied : tilesVisual;

        var result = source != null ? new List<TileInfo>(source) : new List<TileInfo>();
        if (result.Count == 0) result.Add(new TileInfo { offset = Vector2Int.zero });
        return result;
    }

    // Offsets do footprint (células que o recurso ocupa).
    public List<Vector2Int> GetFootprintOffsets() {
        var result = new List<Vector2Int>();
        foreach (var tile in GetFootprintTiles()) result.Add(tile.offset);
        return result;
    }

}
