using System;
using UnityEngine;
using ScriptRunner;

public static class RobotSignals
{
    public static event Action<RobotScript> OnActivateExecution; 
    public static event Action<RobotScript> OnDeactivateExecution; 
    public static event Action<ScriptData> OnScriptAttached;
    public static event Action<RobotScript> OnInterfacePopulation;
    public static event Action<Vector2> OnInterfaceReposition;
    public static event Action OnSpawnRobotRequested;
    public static event Action<RobotScript> OnRobotSpawned;

    public static void TriggerExecutionActivation(RobotScript robotScript){
        OnActivateExecution?.Invoke(robotScript);
    }

    public static void TriggerExecutionDeactivation(RobotScript robotScript){
        OnDeactivateExecution?.Invoke(robotScript);
    }

    public static void TriggerScriptAttach(ScriptData script){
        OnScriptAttached?.Invoke(script);
    }

    public static void TriggerInterfaceOpen(RobotScript robotScript, Vector2 mousePosition){
        InvokeAlive(OnInterfacePopulation, callback => callback(robotScript),
            callback => OnInterfacePopulation -= callback);
        InvokeAlive(OnInterfaceReposition, callback => callback(mousePosition),
            callback => OnInterfaceReposition -= callback);

    }

    public static void TriggerSpawnRobotRequested() {
        OnSpawnRobotRequested?.Invoke();
    }

    public static void TriggerRobotSpawned(RobotScript robotScript) {
        OnRobotSpawned?.Invoke(robotScript);
    }

    private static void InvokeAlive<T>(Action<T> callbacks, Action<Action<T>> invoke, Action<Action<T>> remove) {
        if (callbacks == null) return;

        foreach (var callback in callbacks.GetInvocationList()) {
            var typedCallback = (Action<T>)callback;
            var target = typedCallback.Target as UnityEngine.Object;
            if (typedCallback.Target != null && target == null) {
                remove(typedCallback);
                continue;
            }

            invoke(typedCallback);
        }
    }
}
