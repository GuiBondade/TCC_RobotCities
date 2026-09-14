using UnityEngine;

public class FullScreenToggle : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private float defaultWidth = 300f;
    [SerializeField] private float fullWidth = 1000f;

    [SerializeField] private ZoomScrollWorkspace zoomWorkspace;

    public void SetWidth(bool isFull)
    {
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, isFull ? fullWidth : defaultWidth);
        // tem que sobrepor a UI padrão superior esquerda, ou então desativar ela

        zoomWorkspace.RecalculateBounds();
    }
}