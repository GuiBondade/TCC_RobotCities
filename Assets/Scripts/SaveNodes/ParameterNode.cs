using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ParameterNode 
{
   public int id;
   public ParameterType type;
   public string value;
   public string varName; // precisa diferenciar de value?
   public int leftOperandId;
   public int rightOperandId;
}