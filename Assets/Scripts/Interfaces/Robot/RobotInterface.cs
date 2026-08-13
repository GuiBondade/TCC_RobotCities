using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotInterface : MonoBehaviour
{
    [SerializeField] private ScriptManager scriptManager;

    [SerializeField] private ScriptData scriptChosen; 
    [SerializeField] private TMP_InputField nameInput;
    //ferramenta
    //inventario
    [SerializeField] private ScriptDropdown scriptDropdown;
    [SerializeField] private CustomTextToggle executionToggle;
    [SerializeField] private CustomTextToggle debugToggle;

    private RobotScript robotScript;
    private RobotData data;

    void Start() {
        RobotSignals.OnInterfaceOpen += Setup; 
        gameObject.SetActive(false);

        executionToggle.OnToggleStateChanged += ExecutionToggled;
        debugToggle.OnToggleStateChanged += DebugToggled;
    }

    private void Setup(RobotScript script) {
        /* a partir do RobotData setar:
        nome do robo(inputText)
        ferramenta(não sei como, carrossel? dropdown de imagem?)
        inventario(tem que fazer tudo ainda)
        index do script escolhido(value do dropdown)
        bool de em execução(colorir background do botao pra verde ou escuro)
        bool de debug ativado(colorir background do botao pra verde ou escuro)
         */ // so salva as alterações ao clicar no botão de salvar
        robotScript = script;
        scriptChosen = robotScript.scriptData;
        data = robotScript.robotData;

        nameInput.text = data.robotName;
        scriptDropdown.UpdateDropdown(scriptManager.scripts, data.scriptIndex);
        executionToggle.toggle.isOn = data.isExecuting;
        debugToggle.toggle.isOn = data.isDebugging;
    
        gameObject.SetActive(true);
    }

    private void DropdownOptionChosen(int index){
        if (index == 0) return; 
        index -= 1;
        scriptChosen = scriptManager.scripts[index];

        robotScript.scriptData = scriptChosen;//RobotSignals.TriggerScriptAttach(scriptChosen);// ponho no save (aqui so seta uma var int )
        executionToggle.toggle.isOn = false;
    }

    private void ExecutionToggled(bool isOn) {
        if (isOn) {
            data.isExecuting = true;
            RobotSignals.TriggerExecutionActivation(robotScript);
        } else {
            data.isExecuting = false;
            RobotSignals.TriggerExecutionDeactivation(robotScript);
        }
    }

    private void DebugToggled(bool isOn) {
        if (isOn) {
            data.isDebugging = true;
            //RobotSignals.TriggerDebugActivation();
        } else {
            data.isDebugging = false;
            //RobotSignals.TriggerDebugDeactivation();
        }
    }
}
