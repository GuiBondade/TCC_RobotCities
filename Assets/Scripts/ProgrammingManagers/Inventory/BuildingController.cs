using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingController : MonoBehaviour
{
    [SerializeField] public InventoryManager inventoryManager;

    [SerializeField] public List<SlotLayoutConfig> availableLayouts = new List<SlotLayoutConfig>();
    [SerializeField] public SlotLayoutConfig defaultSlotLayout;

    public SlotLayoutConfig currentSlotLayout { get; private set; }
    public int currentTier = 1;
    public bool isUpgrading = false;

    private void Start()
    {
        if (availableLayouts.Count > 0)
        {
            SelectLayout(availableLayouts[0]);
        }
    }

    public void SelectLayout(SlotLayoutConfig layout) { // chamado por clicar numa receita na interface, se ativada na interface
        if (layout == currentSlotLayout) return;

        currentSlotLayout = layout;
        if (layout.isUpgrade) isUpgrading = true;
        else isUpgrading = false;
        inventoryManager.RebuildSlots(layout, currentTier);

    }

    private void UpgradeTier() {
        if (!inventoryManager.IsFull() || !isUpgrading) return;

        currentTier++;
        currentSlotLayout = defaultSlotLayout;
        isUpgrading = false;
        inventoryManager.RebuildSlots(defaultSlotLayout, currentTier);
    }
}
