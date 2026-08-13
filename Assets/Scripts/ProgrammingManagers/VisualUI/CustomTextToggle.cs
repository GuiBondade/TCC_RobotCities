using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro; // Obrigatório para TextMeshPro

public class CustomTextToggle : MonoBehaviour
{
    public Toggle toggle;
    [SerializeField] private TMP_Text toggleText;
    [SerializeField] private Image toggleBackground;

    [SerializeField] private string onActivatedText;
    [SerializeField] private string onDeactivatedText;

    public Action<bool> OnToggleStateChanged;

    public void UpdateToggleText(bool isOn)
    {
        toggleText.text = isOn ? onActivatedText : onDeactivatedText;
        toggleBackground.color = isOn ? Color.green : Color.gray;
        
        OnToggleStateChanged?.Invoke(isOn);
    }
}
