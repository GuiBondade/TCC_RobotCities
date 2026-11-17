using UnityEditor;
using UnityEngine;

// O editor padrão de Tile não desenha campos de subclasses. Este editor mantém
// os campos do Tile (sprite, cor, collider...) e mostra também o tipo de terreno,
// tanto no Inspector do asset quanto ao selecionar o tile no Tile Palette.
[CustomEditor(typeof(TerrainTile))]
public class TerrainTileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "O tipo de terreno é lido pelo MapLayers quando uma construção ou um " +
            "recurso verifica se pode ocupar a célula.\n\n" +
            "Deixe 'Nenhum' apenas em tiles que não representam chão (ou que ainda " +
            "não foram classificados).",
            MessageType.Info);
    }
}
