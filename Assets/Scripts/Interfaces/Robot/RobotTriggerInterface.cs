using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotTriggerInterface : MonoBehaviour
{
    [SerializeField] private RobotScript robotScript;
    private Vector2 mousePosition;

    public void OnMouseDown() {
        mousePosition = Input.mousePosition;
        RobotSignals.TriggerInterfaceOpen(robotScript, mousePosition); 
        Debug.Log("interface open");
    }
}
