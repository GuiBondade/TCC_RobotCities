using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Ferramentas de edição dos assets de mapa (ConstructionData e ResourceData).
// Captura o desenho pronto de um Tilemap da cena (ex.: RascunhoConstrucoes).
internal static class TileListGUI
{
    // Estado da UI do inspector (persiste enquanto o asset está selecionado).
    internal class State
    {
        public TerrainType terrain = TerrainType.Nenhum;

        public Tilemap source;
        public bool sourceSearched; // já tentou achar o tilemap de origem padrão?
    }

    // Nome padrão do Tilemap de rascunho usado como fonte dos tiles da entidade.
    private const string DefaultSourceName = "RascunhoConstrucoes";

    // Último tilemap de origem usado (fica salvo entre sessões do editor).
    private const string SourcePrefsKey = "TCC_RobotCities.TilemapOrigem";

    public static void Draw(UnityEngine.Object asset, State state, List<TileInfo> visual, List<TileInfo> occupied)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ferramenta de tiles", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Capture o desenho de um Tilemap (ex.: RascunhoConstrucoes).\n" +
            "Construção: visual = o que aparece, ocupados = o que bloqueia.\n" +
            "Recurso: ocupados vazio = ocupa as mesmas células do visual.\n" +
            "A profundidade (camada) é calculada na colocação, pelo offset.y de cada tile.\n" +
            "Terreno: marque o tipo exigido e use os botões abaixo para aplicar em todas " +
            "as células de uma vez; depois ajuste casos mistos (ex.: mina com parte em " +
            "montanha e parte em grama) na lista do Inspector.",
            MessageType.None);

        // Primeira abertura do inspector: já preenche o tilemap de origem padrão.
        if (state.source == null && !state.sourceSearched) {
            state.source = FindDefaultSource();
            state.sourceSearched = true;
        }

        // --- terreno ---
        state.terrain = (TerrainType)EditorGUILayout.EnumFlagsField("Terreno exigido", state.terrain);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aplicar terreno aos ocupados")) {
            ApplyTerrain(asset, occupied, state.terrain, "Aplicar terreno aos ocupados");
        }
        if (GUILayout.Button("Aplicar terreno aos visuais")) {
            ApplyTerrain(asset, visual, state.terrain, "Aplicar terreno aos visuais");
        }
        if (GUILayout.Button("Limpar terreno")) {
            ApplyTerrain(asset, occupied, TerrainType.Nenhum, "Limpar terreno dos ocupados");
            ApplyTerrain(asset, visual, TerrainType.Nenhum, "Limpar terreno dos visuais");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // --- captura do tilemap ---
        state.source = (Tilemap)EditorGUILayout.ObjectField("Tilemap origem", state.source, typeof(Tilemap), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capturar visual do tilemap")) {
            CaptureInto(asset, visual, state, "Capturar tiles visuais");
        }
        if (GUILayout.Button("Capturar ocupados do tilemap")) {
            CaptureInto(asset, occupied, state, "Capturar tiles ocupados");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // --- atalhos ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ocupados = Visual")) {
            Replace(asset, occupied, Clone(visual), "Copiar tiles visuais");
        }
        if (GUILayout.Button("Limpar visual")) {
            Replace(asset, visual, new List<TileInfo>(), "Limpar tiles visuais");
        }
        if (GUILayout.Button("Limpar ocupados")) {
            Replace(asset, occupied, new List<TileInfo>(), "Limpar tiles ocupados");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Total: " + visual.Count + " visual, " + occupied.Count + " ocupado(s).",
            EditorStyles.miniLabel);
    }

    // Lê todos os tiles pintados do Tilemap e normaliza os offsets (menor canto = 0,0).
    private static List<TileInfo> Capture(Tilemap map) {
        var result = new List<TileInfo>();
        if (map == null) return result;

        var bounds = map.cellBounds;
        var cells = new List<Vector3Int>();
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (var pos in bounds.allPositionsWithin) {
            if (map.GetTile(pos) == null) continue;

            cells.Add(pos);
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
        }

        foreach (var pos in cells) {
            result.Add(new TileInfo {
                offset = new Vector2Int(pos.x - minX, pos.y - minY),
                tile = map.GetTile(pos),
            });
        }

        return result;
    }

    // Tilemap de origem padrão: o último usado (EditorPrefs) ou um Tilemap com o
    // nome padrão na cena aberta. Evita ter que arrastar o tilemap toda vez.
    private static Tilemap FindDefaultSource() {
        string wanted = EditorPrefs.GetString(SourcePrefsKey, DefaultSourceName);
        if (string.IsNullOrEmpty(wanted)) return null;

        foreach (var map in UnityEngine.Object.FindObjectsOfType<Tilemap>(true)) {
            if (map.name == wanted) return map;
        }
        return null;
    }

    private static void CaptureInto(UnityEngine.Object asset, List<TileInfo> target, State state, string undoName) {
        if (state.source == null) {
            EditorUtility.DisplayDialog("Tilemap origem", "Selecione o Tilemap de onde os tiles vão ser copiados.", "OK");
            return;
        }

        // Guarda o tilemap usado, pra próxima captura já vir preenchida.
        EditorPrefs.SetString(SourcePrefsKey, state.source.name);

        var captured = Capture(state.source);
        if (captured.Count == 0) {
            EditorUtility.DisplayDialog("Tilemap vazio",
                "O Tilemap '" + state.source.name + "' não tem nenhum tile pintado.", "OK");
            return;
        }

        Replace(asset, target, captured, undoName);
    }

    // Troca o conteúdo da lista (com undo e marcando o asset como alterado).
    private static void Replace(UnityEngine.Object asset, List<TileInfo> target, List<TileInfo> items, string undoName) {
        Undo.RecordObject(asset, undoName);
        target.Clear();
        target.AddRange(items);
        EditorUtility.SetDirty(asset);
    }

    // Aplica o terreno exigido em todas as células de uma lista (com undo).
    private static void ApplyTerrain(UnityEngine.Object asset, List<TileInfo> target, TerrainType terrain, string undoName) {
        Undo.RecordObject(asset, undoName);
        foreach (var tile in target) {
            tile.allowedTerrain = terrain;
        }
        EditorUtility.SetDirty(asset);
    }

    // Cópia rasa campo a campo: "Ocupados = Visual" não compartilha os mesmos TileInfo.
    private static List<TileInfo> Clone(List<TileInfo> source) {
        var result = new List<TileInfo>(source.Count);
        foreach (var t in source) {
            result.Add(new TileInfo {
                offset = t.offset,
                tile = t.tile,
                allowedTerrain = t.allowedTerrain,
                walkable = t.walkable,
            });
        }
        return result;
    }
}

[CustomEditor(typeof(ConstructionData))]
public class ConstructionDataEditor : Editor
{
    private readonly TileListGUI.State _state = new TileListGUI.State();

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        var data = (ConstructionData)target;
        TileListGUI.Draw(target, _state, data.tilesVisual, data.tilesOccupied);
    }
}

[CustomEditor(typeof(ResourceData))]
public class ResourceDataEditor : Editor
{
    private readonly TileListGUI.State _state = new TileListGUI.State();

    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        var data = (ResourceData)target;
        TileListGUI.Draw(target, _state, data.tilesVisual, data.tilesOccupied);
    }
}
