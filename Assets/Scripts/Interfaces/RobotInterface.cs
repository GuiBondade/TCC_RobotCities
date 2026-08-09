using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotInterface : MonoBehaviour
{
    public ScriptData scriptChosen; 

    public void DropdownOptionChosen(int index){
        if (index != 0) return; // so pra impedir erro de teste
        var scriptManager = ScriptManager.instancia; 
        Debug.Log(scriptManager);
        var scriptList = scriptManager.scripts;
        scriptChosen = scriptList[index];

        RobotSignals.TriggerScriptAttach(scriptChosen);
    }
}
