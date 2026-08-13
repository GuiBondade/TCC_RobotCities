using System;

public static class ScriptSignals
{
    public static event Action<ScriptData> OnCreateScript;
    public static event Action<ScriptData> OnDeleteScript;
    public static event Action<ScriptData> OnOpenScript;

    public static void TriggerScriptCreation(ScriptData script){
        OnCreateScript?.Invoke(script);
    }

    public static void TriggerScriptDeletion(ScriptData script){
        OnDeleteScript?.Invoke(script);
    }

    public static void TriggerScriptOpen(ScriptData script){
        OnOpenScript?.Invoke(script);
    }
}
