using UnityEngine;
using UnityEngine.UI;
using System;

using ScriptRunner;

public class RobotScript : MonoBehaviour // arquivo set/delete json
{
    [SerializeField] public RobotMovement robotMovement;
    [SerializeField] public RobotInventory robotInventory;

    public ScriptData scriptData;// pura referencia pro script anexado como código(que sepa da até de tirar)
    public ExecutionContext executionContext;// contexto exato em execução

    public RobotData robotData { get; private set; }// pra setar interface do robo

    public void Awake() {
        robotData = ScriptableObject.CreateInstance<RobotData>();
        // robotData.name = "nao repetir nome";
    }

    public void Start() {
        RobotSignals.OnScriptAttached += OnScriptChosen;    
    }

    public void OnScriptChosen(ScriptData script) {
        scriptData = script;
    }

    public void TestExecution()// SetExecution => vai ser chamado pelos robos, e vai retornar o context
    {
        if (scriptData == null) return;
        // essa funcao vai ser chamada quando tentar ativar a execução do script anexado, 
        // porém somente se tiver selecionado ãlgum script no dropdown
        Debug.Log("Executando");

        RobotSignals.TriggerExecutionActivation(this); 
    } 

    public void StartSetup(Vector3Int position, Vector2 tileSize, float robotSpeed) {
        robotMovement.tilePosition = new Vector2(position.x, position.y);
        robotMovement.tileSize = tileSize;
        robotMovement.robotSpeed = robotSpeed;
    }
}
