using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScriptSpawner : MonoBehaviour
{
    [SerializeField] private ScriptManager scriptManager; 
    [SerializeField] private ScriptInterface scriptUIPrefab;
    [SerializeField] private Transform scriptContainer;
    [SerializeField] private string defaultName;

    private int scriptCounter = 0;

    void Start() {
        AddScript();
    }

    public void AddScript() {
        string name = defaultName;// função de não repetir nome (talvez reusar o de variavel, so que ter que passar lista tambem)
        var scriptAdded = new ScriptData() {
            id = scriptCounter,
            name = defaultName,
        };
        ScriptSignals.TriggerScriptCreation(scriptAdded);
        scriptCounter++;
        var scriptInterface = Instantiate(scriptUIPrefab, scriptContainer);
        scriptInterface.Setup(scriptManager, scriptAdded);
    } 
}
