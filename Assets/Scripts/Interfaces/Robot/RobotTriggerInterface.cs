using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotTriggerInterface : TriggerInterface<RobotScript>
{
    public override void OpenInterface(RobotScript script, Vector2 position) {
        RobotSignals.TriggerInterfaceOpen(script, position); 
        Debug.Log("robot interface open");
    }
}
