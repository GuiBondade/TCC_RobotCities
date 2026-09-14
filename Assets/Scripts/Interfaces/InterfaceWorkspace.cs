using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class InterfaceWorkspace : MonoBehaviour
{
    [SerializeField] private Button saveBtn;
    [SerializeField] private Button closeBtn;
    [SerializeField] private TMP_Text scriptLabel; 
    [SerializeField] private GameObject workspace;

    private ScriptManager scriptManager;
    public static InterfaceWorkspace instancia;

    public event Action OnScriptSaved;

    void Start() {
        instancia = this;
        scriptManager = ScriptManager.instancia;

        scriptManager.ScriptInterfaceStateChanged += ScriptUpdate;
        scriptManager.OnScriptClosed += ScriptClosed;
    }

    private void ScriptUpdate(string scriptName) {
        scriptLabel.text = scriptName;
        workspace.SetActive(true);
    }

    public void ScriptSaved() {
        OnScriptSaved?.Invoke();
    }

    public void ScriptClosed() {
        scriptManager.OpenScript(null);
        workspace.SetActive(false);
    }
}
