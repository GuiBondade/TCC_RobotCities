using System;

using ScriptRunner;

public static class RobotSignals
{
    public static event Action<RobotScript> OnActivateExecution; 
    public static event Action<RobotScript> OnDeactivateExecution; 
    public static event Action<ScriptData> OnScriptAttached;
    public static event Action<RobotScript> OnInterfaceOpen;

    public static void TriggerExecutionActivation(RobotScript robotScript){
        OnActivateExecution?.Invoke(robotScript);
    }

    public static void TriggerExecutionDeactivation(RobotScript robotScript){
        OnDeactivateExecution?.Invoke(robotScript);
    }

    public static void TriggerScriptAttach(ScriptData script){
        OnScriptAttached?.Invoke(script);
    }

    public static void TriggerInterfaceOpen(RobotScript robotScript){
        OnInterfaceOpen?.Invoke(robotScript);
    }
}
