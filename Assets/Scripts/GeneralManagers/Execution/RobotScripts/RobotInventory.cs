using System.Collections.Generic;
using UnityEngine;

// Inventário do robô: slots de material (Neutral) + um slot de ferramenta (Tool).
// A ferramenta fica no mesmo InventoryManager do robô, mas slots Tool são ignorados
// pelas operações de material (AddItems/SubtractItems).
public class RobotInventory : MonoBehaviour
{
    [SerializeField] public InventoryManager inventoryManager;
    [SerializeField] public SlotLayoutConfig defaultLayout;
    [SerializeField] public List<SlotLayoutConfig> availableLayouts = new List<SlotLayoutConfig>();

    public SlotLayoutConfig currentLayout { get; private set; }

    [Tooltip("Usos da ferramenta, usado só no debug de equipar (o normal vem do ToolData)")]
    public int defaultToolMaxUses = 20;

    [Header("Debug de ferramenta (inspector)")]
    [Tooltip("Id da ferramenta a equipar pelo método de teste (ToolDatabase).")]
    public int debugToolId = 0;
    [Tooltip("Durabilidade/usos da ferramenta de teste.")]
    public int debugToolUses = 20;

    private bool initialized;

    // Monta os slots do robô. Chamado pelo RobotScript.StartSetup.
    public void Initialize() {
        if (initialized) return;
        initialized = true;

        if (inventoryManager == null) {
            inventoryManager = GetComponentInChildren<InventoryManager>();
        }
        if (inventoryManager == null) {
            Debug.LogError("RobotInventory sem InventoryManager: o robô não tem onde guardar item.", this);
            return;
        }
        if (defaultLayout == null) {
            Debug.LogError("RobotInventory sem SlotLayoutConfig: o robô ficou sem slots.", this);
            return;
        }

        currentLayout = defaultLayout;
        inventoryManager.RebuildSlots(currentLayout);
    }

    public List<SlotLayoutConfig> GetAvailableLayouts() {
        var layouts = new List<SlotLayoutConfig>();
        foreach (var layout in availableLayouts) {
            if (layout != null && !layouts.Contains(layout)) layouts.Add(layout);
        }
        if (defaultLayout != null && !layouts.Contains(defaultLayout)) layouts.Insert(0, defaultLayout);
        return layouts;
    }

    public bool SelectLayout(SlotLayoutConfig layout) {
        if (layout == null || inventoryManager == null) return false;

        // Preserva a ferramenta equipada entre layouts (quando o novo layout tem slot Tool).
        int toolId = GetToolId();
        int toolUses = GetToolUses();
        int toolMax = 0;
        var toolSlot = GetToolSlot();
        if (toolId >= 0 && toolSlot != null) toolMax = toolSlot.maxAmount;

        bool hasToolSlot = false;
        if (layout.slotConfigList != null) {
            foreach (var sc in layout.slotConfigList) {
                if (sc.role == SlotRole.Tool) { hasToolSlot = true; break; }
            }
        }

        if (toolId >= 0 && !hasToolSlot) {
            Debug.LogWarning("[RobotInventory] Não troca de layout com ferramenta equipada: layout sem slot de ferramenta.");
            return false;
        }

        // Permite troca de layout se o inventário de materiais estiver vazio (ferramenta é ignorada).
        bool materialsEmpty = true;
        foreach (var s in inventoryManager.slotList) {
            if (s.role == SlotRole.Tool) continue;
            if (s.currentAmount > 0) { materialsEmpty = false; break; }
        }
        if (!materialsEmpty) return false;

        currentLayout = layout;
        inventoryManager.RebuildSlots(layout);

        // Reequipa a ferramenta preservada.
        if (toolId >= 0 && hasToolSlot) {
            inventoryManager.EquipTool(toolId, toolMax > 0 ? toolMax : defaultToolMaxUses);
            var newSlot = GetToolSlot();
            if (newSlot != null) newSlot.currentAmount = toolUses;
        }
        return true;
    }

    public Slot GetToolSlot() {
        if (inventoryManager == null) return null;
        return inventoryManager.GetSlotByRole(SlotRole.Tool);
    }

    // Id da ferramenta equipada (-1 se não tem).
    public int GetToolId() {
        var slot = GetToolSlot();
        if (slot == null) return -1;
        if (slot.currentAmount <= 0) return -1; // slot vazio (nenhuma ferramenta equipada)
        return slot.currentTypeId;
    }

    // Usos restantes da ferramenta equipada (0 se não tem).
    public int GetToolUses() {
        var slot = GetToolSlot();
        if (slot == null) return 0;
        return slot.currentAmount;
    }

    // Equipa a ferramenta no slot Tool (guarda o id da ferramenta, do ToolDatabase).
    public bool EquipTool(int toolId, int maxUses) {
        Initialize();
        if (inventoryManager == null) return false;
        return inventoryManager.EquipTool(toolId, maxUses);
    }

    // Equipa a ferramenta pelo id, lendo a durabilidade do ToolDatabase.
    public bool EquipTool(int toolId) {
        int maxUses = defaultToolMaxUses;
        var executionManager = ExecutionManager.instancia;
        if (executionManager != null) {
            var tool = executionManager.GetTool(toolId);
            if (tool != null && tool.maxUses > 0) maxUses = tool.maxUses;
        }
        return EquipTool(toolId, maxUses);
    }

    // Retira a ferramenta do slot Tool de um container (ex.: fábrica) e equipa no robô.
    // Se o robô já tem ferramenta: tenta devolvê-la ao container; se não couber, descarta ("quebra").
    public bool TakeToolFrom(InventoryManager source, int sourceSlotIndex) {
        Initialize();
        if (inventoryManager == null || source == null) return false;
        if (sourceSlotIndex < 0 || sourceSlotIndex >= source.slotList.Count) return false;
        var sourceSlot = source.slotList[sourceSlotIndex];
        if (sourceSlot.role != SlotRole.Tool || sourceSlot.currentTypeId < 0 || sourceSlot.currentAmount <= 0) return false;

        int newToolId = sourceSlot.currentTypeId;
        int newUses = sourceSlot.currentAmount;
        int newMax = sourceSlot.maxAmount;

        int oldToolId = GetToolId();
        int oldUses = 0;
        int oldMax = 0;
        var oldSlot = GetToolSlot();
        if (oldToolId >= 0 && oldSlot != null) {
            oldUses = oldSlot.currentAmount;
            oldMax = oldSlot.maxAmount;
        }

        source.BackupSlots();
        inventoryManager.BackupSlots();

        if (oldToolId >= 0) {
            bool stored = source.TryAddTool(oldToolId, oldUses, oldMax);
            if (!stored) {
                Debug.Log("[RobotInventory] Ferramenta antiga não coube no container: descartada (quebrada).");
            }
        }

        sourceSlot.currentTypeId = -1;
        sourceSlot.currentAmount = 0;
        sourceSlot.hasTypeLocked = false;

        var robotToolSlot = GetToolSlot();
        if (robotToolSlot == null) {
            source.RollBackSlots();
            inventoryManager.RollBackSlots();
            return false;
        }
        robotToolSlot.currentTypeId = newToolId;
        robotToolSlot.hasTypeLocked = true;
        robotToolSlot.maxAmount = newMax;
        robotToolSlot.currentAmount = newUses;

        source.NotifyInventoryChanged();
        inventoryManager.NotifyInventoryChanged();
        return true;
    }

    // Debug: equipa uma ferramenta pelo id/durabilidade definidos no inspector.
    [ContextMenu("Equipar ferramenta (id/durabilidade)")]
    private void EquipDebugTool() {
        if (debugToolUses <= 0) { Debug.LogWarning("[RobotInventory] Durabilidade inválida."); return; }
        bool ok = EquipTool(debugToolId, debugToolUses);
        Debug.Log("RobotInventory: equipar ferramenta id " + debugToolId + " (" + (ok ? "ok" : "falhou") + "), usos: " + GetToolUses());
    }

    // Debug: remove a ferramenta equipada.
    [ContextMenu("Remover ferramenta")]
    private void RemoveDebugTool() {
        var slot = GetToolSlot();
        if (slot == null) return;
        slot.currentTypeId = -1;
        slot.currentAmount = 0;
        slot.hasTypeLocked = false;
        if (inventoryManager != null) inventoryManager.NotifyInventoryChanged();
        Debug.Log("RobotInventory: ferramenta removida.");
    }

    // Debug antigo mantido para compatibilidade.
    [ContextMenu("Equipar ferramenta de teste")]
    private void EquipTestTool() {
        EquipTool(1, defaultToolMaxUses);
        Debug.Log("RobotInventory: ferramenta de teste equipada (id 1), usos: " + GetToolUses());
    }
}
