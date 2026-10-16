using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    [SerializeField] public BuildingController buildingController;
    [SerializeField] public InventoryManager inventoryManager;
    [SerializeField] public TickSignal tickSignal;

    [SerializeField] public int ticksPerCraft = 10;
    private int currentTickProgress = 0; //poe no buildingController pra interface acessar? ou poe referencia pra esse script la?

    private void OnEnable() {
        tickSignal.RegisterListener(ProcessTick);
    }

    private void OnDisable() {
        tickSignal.UnregisterListener(ProcessTick);
    }

    private void ProcessTick() {
        if (!CanCraft()) {
            currentTickProgress = 0;
            return;
        }

        currentTickProgress++;

        if (currentTickProgress >= ticksPerCraft) {
            ExecuteCrafting();
            currentTickProgress = 0;
        }
    }

    private bool CanCraft() {
        if (buildingController.isUpgrading) return false;

        foreach (var slot in inventoryManager.slotList) {
            switch (slot.role) {
                case SlotRole.Input: 
                    if (slot.currentAmount < slot.craftAmount) return false;
                    break;
                case SlotRole.Output: 
                    if (slot.currentAmount + slot.craftAmount > slot.maxAmount) return false;
                    break;
            }            
        }
        return true;
    }

    private void ExecuteCrafting() {
        for (var i = 0; i < inventoryManager.slotList.Count; i++) {
            var slot = inventoryManager.slotList[i];
            
            if (slot.role == SlotRole.Input) {
                inventoryManager.SubtractItems(i, slot.currentAmount, slot.currentTypeId);
                
            } else if (slot.role == SlotRole.Output) {
                inventoryManager.AddItems(i, slot.currentAmount, slot.currentTypeId);
            }
        }
    }
}
