using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingInterface : MonoBehaviour 
{
    [SerializeField] public RepositionInterface repositionInterface;
    [SerializeField] public Transform layoutOptionContent; // pai das instancia de opção de layout (lista vertical)
    [SerializeField] public LayoutOption layoutOptionPrefab; // padrão desativado

    private BuildingController buildingController;

    public void SetSignal() {
        BuildingSignals.OnInterfacePopulation += PopulateLayoutContent;
        BuildingSignals.OnInterfaceReposition += repositionInterface.SetupPosition;
    }

    // Populate layout slots
        // iterar sobre lista de slots a partir do buildingController.inventoryManager.slotList
            // setar a quantidade (tem que atualizar em tempo real?, quando tiver alteração nos slots inventoryManager manda signal que refaz essa populate aqui)
            // setar sprite do tipo(pega o int id e busca numa lista de que?)
        // adicionar porcentagem de uma barra de progresso da receita a partir de razão de ticks passados pro necessario por craft


    // adiciono um botao pra o jogador poder desativar/ativar o crafting sepa (preencher espaço)


    private void PopulateLayoutContent(BuildingController buildingScript) {
        buildingController = buildingScript;
        int layoutsInNeed = buildingController.availableLayouts.Count;
        while(layoutOptionContent.childCount < buildingController.availableLayouts.Count) {
            Instantiate(layoutOptionPrefab, layoutOptionContent);
        }

        for (var i = 0; i < layoutOptionContent.childCount; i++) {
            var layoutInstance = layoutOptionContent.GetChild(i);

            if (i < buildingController.availableLayouts.Count) {
                layoutInstance.gameObject.SetActive(true);
                var layoutConfig = buildingController.availableLayouts[i];
                LayoutOption layoutOption = layoutInstance.GetComponent<LayoutOption>();
                // layoutOption.icone.sprite = ? ;
                // layoutOption.?.tooltip = layoutConfig.recipeConfigName;
                layoutOption.button.onClick.RemoveAllListeners();// desatribuir clique de tudo

                if (CanSelectLayout(layoutConfig)) {
                    layoutOption.ActivateLayout();
                        layoutOption.button.onClick.AddListener(() => buildingController.SelectLayout(layoutConfig));
                } else {
                    layoutOption.DeactivateLayout();
                }
            } else {
                layoutInstance.gameObject.SetActive(false);
            }
        }
    }
    
    private bool CanSelectLayout(SlotLayoutConfig layout) {
        if ((layout.requiredTier <= buildingController.currentTier) || (buildingController.inventoryManager.IsEmpty())) {
            return true;
        }
        return false;
    }
}
