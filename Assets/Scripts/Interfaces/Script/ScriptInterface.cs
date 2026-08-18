using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScriptInterface : MonoBehaviour
{
    private ScriptData scriptData;
    [SerializeField] private TMP_Text nameLabel; // pra quando fora de foco

    private ScriptManager scriptManager;

    public void Setup(ScriptManager manager, ScriptData script) {
        scriptManager = manager;
        scriptData = script;
    }

    public void NameChanged(string name) {
        string newName = name;// função de não repetir nome (talvez reusar o de variavel, so que ter que passar lista tambem)
        scriptData.name = newName;
        nameLabel.text = newName;
    }

    public void ScriptDeleted() {
        ScriptSignals.TriggerDeletionInterfaceOpen(gameObject, scriptData);
    }

    public void ScriptOpened() {
        scriptManager.OpenScript(scriptData);
    }
}
