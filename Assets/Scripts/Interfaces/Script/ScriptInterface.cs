using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScriptInterface : MonoBehaviour
{
    private ScriptData scriptData;
    private ScriptSpawner spawner;
    [SerializeField] private TMP_Text nameLabel; // pra quando fora de foco
    [SerializeField] private TMP_InputField inputName;

    private ScriptManager scriptManager;

    private string uniqueName;

    public void Setup(ScriptManager manager, ScriptData script, ScriptSpawner scriptSpawner, string defaultName) {
        scriptManager = manager;
        scriptData = script;
        spawner = scriptSpawner;
        NameChanged(defaultName);
    }

    public void NameChanged(string name) {   
        spawner.namesList.Remove(scriptData.name);
        uniqueName = spawner.RegisterNewName(name);
        scriptData.name = uniqueName;
        inputName.text = uniqueName;
        nameLabel.text = uniqueName;
        if (scriptData == scriptManager.scriptOnFocus) scriptManager.ChangeInterfaceScriptName(uniqueName); 
    }

    public void ScriptDeleted() {
        ScriptSignals.TriggerDeletionInterfaceOpen(gameObject, scriptData);
    }

    public void ScriptOpened() {
        scriptManager.OpenScript(scriptData);
    }
}
