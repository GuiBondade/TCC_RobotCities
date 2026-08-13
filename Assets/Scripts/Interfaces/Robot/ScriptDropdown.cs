using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScriptDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    private List<string> optionsList = new List<string>();
    [SerializeField] private string defaultOption;

    public void UpdateDropdown(List<ScriptData> scriptList, int selectedIndex) {
        dropdown.ClearOptions(); 
        optionsList.Clear();
        optionsList.Add(defaultOption);
        foreach(var script in scriptList) {
            optionsList.Add(script.name);
        }

        dropdown.AddOptions(optionsList);

        dropdown.value = selectedIndex;
        dropdown.RefreshShownValue();
    }
}
