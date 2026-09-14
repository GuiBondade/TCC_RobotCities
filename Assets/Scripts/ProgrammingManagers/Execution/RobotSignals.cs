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
        OnInterfacePopulation?.Invoke(robotScript);
        OnInterfaceReposition?.Invoke(mousePosition);

    }
}
