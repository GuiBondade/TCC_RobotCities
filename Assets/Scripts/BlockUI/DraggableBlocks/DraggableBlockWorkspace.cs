using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableBlockWorkspace : DraggableBlockGeneric
{
    public CreateVariableUI createVarUI;

    public override void Awake()
    {
        createVarUI = GetComponent<CreateVariableUI>();
        base.Awake();
    }

    public override void AttachBlockToWorkspace(PointerEventData eventData) { // da pra especificar os parametros, pra tipo, eventdata.position
        Debug.Log("dropou em workspace");
        createVarUI.transform.SetParent(canvasAreasManager.contentWorkspace, true);
    }

    public override void CleanUpBlock() {
        blocksFactory.CleanUpBlockWorkspace(createVarUI);
    }
}
