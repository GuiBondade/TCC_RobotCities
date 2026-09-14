using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PaletteItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    protected CanvasAreasManager canvasAreasManager;

    public BlocksFactory blocksFactory;

    public BlockData blockData;

    public TMP_Text label;

    private BlockUI ui;

    protected RectTransform rect;
    
    private ZoomScrollWorkspace scrollWorkspace;

    private static Vector3 GetMouseScreenPosition()
    {
        Vector3 mouseWorldPos = Input.mousePosition;
        mouseWorldPos.z = 0f;
        return mouseWorldPos;
    }

    void Start()
    {
        canvasAreasManager = CanvasAreasManager.instancia;

        label.text = blockData.blockName;
        label.ForceMeshUpdate();

        scrollWorkspace = canvasAreasManager.visibleWorkspace.GetComponent<ZoomScrollWorkspace>();
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (blockData == null) return;
        BlockerManager.instancia.Reset();

        SetupBlockData();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        rect.pivot = new Vector2(0.5f, 0.5f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        rect.position = GetMouseScreenPosition();
        rect.localScale = canvasAreasManager.contentWorkspace.localScale;
        scrollWorkspace.blockInDrag = rect; // altera ref do bloco a ter scale alterado com scroll

        var cg = rect.GetComponent<CanvasGroup>();
        if (cg == null) cg = rect.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // Permitir que o drag atravesse raycasts
    } 

    public void OnDrag (PointerEventData eventData) {
        if (rect == null) return;
        rect.position = GetMouseScreenPosition();
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (rect == null) return;

        scrollWorkspace.blockInDrag = null;

        HandleBlockDrop(eventData);

        // Finaliza drag: permitir que o objeto receba raycasts e possa ser movido depois
        var cg = rect.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = true;

    }

    protected virtual void SetupBlockData() {
        ui = blocksFactory.InitializeBlockLine(blockData, false, canvasAreasManager.canvasProgramming);
        rect = ui.GetComponent<RectTransform>();
        foreach (var parametro in blockData.listaParametros) {
            var paramReference = blocksFactory.InitializeParameter(null, parametro, ui.TopSlot);
            ui.parameterInitialList.Add(paramReference);
        }
    }

    protected virtual void HandleBlockDrop(PointerEventData eventData) {
        if (!RectTransformUtility.RectangleContainsScreenPoint(canvasAreasManager.visibleWorkspace, eventData.position, eventData.pressEventCamera)) 
        {
            blocksFactory.CleanUpBlockLine(ui);
        }
        else 
        {
            BlockUIUtils.AttachToSlotOrWorkspace(ui, eventData, blocksFactory, canvasAreasManager);
        }

        ui = null;
    }
}
