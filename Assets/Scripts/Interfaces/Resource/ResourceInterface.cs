using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interface própria do recurso (minério, lenha, etc.).
// Mostra o material e a quantidade restante, seguindo o padrão de
// BuildingInterface/RobotInterface. O GameObject já existe na cena
// (DynamicInterfaces/ResourceInterface). Ao abrir, popula o slot e se
// reposiciona no mouse.
public class ResourceInterface : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RepositionInterface repositionInterface;
    [SerializeField] private SlotInterface slotInterface;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text emptyLabel;

    [Header("Catálogo")]
    [SerializeField] private MaterialDatabase materialDatabase;

    private ResourceController resourceController;
    private InventoryManager subscribedInventory;

    public void SetSignal() {
        ResourceSignals.OnInterfacePopulation -= Open;
        if (repositionInterface != null) ResourceSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;

        EnsureReferences();

        ResourceSignals.OnInterfacePopulation += Open;
        if (repositionInterface != null) ResourceSignals.OnInterfaceReposition += repositionInterface.SetupPosition;
    }

    private void OnDestroy() {
        ResourceSignals.OnInterfacePopulation -= Open;
        if (repositionInterface != null) ResourceSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;
        UnsubscribeInventory();
        resourceController = null;
    }

    private void EnsureReferences() {
        if (materialDatabase == null && ExecutionManager.instancia != null) {
            materialDatabase = ExecutionManager.instancia.materialDatabase;
        }

        if (repositionInterface == null) repositionInterface = GetComponent<RepositionInterface>();
        if (repositionInterface == null) repositionInterface = gameObject.AddComponent<RepositionInterface>();

        RectTransform panelRect = transform as RectTransform;
        if (repositionInterface != null && panelRect != null) {
            Canvas canvas = GetComponentInParent<Canvas>(true);
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null) repositionInterface.Configure(panelRect, canvasRect);
        }
    }

    private void Open(ResourceController resource) {
        if (this == null || resource == null) return;

        UnsubscribeInventory();
        resourceController = resource;
        subscribedInventory = resource.inventoryManager;

        Populate(resource);

        if (subscribedInventory != null) {
            subscribedInventory.OnInventoryChanged += OnInventoryChanged;
        }

        gameObject.SetActive(true);
    }

    private void Populate(ResourceController resource) {
        if (resource == null) return;

        if (nameLabel != null) nameLabel.text = resource.EntityName;

        var inv = resource.inventoryManager;
        if (inv == null || inv.slotList == null || inv.slotList.Count == 0) {
            if (slotInterface != null) slotInterface.ShowEmpty();
            SetEmptyMessage("Recurso sem estoque.");
            return;
        }

        var slot = inv.slotList[0];
        MaterialData material = default;
        if (materialDatabase != null && slot.currentTypeId >= 0) {
            material = materialDatabase.ObterPorId(slot.currentTypeId);
        }

        if (slotInterface != null) {
            if (slot.currentAmount <= 0) slotInterface.ShowEmpty();
            else slotInterface.UpdateSlot(slot.currentAmount, material);
        }

        SetEmptyMessage(string.Empty);
    }

    private void OnInventoryChanged(InventoryManager inventory) {
        if (resourceController == null || inventory != subscribedInventory) return;
        if (inventory == null || inventory.IsEmpty()) {
            Close();
            return;
        }
        Populate(resourceController);
    }

    private void SetEmptyMessage(string message) {
        if (emptyLabel == null) return;
        emptyLabel.text = message;
        emptyLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void Close() {
        UnsubscribeInventory();
        resourceController = null;
        gameObject.SetActive(false);
    }

    private void UnsubscribeInventory() {
        if (subscribedInventory != null) {
            subscribedInventory.OnInventoryChanged -= OnInventoryChanged;
            subscribedInventory = null;
        }
    }
}