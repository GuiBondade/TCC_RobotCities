using System;
using System.IO;
using UnityEngine;

// Persistência mínima em um único arquivo JSON no persistentDataPath.
// Não salva o estado do mundo dos puzzles; apenas progresso de objetivos e metadados do puzzle.
public static class ObjectiveProgressStore
{
    public const string DefaultFolder = "Objectives";
    public const string DefaultFileName = "objectives.json";

    public static string GetPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) fileName = DefaultFileName;

        // Subpasta dedicada para não colidir com os JSONs de scripts,
        // que usam Application.persistentDataPath/<nomeDoScript>.json.
        return Path.Combine(Application.persistentDataPath, DefaultFolder, fileName);
    }

    public static ObjectiveProgressData Load(string fileName)
    {
        string path = GetPath(fileName);
        if (!File.Exists(path)) return new ObjectiveProgressData();

        try
        {
            string json = File.ReadAllText(path);
            ObjectiveProgressData data = JsonUtility.FromJson<ObjectiveProgressData>(json);
            if (data == null) return new ObjectiveProgressData();
            data.EnsureLists();
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Objectives] Falha ao carregar '{path}': {exception.Message}");
            return new ObjectiveProgressData();
        }
    }

    public static void Save(ObjectiveProgressData data, string fileName)
    {
        if (data == null) return;

        string path = GetPath(fileName);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        try
        {
            data.EnsureLists();
            string json = JsonUtility.ToJson(data, true);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporaryPath, path, null);
                }
                catch (Exception)
                {
                    // Fallback para plataformas que não suportam File.Replace.
                    File.Copy(temporaryPath, path, true);
                    File.Delete(temporaryPath);
                }
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Objectives] Falha ao salvar '{path}': {exception.Message}");
        }
    }

    public static void Delete(string fileName)
    {
        string path = GetPath(fileName);
        if (!File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Objectives] Falha ao apagar '{path}': {exception.Message}");
        }
    }
}
