using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ContentToggle : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject content;
    [SerializeField] private string textEnabled;
    [SerializeField] private string textDisabled;

    public void ToggleContent (bool isEnabled) {
        label.text = isEnabled ? textEnabled : textDisabled;

        content.SetActive(isEnabled);
    }
}
