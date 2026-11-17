using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewParamData", menuName = "Block Programming/Parameter Data")]
public class ParamData : ScriptableObject
{
    public ParameterType type;
    public string defaultParamName;
    public bool CanInputInt;
    public bool CanInputText; // input livre de texto (ex.: digitar um signal novo ou um texto de debug)
    [Tooltip("Limite de caracteres do input livre. <= 0 usa o padrão do tipo (24 para StringParameter).")]
    public int maxCaracteres;
    public List<ParameterSections> sections;
}
