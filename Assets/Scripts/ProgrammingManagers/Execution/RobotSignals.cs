using System;

using ScriptRunner;

public static class RobotSignals
{
    public static event Action<ExecutionContext> OnActivateExecution; 
    public static event Action<ExecutionContext> OnDeactivateExecution; 
    public static event Action<ScriptData> OnScriptAttached;

    public static void TriggerExecutionActivation(ExecutionContext context){
        OnActivateExecution?.Invoke(context);
    }

    public static void TriggerExecutionDeactivation(ExecutionContext context){
        OnDeactivateExecution?.Invoke(context);
    }

    public static void TriggerScriptAttach(ScriptData script){
        OnScriptAttached?.Invoke(script);
    }
}
