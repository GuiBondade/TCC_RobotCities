using System;
using UnityEngine;

public static class ScriptSignals
{
    public static event Action<ScriptData> OnCreateScript;
    public static event Action<GameObject, ScriptData> OnDeleteInterfaceOpen;
    public static event Action<GameObject, ScriptData> OnDeleteInterfaceAccepted;

    public static void TriggerScriptCreation(ScriptData script){
        OnCreateScript?.Invoke(script);
    }

    public static void TriggerDeletionInterfaceOpen(GameObject scriptGO, ScriptData scriptData){
        OnDeleteInterfaceOpen?.Invoke(scriptGO, scriptData);
    }

    public static void TriggerDeletionInterfaceAccepted(GameObject scriptGO, ScriptData scriptData){
        OnDeleteInterfaceAccepted?.Invoke(scriptGO, scriptData);
    }
}
