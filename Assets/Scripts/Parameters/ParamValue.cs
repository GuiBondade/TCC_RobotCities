using System.Collections.Generic;

// Item de uma lista dinâmica de valores (bloco de import).
// 'label' é o texto exibido no popup; 'key' é o que é salvo/selecionado
// (ex.: o instance id da entidade, ou "f:Andar" pra uma função importada).
[System.Serializable]
public struct ParamValue
{
    public string label;
    public string key;

    public ParamValue(string label, string key = null) {
        this.label = label;
        this.key = string.IsNullOrEmpty(key) ? label : key;
    }
}
