using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

using ScriptRunner;

public struct VariableData{
    public string varName;
    public string defaultValue;
}
public class ScriptData // vai ser preenchido na inicialização e no save
{
    public int id;
    public string name;
    public SaveNodesData nodes; 
    public List<ExecutionBlock> functionsRoot; 
    public List<VariableData> vars;
    // int version; caso o id do script sendo compilado ja existir pega a version salva e seta no novo como version = oldVersion++
}

public class ScriptManager : MonoBehaviour // arquivo set/delete json
{
    public static ScriptManager instancia { get; private set; }

    [SerializeField] private ScriptSaveJSON jsonBuilder;

    public ScriptData scriptOnFocus;
    public List<ScriptData> scripts = new List<ScriptData>();

    public event Action<SaveNodesData> OnScriptOpen; 
    public event Action OnScriptClosed; 
    public event Action<string> ScriptInterfaceStateChanged; 
    
    void Awake() {
        if (instancia == null) instancia = this;
        else if (instancia != this) {
            Debug.Log("Duplicata desnecessária de ScriptManager destruída");
            Destroy(gameObject);
        }

        ScriptSignals.OnCreateScript += CreateScript;
    }

    private void OnDestroy()
    {
        ScriptSignals.OnCreateScript -= CreateScript;
    }

    public void SaveScript(SaveNodesData data) // save (set)
    {
        Debug.Log("salvando script: " + scriptOnFocus.id);
        scriptOnFocus.nodes = data;
        jsonBuilder.SaveAsJson(scriptOnFocus.nodes, scriptOnFocus.id.ToString());
        Compile(scriptOnFocus);
    }

    private void Compile(ScriptData script) {
        BlockCompiler compiler = new BlockCompiler();
        ExecutionBlock root = compiler.Compile(script.nodes, 0/* esse 0 ta bem errado, tem que ser o id do primeiro */);// tem que considerar uma lista de primeiros blocos
        List<ExecutionBlock> roots = new List<ExecutionBlock>();
        roots.Add(root);
        List<VariableData> varList = new List<VariableData>();
        foreach(CreateVarNode var in script.nodes.variaveisData) {
            varList.Add(new VariableData(){
                varName = var.varName,
                defaultValue = var.defaultValue
            }
            );
        } 

        script.functionsRoot = roots;
        script.vars = varList;
    }

    public void OpenScript(ScriptData script)
    {
        Debug.Log("Build blocks from script: " + script?.id);
        SaveNodesData nodes = null;
        if (script != null) nodes = script.nodes;
        OnScriptOpen?.Invoke(nodes);
        ChangeInterfaceScriptName(script?.name);
        scriptOnFocus = script;
    }

    public void ChangeInterfaceScriptName(string name) {
        ScriptInterfaceStateChanged?.Invoke(name);
    }

    public void DeleteScript(ScriptData script)
    {
        if (script == scriptOnFocus) OnScriptClosed?.Invoke();
        scripts.Remove(script);
        jsonBuilder.DeleteJson(script.id.ToString());
    }

    public void CreateScript(ScriptData script)
    {
        scripts.Add(script);
    }
}
