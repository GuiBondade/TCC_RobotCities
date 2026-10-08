using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

using ScriptRunner;

public class ExecutionManager : MonoBehaviour // arquivo set/delete json
{
    /* 
        EXECUTAR TODOS OS ROBOS A CADA TICK
        ROBOS ADICIONADOS/RETIRADOS NA LISTA DE EXECUÇÃO A PARTIR DA ATIVAÇÃO/DESATIVAÇÃO PELO PLAYER
        
        (NO SCRIPT DO ROBO VAI SER TROCADO A REF DO SCRIPT ANEXADO COM ESCOLHA EM DROPDOWN)
        (VAI SER CRIADO UMA LISTA DE SCRIPTS SALVOS COM ID E REFERENCIA PRO ARQUIVO JSON(a qual vai ser exposta no dropdown da interface dos robos),
            compiledScripts no scriptManager, dicionario de struct a ser melhorado)

     */
    private float acumuladorTempo = 0f;
    public float tickTime = 0.2f; // Ex: 100ms por tick

    private List<ExecutionContext> functionsInExecution = new List<ExecutionContext>(); 
    private List<ExecutionContext> functionsToRemove = new List<ExecutionContext>(); 

    private void OnEnable() => RobotSignals.OnActivateExecution += ActivateExecution;
    private void OnDisable() => RobotSignals.OnDeactivateExecution -= DeactivateExecution;

    public void ActivateExecution(RobotScript robot) {
        var context = new ExecutionContext();
        context.robotScript = robot;
        
        ScriptData scriptData = robot.scriptData;

        if (scriptData.vars != null)
        {
            foreach (var var in scriptData.vars)
            {
                object valorInicial = ValueParser.ParseRawString(var.defaultValue);
                
                context.SetVariable(var.varName, valorInicial);
            }
        }
        context.current = scriptData.functionsRoot[0];
        robot.executionContext = context;

        functionsInExecution.Add(context);
    }

    public void DeactivateExecution(RobotScript robot){
        functionsInExecution.Remove(robot.executionContext);
    }

    void Update() {
        acumuladorTempo += Time.deltaTime;
        while (acumuladorTempo >= tickTime) {
            TickExecution();
            acumuladorTempo -= tickTime;
        }
    }

    // chamado a cada tick
    public void TickExecution(){
        //Debug.Log(functionsInExecution.Count);
        foreach (var context in functionsInExecution) {
            if (context.ticksOcupado > 0) {
                context.ticksOcupado--; // Reduz o tempo restante da animação/ação
                continue; // Pula o script dele neste tick (ele está ocupado trabalhando)
            }
            if (context.current == null) {
                functionsToRemove.Add(context);
                continue;
            }
            Debug.Log("Bloco Executado");
            context.current = context.current.Execute(context);
        }
        foreach (var context in functionsToRemove){
            functionsInExecution.Remove(context);
        }
    }
}
