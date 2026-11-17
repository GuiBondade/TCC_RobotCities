using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controle de "Exportar": marca se uma função/variável pode ser importada por outro script.
// É um botão que alterna o estado (o prefab precisa de um Button e, opcionalmente, um TMP_Text).
public class ExportToggleUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string offLabel = "Exportar";
    [SerializeField] private string onLabel = "Exportado";

    // Estado atual: true = exportado (importável por outro script).
    public bool IsExported { get; private set; }

    public void Setup(bool exported) {
        IsExported = exported;
        if (button != null) {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Toggle);
        }
        Refresh();
    }

    public void Toggle() {
        IsExported = !IsExported;
        Refresh();
    }

    // Define o estado sem passar pelo clique (load/restauração).
    public void SetExported(bool exported) {
        IsExported = exported;
        Refresh();
    }

    private void Refresh() {
        if (label == null) return;
        label.text = IsExported ? onLabel : offLabel;
        label.ForceMeshUpdate();
    }
}
