using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlotRole : byte {Input, Output, Neutral, Tool}

[System.Serializable]
public struct SlotConfigData 
{
    public int slotIndex; // faz automatico ja eu acho, o index na lista, sei la
    [Tooltip("Para slots Tool, informe o ID da ferramenta no ToolDatabase.")]
    public int typeId;
    public bool hasTypeLocked;
    public SlotRole role;
    public int craftAmount; // quantidade da receita para Input/Output; 0 para Tool/Neutral
    [Tooltip("Capacidade do slot (máximo de itens). Ignorado em slots Tool, cuja capacidade vem da ferramenta. <= 0 usa o padrão (16).")]
    public int maxAmount; // capacidade por layout (tier da construção); 0 = padrão 16 (fallback no InventoryManager)
}

[CreateAssetMenu(fileName = "SlotLayoutConfig", menuName = "Inventory/Slot Layout Config")]
public class SlotLayoutConfig : ScriptableObject
{
    public string recipeConfigName;
    public Sprite icon;
    public List<SlotConfigData> slotConfigList;
}
