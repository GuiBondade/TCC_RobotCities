using System.Collections.Generic;
using UnityEngine;

// Lista central de objetivos ativos. O manager carrega este asset no Inspector.
[CreateAssetMenu(menuName = "Objectives/Objective Catalog", fileName = "ObjectiveCatalog")]
public class ObjectiveCatalog : ScriptableObject
{
    [SerializeField] private List<ObjectiveData> objectives = new List<ObjectiveData>();

    public IReadOnlyList<ObjectiveData> Objectives => objectives;

    public ObjectiveData GetById(string id)
    {
        if (string.IsNullOrEmpty(id) || objectives == null) return null;

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveData objective = objectives[i];
            if (objective != null && objective.id == id) return objective;
        }
        return null;
    }

    private void OnValidate()
    {
        if (objectives == null) objectives = new List<ObjectiveData>();
    }
}
