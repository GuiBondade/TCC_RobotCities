using UnityEngine;
using UnityEngine.UI;
using System;

using ScriptRunner;

public class RobotScript : MonoBehaviour // arquivo set/delete json
{
    public RobotMovement robotMovement;
    public RobotInventory robotInventory;

    public ScriptData scriptTarget;

    public void Start() {
        RobotSignals.OnScriptAttached += OnScriptChosen;    
    }

    public void OnScriptChosen(ScriptData script) {
        scriptTarget = script;
    }

    public void TestExecution(int scriptId)// SetExecution => vai ser chamado pelos robos, e vai retornar o context
    {
        if (scriptId < 0) return;
        // essa funcao vai ser chamada quando tentar ativar a execução do script anexado, 
        // porém somente se tiver selecionado ãlgum script no dropdown
        Debug.Log("Executando");
        ExecutionContext context = new ExecutionContext();
        context.robotScript = this;

        //ScriptData scriptTarget = ScriptManager.instancia.scripts[scriptId];
        
        if (scriptTarget.vars != null)
        {
            foreach (var var in scriptTarget.vars)
            {
                object valorInicial = ValueParser.ParseRawString(var.defaultValue);
                
                context.SetVariable(var.varName, valorInicial);
            }
        }

        context.current = scriptTarget.functionsRoot[0];

        RobotSignals.TriggerExecutionActivation(context);
    } 

    public void StartSetup(Vector3Int position, Vector2 tileSize, float robotSpeed) {
        robotMovement.tilePosition = new Vector2(position.x, position.y);
        robotMovement.tileSize = tileSize;
        robotMovement.robotSpeed = robotSpeed;
    }
}
