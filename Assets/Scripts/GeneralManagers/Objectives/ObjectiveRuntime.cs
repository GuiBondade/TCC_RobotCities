using UnityEngine;

// Estado mutável de um objetivo durante a partida. Nunca é salvo no ScriptableObject.
public enum ObjectiveState
{
    Locked,
    InProgress,
    Completed
}

public class ObjectiveRuntime
{
    public readonly ObjectiveData data;

    // Ordem original no catálogo, usada como desempate na ordenação.
    public int catalogIndex;
    public int progress;
    public ObjectiveState state;
    public bool activationProcessed;
    public bool completionProcessed;

    public ObjectiveRuntime(ObjectiveData data)
    {
        this.data = data;
    }

    public int Target => Mathf.Max(1, data != null ? data.targetAmount : 1);

    public bool IsLocked => state == ObjectiveState.Locked;
    public bool IsInProgress => state == ObjectiveState.InProgress;
    public bool IsCompleted => state == ObjectiveState.Completed;

    public string BuildProgressText()
    {
        return data != null ? data.BuildProgressText(progress) : string.Empty;
    }
}
