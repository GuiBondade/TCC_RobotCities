using UnityEngine;

// Marca um elemento visual que pode receber highlight.
// Coloque este componente em um filho do elemento (ex.: um objeto "Highlight"
// com Image, Outline ou Animator). Ele se esconde ao acordar para não aparecer
// antes de o UIHighlightController ser configurado.
public class UIHighlightTarget : MonoBehaviour
{
    [Tooltip("Chave usada por ObjectiveData.highlightKey / PuzzleInfo.highlightKey. Vazio = nome do GameObject.")]
    [SerializeField] private string key;

    [Tooltip("Esconde o visual ao iniciar. Desative se o próprio objeto já controla a visibilidade.")]
    [SerializeField] private bool hideOnAwake = true;

    public string Key => string.IsNullOrWhiteSpace(key) ? gameObject.name : key.Trim();
    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        if (hideOnAwake) gameObject.SetActive(false);
    }

    public void SetKey(string value)
    {
        key = value;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
