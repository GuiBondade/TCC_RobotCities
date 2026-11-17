using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScriptDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    private List<string> optionsList = new List<string>();
    [SerializeField] private string defaultOption;

    public void UpdateDropdown(List<ScriptData> scriptList, int selectedIndex) {
        if (dropdown == null) return;

        dropdown.ClearOptions(); 
        optionsList.Clear();
        optionsList.Add(defaultOption);

        if (scriptList != null) {
            foreach(var script in scriptList) {
                if (script != null) optionsList.Add(script.name);
            }
        }

        dropdown.AddOptions(optionsList);

        // SetValueWithoutNotify evita que popular o dropdown dispare OnValueChanged
        // (o que antes desativava a execução ao abrir a interface).
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, optionsList.Count - 1));
        dropdown.RefreshShownValue();
    }
}
