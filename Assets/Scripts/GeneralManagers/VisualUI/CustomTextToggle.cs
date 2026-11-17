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
        RefreshVisual(isOn);
        OnToggleStateChanged?.Invoke(isOn);
    }

    // Atualiza o estado visual do toggle sem disparar OnToggleStateChanged.
    // Usado pela interface para refletir o estado real ao popular o painel.
    public void SetStateWithoutNotify(bool isOn)
    {
        if (toggle != null) toggle.SetIsOnWithoutNotify(isOn);
        RefreshVisual(isOn);
    }

    private void RefreshVisual(bool isOn)
    {
        if (toggleText != null) toggleText.text = isOn ? onActivatedText : onDeactivatedText;
        if (toggleBackground != null) toggleBackground.color = isOn ? Color.green : Color.gray;
    }
}
