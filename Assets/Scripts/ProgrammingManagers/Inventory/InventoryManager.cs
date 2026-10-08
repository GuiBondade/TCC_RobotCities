using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    private int currentTier = 1;
    //private List<SlotLayoutConfig> slotLayoutList; é os banners aceitos pela construção, é meio unico delas entao deixa quieto(tier tambem)
    private SlotLayoutConfig currentSlotLayout;
    private List<Slot> slotList;

    private int[] maxAmountByTier = {16, 32, 64, 256};

    private void RebuildSlots() { // quando trocar de banner 
        slotList.Clear();

        int currentMaxAmount = maxAmountByTier[currentTier - 1];

        foreach (var slotConfig in currentSlotLayout.slotConfigList) {
            slotList.Add(
                new Slot () {
                    currentTypeId = slotConfig.typeId,
                    hasTypeLocked = slotConfig.hasTypeLocked,
                    currentAmount = 0,
                    maxAmount = currentMaxAmount,
                }
            );
        }
    }
}
