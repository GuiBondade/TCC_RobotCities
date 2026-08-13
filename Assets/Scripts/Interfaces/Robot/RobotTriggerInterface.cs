using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotTriggerInterface : MonoBehaviour
{
    public RobotScript robotScript;

    public void OnMouseDown() {
        RobotSignals.TriggerInterfaceOpen(robotScript);
        Debug.Log("interface open");
    }
}
