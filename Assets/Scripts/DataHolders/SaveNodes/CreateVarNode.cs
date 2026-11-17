using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CreateVarNode
{
    public string varName;
    public ParameterType type;
    public ParameterSections section;
    public string defaultValue;
    public Vector2 position;
    public bool exported; // a variável pode ser importada por outro script
}
