using System;
using UnityEngine;

// Evento abstrato consumido pelos objetivos. O ObjectiveManager não consulta o mundo:
// cada sistema emite uma transição de estado (signal, tile, construção, inventário etc.)
// e o evento carrega o MapContext de origem para separar mapa principal e puzzles.
public readonly struct ObjectiveEvent
{
    public readonly ObjectiveEventType type;
    public readonly string key;
    public readonly int amount;
    public readonly MapContext originContext;

    public ObjectiveEvent(ObjectiveEventType type, string key, int amount = 1, MapContext originContext = null)
    {
        this.type = type;
        this.key = key ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
        this.originContext = originContext;
    }

    // PuzzleCompleted é um evento global: pode avançar objetivos do mapa principal.
    public bool IsGlobal => type == ObjectiveEventType.PuzzleCompleted;
}

// Barramento estático de entrada e saída dos objetivos.
// Entrada: ExecuteManager/objetos de jogo chamam TriggerEvent/TriggerSignal.
// Saída: a interface e outros sistemas escutam os eventos On...
public static class ObjectiveSignals
{
    public static event Action<ObjectiveEvent> OnEvent;

    public static event Action<ObjectiveData, int, int> OnObjectiveProgressChanged;
    public static event Action<ObjectiveData> OnObjectiveUnlocked;
    public static event Action<ObjectiveData> OnObjectiveCompleted;
    public static event Action<ObjectiveData, ObjectiveState> OnObjectiveStateChanged;
    public static event Action<ObjectiveUnlockType, string, ObjectiveData> OnUnlockGranted;
    public static event Action<ObjectiveScope, string, string> OnPinnedObjectiveChanged;
    public static event Action OnObjectivesReloaded;

    public static void TriggerEvent(ObjectiveEvent evt)
    {
        if (string.IsNullOrEmpty(evt.key)) return;
        OnEvent?.Invoke(evt);
    }

    public static void TriggerSignal(string signalName, int amount = 1, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(ObjectiveEventType.Signal, signalName, amount, originContext));
    }

    // Chave canônica para objetivos de tile. Mantém o formato em um único lugar
    // para que emissores e configuração usem exatamente a mesma string.
    public static string BuildTileKey(Vector3Int cell)
    {
        return "tile:" + cell.x + "," + cell.y;
    }

    // Contexto do ambiente atual. Eventos de UI (workspace) usam este contexto
    // para que um puzzle aberto não alimente objetivos do mapa principal.
    public static MapContext ResolveActiveMapContext()
    {
        return MapManager.instancia != null ? MapManager.instancia.ActiveContext : null;
    }

    public static void TriggerRobotEnteredTile(Vector3Int cell, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.RobotEnteredTile, BuildTileKey(cell), 1, originContext));
    }

    public static void TriggerRobotExitedTile(Vector3Int cell, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.RobotExitedTile, BuildTileKey(cell), 1, originContext));
    }

    public static void TriggerConstructionPlaced(string constructionId, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ConstructionPlaced, constructionId, 1, originContext));
    }

    public static void TriggerConstructionCompleted(string constructionId, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ConstructionCompleted, constructionId, 1, originContext));
    }

    public static void TriggerResourceExtracted(int materialTypeId, int amount, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ResourceExtracted, materialTypeId.ToString(), amount, originContext));
    }

    public static void TriggerItemStored(int materialTypeId, int amount, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ItemStored, materialTypeId.ToString(), amount, originContext));
    }

    public static void TriggerItemTaken(int materialTypeId, int amount, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ItemTaken, materialTypeId.ToString(), amount, originContext));
    }

    public static void TriggerToolCrafted(int toolId, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.ToolCrafted, toolId.ToString(), 1, originContext));
    }

    public static void TriggerBlockPlacedInWorkspace(string blockId, MapContext originContext = null)
    {
        TriggerEvent(new ObjectiveEvent(
            ObjectiveEventType.BlockPlacedInWorkspace, blockId, 1, originContext));
    }

    internal static void RaiseProgressChanged(ObjectiveData data, int current, int target)
    {
        OnObjectiveProgressChanged?.Invoke(data, current, target);
    }

    internal static void RaiseObjectiveUnlocked(ObjectiveData data)
    {
        OnObjectiveUnlocked?.Invoke(data);
    }

    internal static void RaiseObjectiveCompleted(ObjectiveData data)
    {
        OnObjectiveCompleted?.Invoke(data);
    }

    internal static void RaiseStateChanged(ObjectiveData data, ObjectiveState state)
    {
        OnObjectiveStateChanged?.Invoke(data, state);
    }

    internal static void RaiseUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source)
    {
        OnUnlockGranted?.Invoke(type, key, source);
    }

    internal static void RaisePinnedObjectiveChanged(ObjectiveScope scope, string puzzleId, string objectiveId)
    {
        OnPinnedObjectiveChanged?.Invoke(scope, puzzleId, objectiveId);
    }

    internal static void RaiseObjectivesReloaded()
    {
        OnObjectivesReloaded?.Invoke();
    }

    // Domain reload e scene reload estão desativados no projeto.
    // Sem isso, assinantes e eventos da sessão anterior sobreviveriam ao Play Mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnEvent = null;
        OnObjectiveProgressChanged = null;
        OnObjectiveUnlocked = null;
        OnObjectiveCompleted = null;
        OnObjectiveStateChanged = null;
        OnUnlockGranted = null;
        OnPinnedObjectiveChanged = null;
        OnObjectivesReloaded = null;
    }
}
