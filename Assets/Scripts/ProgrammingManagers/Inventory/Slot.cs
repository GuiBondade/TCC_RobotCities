using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Slot
{
    public SlotRole role;
    public int craftAmount;
    public int currentTypeId;
    public bool hasTypeLocked;
    public int currentAmount;
    public int maxAmount;

    public void CopyFrom(Slot slot) {
        this.role = slot.role;
        this.craftAmount = slot.craftAmount;
        this.currentTypeId = slot.currentTypeId;
        this.hasTypeLocked = slot.hasTypeLocked;
        this.currentAmount = slot.currentAmount;
        this.maxAmount = slot.maxAmount;
    }
}
