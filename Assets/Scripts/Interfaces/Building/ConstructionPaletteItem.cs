using UnityEngine;
using TMPro;

/// <summary>
/// Item da paleta de construções (UI).
/// Ao ser clicado, envia o ConstructionData para o PlacementManager via ConstructionSignals,
/// que entra em modo de posicionamento e passa a atualizar o preview com a posição do mouse.
/// </summary>
public class ConstructionPaletteItem : MonoBehaviour
{
    [SerializeField] private ConstructionData constructionData;
    [SerializeField] private TMP_Text label; // opcional: nome exibido no item

    public ConstructionData ConstructionData => constructionData;

    private void Start() {
        RefreshLabel();
    }

    /// <summary>Usado pelo BuildingInputManager ao popular a paleta via código.</summary>
    public void SetConstruction(ConstructionData data) {
        constructionData = data;
        RefreshLabel();
    }

    /// <summary>Ligar no onClick do botão do item da paleta.</summary>
    public void OnSelected() {
        if (constructionData == null) {
            Debug.LogWarning("Item da paleta de construções sem ConstructionData atribuído.", this);
            return;
        }

        ConstructionSignals.TriggerConstructionSelected(constructionData);
    }

    private void RefreshLabel() {
        if (label == null || constructionData == null) return;
        label.text = constructionData.displayName;
    }
}
