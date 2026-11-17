using UnityEngine;
using UnityEngine.Tilemaps;

// Tile de chão que carrega o próprio tipo de terreno.
//
// A ideia é não manter um dicionário separado de "tile -> terreno": o dado fica
// no asset do tile. Assim, ao pintar o chão no Tile Palette, o tipo já acompanha
// o tile e continua correto mesmo se o asset for movido/renomeado.
//
// Tiles comuns (UnityEngine.Tilemaps.Tile) são lidos como TerrainType.Nenhum.
// Converta-os para TerrainTile quando quiser que participem das regras.
[CreateAssetMenu(fileName = "TerrainTile", menuName = "2D/Tiles/Terrain Tile")]
public class TerrainTile : Tile
{
    [Tooltip("Tipo de terreno desta célula. Usado na checagem de construção e de spawn de recursos.")]
    public TerrainType terrain = TerrainType.Nenhum;
}
