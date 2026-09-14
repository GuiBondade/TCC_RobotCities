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

    public List<string> namesList = new List<string>();
    private string newScriptName;

    void Start() {
        ScriptSignals.OnDeleteInterfaceAccepted += DeleteScript;
        
        AddScript();
    }

    public void AddScript() {
        var scriptAdded = new ScriptData() {
            id = scriptCounter,
        };
        ScriptSignals.TriggerScriptCreation(scriptAdded);
        scriptCounter++;
        var scriptInterface = Instantiate(scriptUIPrefab, scriptContainer);
        scriptInterface.Setup(scriptManager, scriptAdded, this, defaultName);
    } 

    public void DeleteScript(GameObject scriptGO, ScriptData scriptData) {
        namesList.Remove(scriptData.name);
        scriptManager.DeleteScript(scriptData);
        Destroy(scriptGO);
        if (scriptManager.scripts.Count < 1) {
            AddScript();
        }
    }

    public string RegisterNewName(string name) {
        newScriptName = StringUtils.EnsureUniqueString(name, namesList, defaultName);
        namesList.Add(newScriptName);
        return newScriptName;
    }
}
