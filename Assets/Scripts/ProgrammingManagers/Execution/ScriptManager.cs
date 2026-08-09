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
    /* 
        TEM QUE TER UM SCRIPT COM FUNÇÕES DE UI PARA CRIAR NOVO SCRIPT E ADICIONÁ-LO NO DICIONARIO COM ID NOVO
        COM NOME DEFAULT, PODER ALTERAR O NOME DO SCRIPT, PODER DELETAR O SCRIPT E TAMBEM DO DICIONARIO
     */

    public static ScriptManager instancia { get; private set; }

    public WorkspaceBuilder graphBuilder;
    public ScriptJSONBuilder jsonBuilder;

    private ScriptData scriptOnFocus;
    public List<ScriptData> scripts = new List<ScriptData>();
    
    void Start() {
        instancia = this;

        ScriptSignals.OnDeleteScript += DeleteScript;
        ScriptSignals.OnOpenScript += OpenScript;
        ScriptSignals.OnCreateScript += CreateScript;

        scriptOnFocus = new ScriptData(){
            id = 0,
            name = "scriptDefault",
        };
        scripts.Add(scriptOnFocus);
    }

    public void TestGraph() // save (set)
    {
        scriptOnFocus.nodes = graphBuilder.WorkspaceBuildGraph();
        jsonBuilder.SaveAsJson(scriptOnFocus.nodes, scriptOnFocus.name);

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
        ExecutionBlock root = compiler.Compile(scriptOnFocus.nodes, 0);// tem que considerar uma lista de primeiros blocos
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
        //scriptIdCounter++; // esse trampo do id vai ser feito quando criar um script novo, e não quando salvar
    }

    public void OpenScript(ScriptData script)
    {
        SaveNodesData nodes = script.nodes;
        jsonBuilder.BuildBlocksFromData(nodes);
        scriptOnFocus = script;
    }

    public void DeleteScript(ScriptData script)
    {
        scripts.Remove(script);
    }

    public void CreateScript(ScriptData script)
    {
        scripts.Add(script);
    }










    /* public void TestExecution(int scriptId)// SetExecution => vai ser chamado pelos robos, e vai retornar o context
    {
        if (scriptId < 0) return;
        // essa funcao vai ser chamada quando tentar ativar a execução do script anexado, 
        // porém somente se tiver selecionado ãlgum script no dropdown
        Debug.Log("Executando");
        ExecutionContext context = new ExecutionContext();
        context.robotScript = GetComponent<RobotScript>();
        Debug.Log(this.name);

        ScriptCompilado scriptTarget = compiledScripts[scriptId];
        
        if (scriptTarget.vars != null)
        {
            foreach (var varNode in scriptTarget.vars)
            {
                object valorInicial = ValueParser.ParseRawString(varNode.defaultValue);
                
                context.SetVariable(varNode.varName, valorInicial);
            }
        }

        context.current = scriptTarget.current;

        // adicionar na lista de execucao durante tick
        // vai ser feito pelo proprio robo
        ActivateScript(context);

    }

    public void ActivateScript(ExecutionContext context){

        functionsInExecution.Add(context);
        // setar bloco current
    } */
}
