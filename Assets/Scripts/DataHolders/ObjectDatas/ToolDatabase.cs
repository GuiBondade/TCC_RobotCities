using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ToolDatabase", menuName = "Inventory/Tool Database")]
public class ToolDatabase : ScriptableObject {
    [SerializeField] private List<ToolData> listTools = new List<ToolData>();

    public IReadOnlyList<ToolData> Tools => listTools;

    private Dictionary<int, ToolData> toolsById;

    public void Initialize() {
        toolsById = new Dictionary<int, ToolData>();

        foreach (var tool in listTools) {
            if (tool == null) continue;

            if (!toolsById.ContainsKey(tool.id)) {
                toolsById.Add(tool.id, tool);
            } else {
                Debug.LogWarning("ToolDatabase: id de ferramenta repetido: " + tool.id);
            }
        }
    }

    // Ferramenta pelo id (o id fica no currentTypeId do slot Tool).
    public ToolData GetById(int id) {
        if (toolsById == null) Initialize();

        if (toolsById.TryGetValue(id, out var tool)) return tool;

        Debug.LogWarning("ToolDatabase: ferramenta não encontrada (id " + id + ").");
        return null;
    }

    public ToolData TryGetByName(string displayName) {
        if (string.IsNullOrEmpty(displayName)) return null;
        if (toolsById == null) Initialize();
        foreach (var tool in listTools) {
            if (tool == null) continue;
            if (StringComparer.OrdinalIgnoreCase.Equals(tool.displayName, displayName)) return tool;
        }
        return null;
    }
}
