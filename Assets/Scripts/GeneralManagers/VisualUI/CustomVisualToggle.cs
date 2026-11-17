using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro; // Obrigatório para TextMeshPro

public class CustomVisualToggle : MonoBehaviour
{
    [SerializeField] private TMP_Text toggleText;
    [SerializeField] private GameObject scriptInterface;


    public void ToggleVisual(bool isInterfaceOn)
    {
        scriptInterface.SetActive(isInterfaceOn);
        toggleText.enabled = !isInterfaceOn;
    }
}
