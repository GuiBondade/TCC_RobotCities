using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableBlockLine : DraggableBlockGeneric
{
    public BlockUI blockUI;

    private Transform originalParent;
    //private Vector3 originalLocalPos;

    public override void Awake()
    {
        blockUI = GetComponent<BlockUI>();
        base.Awake();
    }

    public override void AttachBlockToMouse() {
        originalParent = transform.parent;
        //originalLocalPos = transform.localPosition;

        // Subtrai essa altura dos blockSpacers dos ancestrais do bloco sendo arrastado
        if (blockUI.bodyAncestors != null && blockUI.bodyAncestors.Count > 0) { // originalParent = slotBody
            blockUI.AdjustBodySpacers(-(blockUI.GetTailHeight()));
        }
        if (originalParent != canvasAreasManager.contentWorkspace && originalParent.TryGetComponent<BlockSlot>(out BlockSlot slot)) {
            if (slot.slotType == SlotType.Body) { //significa que ta tirando o filho direto do body, junto a seus filhos(deixando o slot vazio de blocos)
                blockUI.AdjustBodySpacers(blockUI.defaultSpacerHeight);
            }
        } 
        blockUI.AssignBodyAncestorsRecursive(null, null);

        // resetar currentblock do slot que tava alocado
        var slotParent = originalParent.GetComponent<BlockSlot>();
        if (slotParent != null) slotParent.currentBlock = null;

        base.AttachBlockToMouse();
    }

    public override void AttachBlockToWorkspace(PointerEventData eventData) { // da pra especificar os parametros, pra tipo, eventdata.position
        // Se estiver sobre um BlockSlot, coloca como filho dele
        var placeDropped = eventData.pointerEnter?.transform;
        if (placeDropped != null && placeDropped != canvasAreasManager.contentWorkspace)
        {
            Debug.Log("dropou em bloco");
            BlockSlot slot = BlockUIUtils.GetBlockSlotByEventData(eventData); 
            if (slot != null) blocksFactory.ConnectToSlot(blockUI, false, slot.transform);
        } else {
            Debug.Log("dropou em workspace");
            blocksFactory.ConnectToSlot(blockUI, true, canvasAreasManager.contentWorkspace);
        }
    }

    public override void CleanUpBlock() {
        blocksFactory.CleanUpBlockLine(blockUI);
    }
}
