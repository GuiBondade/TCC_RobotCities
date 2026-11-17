using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class InventoryManager : MonoBehaviour
{    
    // Capacidade por slot agora vem de SlotConfigData.maxAmount (tier da construção).
    // O fallback (16) fica em DefaultMaxAmount, dentro de RebuildSlots.
    public List<Slot> slotList = new List<Slot>();
    public event Action<InventoryManager> OnInventoryChanged;

    private List<Slot> slotBackup = new List<Slot>();

    public void RebuildSlots(SlotLayoutConfig slotLayout) { // quando trocar de layout (banner/receita)
        slotList.Clear();

        // Capacidade por slot vem do asset (SlotConfigData.maxAmount), que carrega o tier
        // da construção. Asset antigo sem o campo desserializa 0 -> fallback 16 (comportamento antigo).
        const int defaultMaxAmount = 16;

        foreach (var slotConfig in slotLayout.slotConfigList) {
            int capacity = slotConfig.maxAmount > 0 ? slotConfig.maxAmount : defaultMaxAmount;
            slotList.Add(
                new Slot () {
                    role = slotConfig.role,
                    craftAmount = slotConfig.craftAmount,
                    currentTypeId = slotConfig.typeId,
                    hasTypeLocked = slotConfig.hasTypeLocked,
                    currentAmount = 0,
                    maxAmount = capacity,
                }
            );
        }
        NotifyInventoryChanged();
    }

    // Monta um único slot travado no material indicado, com o estoque informado.
    // Usado por recursos: um material só, tipo fixo, sem receita (craftAmount 1).
    // O slot precisa ficar travado (hasTypeLocked): se destravado, ao zerar a quantidade
    // o SubtractItems limpa o currentTypeId (-1) e a UI quebra ao buscar o material.
    public void SetupSingleSlot(int typeId, int amount) {
        slotList.Clear();
        slotList.Add(
            new Slot() {
                role = SlotRole.Input,
                craftAmount = 1,
                currentTypeId = typeId,
                hasTypeLocked = true,
                currentAmount = amount,
                maxAmount = amount,
            }
        );
        NotifyInventoryChanged();
    }

    // Monta os slots da fase de construção (obra).
    // O limite de cada slot é a própria exigência (craftAmount), e não o limite do tier.
    // Consequências (permite reaproveitar os métodos que já existiam):
    //   IsFull()  = todos os materiais exigidos foram depositados
    //   IsEmpty() = nenhum material foi depositado ainda
    //   um material só entra no slot travado do seu tipo (hasTypeLocked)
    public void RebuildConstructionSlots(SlotLayoutConfig slotLayout) {
        slotList.Clear();

        foreach (var slotConfig in slotLayout.slotConfigList) {
            slotList.Add(
                new Slot () {
                    role = slotConfig.role,
                    craftAmount = slotConfig.craftAmount,
                    currentTypeId = slotConfig.typeId,
                    hasTypeLocked = slotConfig.hasTypeLocked,
                    currentAmount = 0,
                    maxAmount = slotConfig.craftAmount, // a exigência é o próprio limite do slot
                }
            );
        }
        NotifyInventoryChanged();
    }

    // Sistema de Backup para caso de erro 
    public void BackupSlots() {
        while (slotBackup.Count > slotList.Count) {
            slotBackup.RemoveAt(slotBackup.Count - 1);
        }

        for (int i = 0; i < slotList.Count; i++) {
            if (i >= slotBackup.Count) {
                slotBackup.Add(new Slot()); // Só cria se o backup for menor que a lista original
            }

            slotBackup[i].CopyFrom(slotList[i]); 
        }
    }

    public void RollBackSlots() {
        bool changed = slotList.Count != slotBackup.Count;
        int sharedCount = Mathf.Min(slotList.Count, slotBackup.Count);
        for (int i = 0; i < sharedCount && !changed; i++) {
            changed = !SlotsEqual(slotList[i], slotBackup[i]);
        }

        while (slotList.Count > slotBackup.Count) {
            slotList.RemoveAt(slotList.Count - 1);
        }

        for (int i = 0; i < slotBackup.Count; i++) {
            if (i >= slotList.Count) {
                slotList.Add(new Slot());
            }

            slotList[i].CopyFrom(slotBackup[i]); 
        }

        if (changed) NotifyInventoryChanged();
    }

    private static bool SlotsEqual(Slot first, Slot second) {
        if (first == null || second == null) return first == second;

        return first.role == second.role
            && first.craftAmount == second.craftAmount
            && first.currentTypeId == second.currentTypeId
            && first.hasTypeLocked == second.hasTypeLocked
            && first.currentAmount == second.currentAmount
            && first.maxAmount == second.maxAmount;
    }

    // Utilidades pra Upgrade
    // Vazio = nenhum item guardado em nenhum slot. O slot de ferramenta também conta:
    // uma ferramenta guardada é um item e seria perdida (ex.: ao destruir a construção
    // ou ao trocar de layout, que reconstrói os slots).
    public bool IsEmpty() {
        foreach (var slot in slotList) {
            if (slot.currentAmount > 0) return false;
        }
        return true;
    }

    public bool IsFull() {
        foreach (var slot in slotList) {
            if (slot.role == SlotRole.Tool) continue; // ferramenta não conta como item guardado
            if (slot.currentAmount < slot.maxAmount) return false;
        }
        return true;
    }

    // Quanto o inventário tem de um tipo de material (ignora slots de ferramenta).
    public int GetAmountOf(int type) {
        int total = 0;

        foreach (var slot in slotList) {
            if (slot.role == SlotRole.Tool) continue;
            if (slot.currentTypeId == type) total += slot.currentAmount;
        }

        return total;
    }

    // ------------------------------------------------------------------
    // Ferramenta
    // O slot de ferramenta guarda o ID da ferramenta (ToolData.id) no currentTypeId,
    // os usos restantes no currentAmount e a durabilidade no maxAmount.
    // Slots de ferramenta são ignorados nas operações de material (Add/Subtract).
    // ------------------------------------------------------------------

    public Slot GetSlotByRole(SlotRole role) {
        int index = FindSlotByRole(role);
        return index == -1 ? null : slotList[index];
    }

    // Equipa uma ferramenta no slot de ferramenta: tipo fixo (id da ferramenta) e usos cheios.
    public bool EquipTool(int toolTypeId, int maxUses) {
        int index = FindSlotByRole(SlotRole.Tool);
        return EquipToolAtSlot(index, toolTypeId, maxUses);
    }

    public bool EquipToolAtSlot(int slotIndex, int toolTypeId, int maxUses) {
        if (slotIndex < 0 || slotIndex >= slotList.Count
            || slotList[slotIndex].role != SlotRole.Tool
            || maxUses <= 0) return false;

        var slot = slotList[slotIndex];
        slot.currentTypeId = toolTypeId;
        slot.hasTypeLocked = true;
        slot.maxAmount = maxUses;
        slot.currentAmount = maxUses;
        NotifyInventoryChanged();
        return true;
    }

    // Tenta adicionar uma ferramenta num slot Tool vazio (ou do mesmo id). Usado para devolver
    // a ferramenta antiga ao container ao trocar. Retorna false se não houver slot adequado.
    public bool TryAddTool(int toolId, int uses, int maxUses) {
        for (int i = 0; i < slotList.Count; i++) {
            var slot = slotList[i];
            if (slot.role != SlotRole.Tool) continue;
            if (slot.currentAmount > 0 && slot.currentTypeId != toolId) continue;
            if (slot.currentAmount > 0 && slot.currentTypeId == toolId) {
                int space = slot.maxAmount - slot.currentAmount;
                if (space <= 0) continue;
                int put = Mathf.Min(space, uses);
                slot.currentAmount += put;
                if (put >= uses) { NotifyInventoryChanged(); return true; }
                uses -= put;
                continue;
            }
            // slot vazio
            slot.currentTypeId = toolId;
            slot.hasTypeLocked = true;
            slot.maxAmount = maxUses > 0 ? maxUses : slot.maxAmount;
            slot.currentAmount = Mathf.Min(uses, slot.maxAmount);
            NotifyInventoryChanged();
            return true;
        }
        return false;
    }

    // Consome um uso da ferramenta. Devolve false se não houver slot/uso.
    // Quando os usos acabam, a ferramenta sai do slot (currentTypeId = -1).
    public bool UseTool() {
        int index = FindSlotByRole(SlotRole.Tool);
        if (index == -1) return false;

        var slot = slotList[index];
        if (slot.currentTypeId < 0 || slot.currentAmount <= 0) return false;

        slot.currentAmount--;
        if (slot.currentAmount <= 0) {
            slot.currentAmount = 0;
            slot.currentTypeId = -1; // ferramenta acabou
        }
        NotifyInventoryChanged();
        return true;
    }

    private int FindSlotByRole(SlotRole role) {
        for (var i = 0; i < slotList.Count; i++) {
            if (slotList[i].role == role) return i;
        }
        return -1;
    }

    // Alteração da quantidade de materiais nos slots.
    // Distribui entre vários slots: primeiro preenche o slot que já tem o tipo, depois
    // usa slots vazios. Se não couber tudo, devolve false (quem chamou faz rollback).
    public bool AddItems(int amount, int type, int indexStart = 0) {
        if (amount <= 0) return true;

        int remaining = amount;

        for (int i = indexStart; i < slotList.Count && remaining > 0; i++) {
            var slot = slotList[i];

            // Slot de ferramenta não recebe material (e material não vira ferramenta).
            if (slot.role == SlotRole.Tool) continue;
            // Slot travado em outro material não pode ser usado.
            if (slot.hasTypeLocked && slot.currentTypeId != type) continue;
            // Slot ocupado com outro material não mistura.
            if (slot.currentAmount > 0 && slot.currentTypeId != type) continue;

            int space = slot.maxAmount - slot.currentAmount;
            if (space <= 0) continue;

            int put = Mathf.Min(space, remaining);
            slot.currentTypeId = type; // slot vazio assume o novo tipo
            slot.currentAmount += put;
            remaining -= put;
        }

        bool succeeded = remaining == 0;
        if (succeeded) NotifyInventoryChanged();
        return succeeded;
    }


    public bool SubtractItems(int amount, int type, int indexStart = 0) { // tem que ter algum return? (int excedente ou bool pra erro?)
        if (amount <= 0) return true;

        int remaining = amount;

        for (int i = indexStart; i < slotList.Count && remaining > 0; i++) {
            var slot = slotList[i];

            // Slot de ferramenta guarda usos, não material: nunca é subtraído como item.
            if (slot.role == SlotRole.Tool) continue;
            if (slot.currentTypeId != type || slot.currentAmount <= 0) continue;

            int take = Mathf.Min(slot.currentAmount, remaining);
            slot.currentAmount -= take;
            remaining -= take;

            if (slot.currentAmount == 0 && !slot.hasTypeLocked) {
                slot.currentTypeId = -1; //atualizar visual do slot? acho que a função que chama da reset no visual com os dados novos(ou seja, tem um middleware entre os dados e a parte visual, pra renderização correta e alterações)
            }
        }

        bool succeeded = remaining == 0;
        if (succeeded) NotifyInventoryChanged();
        return succeeded; //tudo certo
    }

    public void NotifyInventoryChanged() {
        OnInventoryChanged?.Invoke(this);
    }

    // Limpa todos os slots (sem backup). Usado por construções sem layout de operação
    // (ponte, mina) depois de prontas, para não deixar o slot da obra na interface.
    public void ClearSlots() {
        slotList.Clear();
        NotifyInventoryChanged();
    }

    // Slot de ferramenta (SlotRole.Tool) é ignorado por AddItems/SubtractItems:
    // ele guarda usos de ferramenta, não material.

    // ------------------------------------------------------------------
    // Consumo/produção por papel de slot (usado pelo CraftingManager e pela
    // conclusão da construção).
    // Não fazem backup interno: quem chama controla a transação
    // (BackupSlots antes, RollBackSlots em caso de falha).
    // ------------------------------------------------------------------

    // Consome o craftAmount de cada slot Input.
    public bool ConsumeInputSlots() {
        for (int i = 0; i < slotList.Count; i++) {
            var slot = slotList[i];
            if (slot.role != SlotRole.Input) continue;

            if (!SubtractItems(slot.craftAmount, slot.currentTypeId, i)) return false;
        }
        return true;
    }

    // Preenche o craftAmount de cada slot Output.
    public bool FillOutputSlots() {
        for (int i = 0; i < slotList.Count; i++) {
            var slot = slotList[i];
            if (slot.role != SlotRole.Output) continue;

            if (!AddItems(slot.craftAmount, slot.currentTypeId, i)) return false;
        }
        return true;
    }
}
