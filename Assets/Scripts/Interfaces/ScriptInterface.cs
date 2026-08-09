using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScriptInterface : MonoBehaviour
{
    public ScriptData scriptData;
    public TMP_Text nameLabel; // pra quando fora de foco
    //public TMP_InputField nameInput;                                  //não preciso alterar nada nele em si
    //public Button deleteBtn; //interface de confirmação(fazer)!!!     //não preciso alterar nada nele em si
    //public Button openScriptBtn;                                      //não preciso alterar nada nele em si

    public void NameChanged(string name) {
        string newName = name;// função de não repetir nome (talvez reusar o de variavel, so que ter que passar lista tambem)
        scriptData.name = newName;
        nameLabel.text = newName;
    }

    public void ScriptDeleted() {
        // abre interface de confirmação, e quando clicar no botão dentro da interface emite o event
        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            ScriptSignals.TriggerScriptDeletion(scriptData);
    }

    public void ScriptOpened() {
        ScriptSignals.TriggerScriptOpen(scriptData);
    }

    // BLOCKER vai desativar a scriptInterface e ativar o nameLabel(TMP_Text) pro nome
}
