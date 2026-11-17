using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScriptSaveJSON : MonoBehaviour
{
    public void SaveAsJson(SaveNodesData nodes, string name){
        if (nodes == null) {
            Debug.LogError("[ScriptSaveJSON] Não é possível salvar dados nulos.", this);
            return;
        }
        if (string.IsNullOrWhiteSpace(name)) {
            Debug.LogError("[ScriptSaveJSON] Não é possível salvar com nome vazio.", this);
            return;
        }

        // salvo em: C:\Users\<seu_usuario>\AppData\LocalLow\<CompanyName>[DefaultCompany*]\<ProductName>\workspace.json
        string json = JsonUtility.ToJson(nodes, true); // true = readability (tira quando salvar de verdade)
        var folder = Application.persistentDataPath;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{name}.json");
        File.WriteAllText(path, json);
        Debug.Log("Script salvo em: " + path);
    }

    public void DeleteJson(string name) {
        string filePath = Path.Combine(Application.persistentDataPath, $"{name}.json");
        if (File.Exists(filePath)) {
            File.Delete(filePath);
        } else {
            Debug.Log("Arquivo não encontrado: " + name);
        }
    }
}
