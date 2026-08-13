using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotData : ScriptableObject
{
    // Dados pra setar quando abrir a interface
    public string robotName;
    public int toolId;
    //inventario slot qtd e tals(object)
    public int scriptIndex; //do dropdown
    public bool isExecuting;
    public bool isDebugging;
}
