using UnityEngine;
using UnityEngine.EventSystems;

public abstract class DraggableBlockGeneric : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    protected CanvasAreasManager canvasAreasManager;
    protected BlocksFactory blocksFactory;
    private ZoomScrollWorkspace scrollWorkspace;

    private bool isRightClickDragging = false;

    private static Vector3 GetMouseScreenPosition()
    {
        Vector3 mouseWorldPos = Input.mousePosition;
        mouseWorldPos.z = 0f;
        return mouseWorldPos;
    }

    public virtual void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    void Start() 
    {
        canvasAreasManager = CanvasAreasManager.instancia;
        scrollWorkspace = canvasAreasManager.visibleWorkspace.GetComponent<ZoomScrollWorkspace>();
        blocksFactory = BlocksFactory.instance;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (isRightClickDragging || eventData.button == PointerEventData.InputButton.Right)
        {
            eventData.pointerDrag = null;
            return;
        }

        BlockerManager.instancia.Reset();

        canvasGroup.blocksRaycasts = false;

        AttachBlockToMouse();

        scrollWorkspace.blockInDrag = rect; // altera bloco a ter scale alterado com scroll
    }

    public void OnDrag(PointerEventData eventData) {
        if (isRightClickDragging || eventData.button == PointerEventData.InputButton.Right) return;
        rect.position = GetMouseScreenPosition();
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (isRightClickDragging || eventData.button == PointerEventData.InputButton.Right) return;
        
        scrollWorkspace.blockInDrag = null;

        // Se soltar fora da workspace, destruir
        if (!RectTransformUtility.RectangleContainsScreenPoint(canvasAreasManager.visibleWorkspace, eventData.position, eventData.pressEventCamera)) {
            CleanUpBlock();
        }
        // verificar se foi solto em um slot de bloco, caso não, resetar os ancestrais
        else {
            AttachBlockToWorkspace(eventData);
        }

        canvasGroup.blocksRaycasts = true;
    }

    public virtual void AttachBlockToMouse() {
        // Parenta na workspace (ou root) para arrastar livremente
        transform.SetParent(canvasAreasManager.canvasProgramming, true);

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public abstract void AttachBlockToWorkspace(PointerEventData eventData);
    public abstract void CleanUpBlock();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            isRightClickDragging = true;
            eventData.pointerDrag = null; // Cancela o rastreamento de arrasto na UI da Unity
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            isRightClickDragging = false;
        }
    }
}
