using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    [SerializeField] public BuildingController buildingController;
    [SerializeField] public InventoryManager inventoryManager;
    [SerializeField] public TickSignal tickSignal;

    [SerializeField] public int ticksPerCraft = 10;
    private int currentTickProgress = 0; //poe no buildingController pra interface acessar? ou poe referencia pra esse script la?

    private void Awake() {
        // Auto-resolve: evita wiring manual em cada prefab de construção.
        if (buildingController == null) buildingController = GetComponent<BuildingController>();
        if (inventoryManager == null && buildingController != null) inventoryManager = buildingController.inventoryManager;
    }

    private void OnEnable() {
        // Fallback pro tick global (mesmo padrão do RobotMovement.SubscribeToTick).
        if (tickSignal == null && ExecutionManager.instancia != null) {
            tickSignal = ExecutionManager.instancia.tickSignal;
        }

        if (tickSignal == null) {
            Debug.LogError("[Crafting] TickSignal não encontrado: a construção não vai progredir.", this);
            return;
        }

        tickSignal.RegisterListener(ProcessTick);
    }

    private void OnDisable() {
        tickSignal?.UnregisterListener(ProcessTick);
    }

    private void ProcessTick() {
        if (buildingController == null || !buildingController.isInitialized) return;

        // Em obra: avança o progresso da construção e não processa crafting nenhum.
        if (!buildingController.IsActive) {
            buildingController.AdvanceConstruction();
            return;
        }

        if (!CanCraft()) {
            currentTickProgress = 0;
            return;
        }

        currentTickProgress++;

        if (currentTickProgress >= ticksPerCraft) {
            ExecuteCrafting();
            currentTickProgress = 0;
        }
    }

    private bool CanCraft() {
        if (buildingController == null || !buildingController.IsActive) return false;
        if (inventoryManager == null) return false;

        foreach (var slot in inventoryManager.slotList) {
            switch (slot.role) {
                case SlotRole.Input: 
                    if (slot.currentAmount < slot.craftAmount) return false;
                    break;
                case SlotRole.Output: 
                    if (slot.currentAmount + slot.craftAmount > slot.maxAmount) return false;
                    break;
                case SlotRole.Tool:
                    if (!CanProduceTool(slot)) return false;
                    break;
            }            
        }
        return true;
    }

    private void ExecuteCrafting() {
        // Transacional: se alguma etapa falhar, desfaz tudo (mesmo padrão do TesteInventario).
        // O backup é único: dois backups aninhados fariam o rollback do output
        // desfazer só metade da transação.
        inventoryManager.BackupSlots();

        if (!inventoryManager.ConsumeInputSlots()
            || !inventoryManager.FillOutputSlots()
            || !ProduceTools()) {
            inventoryManager.RollBackSlots();
        }
    }

    private bool CanProduceTool(Slot slot) {
        if (slot == null || slot.currentAmount > 0 || slot.currentTypeId < 0) return false;

        ToolData tool = GetTool(slot.currentTypeId);
        if (tool == null) return false;

        return tool.maxUses > 0;
    }

    private bool ProduceTools() {
        for (int i = 0; i < inventoryManager.slotList.Count; i++) {
            Slot slot = inventoryManager.slotList[i];
            if (slot.role != SlotRole.Tool) continue;

            ToolData tool = GetTool(slot.currentTypeId);
            if (tool == null || !inventoryManager.EquipToolAtSlot(i, tool.id, tool.maxUses)) {
                return false;
            }

            MapContext originContext = buildingController != null ? buildingController.mapContext : null;
            ObjectiveSignals.TriggerToolCrafted(tool.id, originContext);
        }

        return true;
    }

    private ToolData GetTool(int toolId) {
        ExecutionManager executionManager = ExecutionManager.instancia;
        if (executionManager == null) {
            Debug.LogError("[Crafting] ExecutionManager não encontrado para criar a ferramenta.", this);
            return null;
        }

        ToolData tool = executionManager.GetTool(toolId);
        if (tool == null) {
            Debug.LogError($"[Crafting] O ID {toolId} configurado no slot Tool não corresponde a uma ferramenta.", this);
        }

        return tool;
    }
}
