using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlotRole : byte {Input, Output, Neutral, Tool}

[System.Serializable]
public struct SlotConfigData 
{
    public int slotIndex; // faz automatico ja eu acho, o index na lista, sei la
    public int typeId;
    public bool hasTypeLocked;
    public SlotRole role;
    public int craftAmount; // 0 pra robo
}

[CreateAssetMenu(fileName = "SlotLayoutConfig", menuName = "Inventory/Slot Layout Config")]
public class SlotLayoutConfig : ScriptableObject
{
    public string recipeConfigName;
    public bool isUpgrade;
    public int requiredTier;
    public List<SlotConfigData> slotConfigList;
}
