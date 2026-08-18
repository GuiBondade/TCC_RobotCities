using UnityEngine;
using UnityEngine.UI;

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

    public WorkspaceBuilder graphBuilder;
    public ScriptJSONBuilder jsonBuilder;

    private ScriptData scriptOnFocus;
    public List<ScriptData> scripts = new List<ScriptData>();
    
    void Awake() {
        if (instancia == null) instancia = this;
        else if (instancia != this) {
            Debug.Log("Duplicata desnecessária de ScriptManager destruída");
            Destroy(gameObject);
        }
    }

    void Start() {
        instancia = this;

        ScriptSignals.OnCreateScript += CreateScript;
    }

    public void TestGraph() // save (set)
    {
        Debug.Log("salvando script: " + scriptOnFocus.id);
        scriptOnFocus.nodes = graphBuilder.WorkspaceBuildGraph(scriptOnFocus.nodes);
        jsonBuilder.SaveAsJson(scriptOnFocus.nodes, scriptOnFocus.id.ToString());

        foreach (var p in scriptOnFocus.nodes.parametrosData)
        {
            Debug.Log($"Parameter {p.id} - {p.type} | Value: {p.value} | LeftOperand: {p.leftOperandId} | RightOperand: {p.rightOperandId}");
        }
        foreach (var b in scriptOnFocus.nodes.blocosData) // le de tras pra frente, pois salva de tras pra frente
        {
            Debug.Log($"Block {b.id} | BlockDataId {b.functionId} | ParamIds: {string.Join(", ", b.paramIds)} | Next: {b.next} | Body: {b.body}");
        }

        Debug.Log("Compilando");
        
        BlockCompiler compiler = new BlockCompiler();
        ExecutionBlock root = compiler.Compile(scriptOnFocus.nodes, 0/* esse 0 ta bem errado, tem que ser o id do primeiro */);// tem que considerar uma lista de primeiros blocos
        List<ExecutionBlock> roots = new List<ExecutionBlock>();
        roots.Add(root);
        List<VariableData> varList = new List<VariableData>();
        foreach(CreateVarNode var in scriptOnFocus.nodes.variaveisData) {
            varList.Add(new VariableData(){
                varName = var.varName,
                defaultValue = var.defaultValue
            }
            );
        } 

        scriptOnFocus.functionsRoot = roots;
        scriptOnFocus.vars = varList;
    }

    public void OpenScript(ScriptData script)
    {
        Debug.Log("Build blocks from script: " + script.id);
        SaveNodesData nodes = script.nodes;
        //dar clear dos blocos na workspace
        // talvez iterar sobre os filhos diretos da content Workspace e da blocksFactory.CleanUpBlock()
        jsonBuilder.BuildBlocksFromData(nodes);
        scriptOnFocus = script;
    }

    public void DeleteScript(ScriptData script)
    {
        // if (script == scriptOnFocus) blocksFactory.CleanUpWorkspace();
        scripts.Remove(script);
        jsonBuilder.DeleteJson(script.id.ToString());
    }

    public void CreateScript(ScriptData script)
    {
        scripts.Add(script);
    }
}
