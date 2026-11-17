using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingInterface : MonoBehaviour 
{
    [SerializeField] public RepositionInterface repositionInterface;
    [SerializeField] public Transform layoutOptionContent; // pai das instancia de opção de layout (lista vertical)
    [SerializeField] public LayoutOption layoutOptionPrefab; // padrão desativado
    [SerializeField] public Transform slotInterfaceContent; // pai das interfaces de slot
    [SerializeField] public SlotInterface slotInterfacePrefab; // prefab do slot com SlotInterface
    [SerializeField] public MaterialDatabase materialDatabase; // lista de materiais
    [SerializeField] public ToolDatabase toolDatabase; // catálogo de ferramentas

    private BuildingController buildingController;

    public void SetSignal() {
        BuildingSignals.OnInterfacePopulation -= PopulateLayoutContent;
        if (repositionInterface != null) BuildingSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;

        BuildingSignals.OnInterfacePopulation += PopulateLayoutContent;
        if (repositionInterface != null) BuildingSignals.OnInterfaceReposition += repositionInterface.SetupPosition;
    }

    private void OnDestroy() {
        BuildingSignals.OnInterfacePopulation -= PopulateLayoutContent;
        if (repositionInterface != null) BuildingSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;
        UnsubscribeFromInventory();
    }

    public void PopulateSlots() {
        if (buildingController == null || buildingController.inventoryManager == null) {
            Debug.LogError("[BuildingInterface] Não foi possível popular slots: BuildingController/InventoryManager indisponível.", this);
            return;
        }
        if (slotInterfaceContent == null) {
            Debug.LogError("[BuildingInterface] slotInterfaceContent não configurado no inspector.", this);
            return;
        }
        if (slotInterfacePrefab == null) {
            Debug.LogError("[BuildingInterface] slotInterfacePrefab não configurado no inspector.", this);
            return;
        }
        if (materialDatabase == null) {
            Debug.LogError("[BuildingInterface] materialDatabase não configurado no inspector.", this);
            return;
        }

        List<Slot> slotList = buildingController.inventoryManager.slotList;
        for (var i = 0; i < slotList.Count; i++) {
            var currentSlot = slotList[i];
            SlotInterface slotInterface;
            if (currentSlot.role == SlotRole.Tool) {
                ToolData tool = null;
                if (currentSlot.currentTypeId >= 0 && currentSlot.currentAmount > 0) {
                    if (toolDatabase != null) tool = toolDatabase.GetById(currentSlot.currentTypeId);
                    else if (ExecutionManager.instancia != null) tool = ExecutionManager.instancia.GetTool(currentSlot.currentTypeId);
                }
                if (i >= slotInterfaceContent.childCount) {
                    slotInterface = Instantiate(slotInterfacePrefab, slotInterfaceContent);
                } else {
                    slotInterface = slotInterfaceContent.GetChild(i).GetComponent<SlotInterface>();
                }
                if (slotInterface == null) {
                    Debug.LogError("[BuildingInterface] SlotInterface instanciado sem componente válido.", this);
                    continue;
                }
                slotInterface.gameObject.SetActive(true);
                if (tool == null) slotInterface.ShowEmpty();
                else slotInterface.UpdateToolSlot(tool, currentSlot.currentAmount, currentSlot.maxAmount);
                continue;
            }

            MaterialData material = materialDatabase.ObterPorId(currentSlot.currentTypeId);
            if (currentSlot.currentTypeId < 0 || !currentSlot.hasTypeLocked) {
                material = default;
            }

            if (i >= slotInterfaceContent.childCount) {
                slotInterface = Instantiate(slotInterfacePrefab, slotInterfaceContent);
            } else {
                slotInterface = slotInterfaceContent.GetChild(i).GetComponent<SlotInterface>();
            }

            if (slotInterface == null) {
                Debug.LogError("[BuildingInterface] SlotInterface instanciado sem componente válido.", this);
                continue;
            }

            slotInterface.gameObject.SetActive(true);
            slotInterface.UpdateSlot(currentSlot.currentAmount, material);
        }
        for (var j = slotList.Count; j < slotInterfaceContent.childCount; j++) {
            slotInterfaceContent.GetChild(j).gameObject.SetActive(false);
        }
    }


    // adicionar porcentagem de uma barra de progresso da receita a partir de razão de ticks passados pro necessario por craft
        // chamado por signal a cada tick de progresso no crafting?


    // adiciono um botao pra o jogador poder desativar/ativar o crafting sepa (preencher espaço)
        // manda signal com bool que seta uma var no craftingManager que é verificado no canCraft também


    private void PopulateLayoutContent(BuildingController buildingScript) {
        if (this == null) return;
        if (buildingScript == null) {
            Debug.LogError("[BuildingInterface] Trigger de abertura da interface sem BuildingController.", this);
            return;
        }
        if (layoutOptionContent == null) {
            Debug.LogError("[BuildingInterface] layoutOptionContent não configurado no inspector.", this);
            return;
        }
        if (layoutOptionPrefab == null) {
            Debug.LogError("[BuildingInterface] layoutOptionPrefab não configurado no inspector.", this);
            return;
        }
        if (materialDatabase == null) {
            Debug.LogError("[BuildingInterface] materialDatabase não configurado no inspector.", this);
            return;
        }

        buildingController = buildingScript;
        SubscribeToInventory();
        gameObject.SetActive(true);
        layoutOptionContent.gameObject.SetActive(true);
        PopulateSlots();

        if (repositionInterface == null) {
            Debug.LogError("[BuildingInterface] repositionInterface não configurado no inspector.", this);
            return;
        }

        while(layoutOptionContent.childCount < buildingController.availableLayouts.Count) {
            Instantiate(layoutOptionPrefab, layoutOptionContent);
        }

        for (var i = 0; i < layoutOptionContent.childCount; i++) {
            var layoutInstance = layoutOptionContent.GetChild(i);

            if (i < buildingController.availableLayouts.Count) {
                layoutInstance.gameObject.SetActive(true);
                var layoutConfig = buildingController.availableLayouts[i];
                LayoutOption layoutOption = layoutInstance.GetComponent<LayoutOption>();
                if (layoutOption == null) {
                    Debug.LogError("[BuildingInterface] A opção de layout instanciada não possui LayoutOption.", layoutInstance);
                    continue;
                }
                if (layoutOption.icone == null) {
                    Debug.LogError("[BuildingInterface] LayoutOption sem Image de ícone configurada.", layoutOption);
                    continue;
                }

                if (layoutOption.nome != null) {
                    layoutOption.nome.text = layoutConfig.recipeConfigName;
                }
                layoutOption.icone.sprite = GetLayoutIcon(layoutConfig);
                // layoutOption.?.tooltip = layoutConfig.recipeConfigName;
                if (layoutOption.button == null) {
                    Debug.LogError("[BuildingInterface] LayoutOption sem Button configurado.", layoutOption);
                    continue;
                }
                layoutOption.button.onClick.RemoveAllListeners();// desatribuir clique de tudo

                if (CanSelectLayout(layoutConfig)) {
                    layoutOption.ActivateLayout();
                    layoutOption.button.onClick.AddListener(() => {
                        buildingController.SelectLayout(layoutConfig);
                        PopulateSlots();
                    });
                } else {
                    layoutOption.DeactivateLayout();
                }

                if (buildingController.currentSlotLayout == layoutConfig) {
                    layoutOption.ActivateLayout();
                }
            } else {
                layoutInstance.gameObject.SetActive(false);
            }
        }
    }

    private void SubscribeToInventory() {
        UnsubscribeFromInventory();
        if (buildingController != null && buildingController.inventoryManager != null) {
            buildingController.inventoryManager.OnInventoryChanged += OnInventoryChanged;
        }
    }

    private void UnsubscribeFromInventory() {
        if (buildingController != null && buildingController.inventoryManager != null) {
            buildingController.inventoryManager.OnInventoryChanged -= OnInventoryChanged;
        }
    }

    private void OnInventoryChanged(InventoryManager inventory) {
        if (!gameObject.activeInHierarchy || buildingController == null
            || inventory != buildingController.inventoryManager) return;
        PopulateSlots();
        PopulateLayoutContent(buildingController);
    }

    private Sprite GetLayoutIcon(SlotLayoutConfig layoutConfig) {
        if (layoutConfig == null) return null;
        if (layoutConfig.icon != null) return layoutConfig.icon;
        if (layoutConfig.slotConfigList == null) return null;

        foreach (var slotConfig in layoutConfig.slotConfigList) {
            if (!slotConfig.hasTypeLocked) continue;

            MaterialData material = materialDatabase.ObterPorId(slotConfig.typeId);
            if (material.icon != null) return material.icon;
        }

        return null;
    }
    
    private bool CanSelectLayout(SlotLayoutConfig layout) {
        // Construção em obra: nenhuma troca de layout liberada (as opções ficam desativadas).
        if (!buildingController.IsActive) return false;
        if (layout == null) return false;
        // Evita perda de itens ao trocar de layout: só troca com inventário vazio.
        return buildingController.inventoryManager.IsEmpty();
    }
}
