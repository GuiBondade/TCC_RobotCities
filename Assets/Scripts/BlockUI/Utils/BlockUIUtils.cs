using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public static class BlockUIUtils 
{
    // Uma única lista na memória para evitar ficar criando listas novas (otimiza performance)
    private static readonly List<RaycastResult> s_RaycastTargets = new List<RaycastResult>();

    public static BlockSlot GetBlockSlotByEventData(PointerEventData eventData) 
    {
        if (eventData == null) return null;

        s_RaycastTargets.Clear();
        EventSystem.current.RaycastAll(eventData, s_RaycastTargets);

        foreach (var target in s_RaycastTargets) 
        {
            // GetComponent é o certo, ""InParent pega o slot do pai, ou seja, o errado
            BlockSlot slot = target.gameObject.GetComponent<BlockSlot>();
            if (slot != null) 
            {
                return slot;
            } 
        }
        return null;
    }

    public static void AttachToSlotOrWorkspace(
        BlockUI targetBlock, 
        PointerEventData eventData, 
        BlocksFactory blocksFactory, 
        CanvasAreasManager canvasAreasManager)
    {
        BlockSlot slot = GetBlockSlotByEventData(eventData);

        if (slot != null)
        {
            blocksFactory.ConnectToSlot(targetBlock, false, slot.transform);
        }
        else
        {
            blocksFactory.ConnectToSlot(targetBlock, true, canvasAreasManager.contentWorkspace);
        }
    }
}