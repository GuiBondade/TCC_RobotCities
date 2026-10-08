using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlotRole : byte {input, output, neutral, tool}

[System.Serializable]
public struct SlotConfigData 
{
    public int slotIndex;
    public int typeId;
    public bool hasTypeLocked;
    public SlotRole role;
    public int craftAmount; // 0 pra robo
}

[CreateAssetMenu(fileName = "SlotLayoutConfig", menuName = "Inventory/Slot Layout Config")]
public class SlotLayoutConfig : ScriptableObject
{
    public string recipeConfigName;
    public List<SlotConfigData> slotConfigList;
}
