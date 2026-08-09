using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptSpawner : MonoBehaviour
{
    public ScriptManager scriptManager; 
    public ScriptInterface scriptUIPrefab;
    public Transform scriptContainer;
    public string defaultName;

    private int scriptCounter = 0;

    public void AddScript() {
        string name = defaultName;// função de não repetir nome (talvez reusar o de variavel, so que ter que passar lista tambem)
        var scriptAdded = new ScriptData() {
            id = scriptCounter,
            name = defaultName,
        };
        scriptCounter++;
        ScriptSignals.TriggerScriptCreation(scriptAdded);
        var scriptUI = Instantiate(scriptUIPrefab, scriptContainer);
        scriptUI.scriptData = scriptAdded;
    } 
}
