using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{    
    [SerializeField] private int[] maxAmountByTier = {16, 32, 64, 256};

    public List<Slot> slotList = new List<Slot>();

    private List<Slot> slotBackup = new List<Slot>();

    public void RebuildSlots(SlotLayoutConfig slotLayout, int currentTier = 1) { // quando trocar de banner 
        slotList.Clear();

        int currentMaxAmount = maxAmountByTier[currentTier - 1];

        foreach (var slotConfig in slotLayout.slotConfigList) {
            slotList.Add(
                new Slot () {
                    role = slotConfig.role,
                    craftAmount = slotConfig.craftAmount,
                    currentTypeId = slotConfig.typeId,
                    hasTypeLocked = slotConfig.hasTypeLocked,
                    currentAmount = 0,
                    maxAmount = currentMaxAmount,
                }
            );
        }
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
        while (slotList.Count > slotBackup.Count) {
            slotList.RemoveAt(slotList.Count - 1);
        }

        for (int i = 0; i < slotBackup.Count; i++) {
            if (i >= slotList.Count) {
                slotList.Add(new Slot());
            }

            slotList[i].CopyFrom(slotBackup[i]); 
        }
    }

    // Utilidades pra Upgrade
    public bool IsEmpty() {
        foreach (var slot in slotList) {
            if (slot.currentAmount > 0) return false;
        }
        return true;
    }

    public bool IsFull() {
        foreach (var slot in slotList) {
            if (slot.currentAmount < slot.maxAmount) return false;
        }
        return true;
    }

    public void UpdateMaxAmount(int tier) {
        for (var i = 0; i < slotList.Count; i++) {
            Slot slot = slotList[i];
            slot.maxAmount = maxAmountByTier[tier];
        }
    }

    // Alteração da quantidade de materiais nos slots
    public bool AddItems(int amount, int type, int indexStart = 0) { 
        int indexSlot = FindSlotToAdd(type, indexStart);
        if (indexSlot == -1) return false; // Não tem nenhum slot compatível ou livre

        var slot = slotList[indexSlot];

        int newAmount = slot.currentAmount + amount; 
        if (newAmount > slot.maxAmount) {
            return AddItems(amount, type, indexSlot + 1); 
            // da pra por só a quantidade certa e voltar o excedente sei la
        }
        if (type != slot.currentTypeId) {
            // Se estiver travado para outro tipo, pula para o próximo slot
            if (slot.hasTypeLocked) { 
                return AddItems(amount, type, indexSlot + 1); 
            } 
            slot.currentTypeId = type; // Slot estava livre e vazio, assume o novo tipo
        }
            
        slot.currentAmount = newAmount;
        return true; // Tudo certo!
    }


    public bool SubtractItems(int amount, int type, int indexStart = 0) { // tem que ter algum return? (int excedente ou bool pra erro?)
        int indexSlot = FindSlotToSubtract(type, indexStart);
        if (indexSlot == -1) return false; // não tem slot com esse tipo
        
        var slot = slotList[indexSlot];

        int newAmount = slot.currentAmount - amount; 
        if (newAmount < 0) {
            return SubtractItems(amount, type, indexSlot + 1); // passou do mínimo (erro)
            // da pra retirar só a quantidade que tem e impedir passar de 0 sei la
        }

        if (newAmount == 0 && !slot.hasTypeLocked) {
            slot.currentTypeId = -1; //atualizar visual do slot? acho que a função que chama da reset no visual com os dados novos(ou seja, tem um middleware entre os dados e a parte visual, pra renderização correta e alterações)
        }
            
        slot.currentAmount = newAmount;
        return true; //tudo certo
    }

    // Utilidade pra alteração em slot
    private int FindSlotToAdd(int type, int currentIndex) {
        for (var i = currentIndex; i < slotList.Count; i++) {
            if (slotList[i].currentTypeId == type || slotList[i].currentAmount == 0) {
                return i;
            }
        }
        return -1;
    }

    private int FindSlotToSubtract(int type, int currentIndex) {
        for (var i = currentIndex; i < slotList.Count; i++) {
            if (slotList[i].currentTypeId == type && slotList[i].currentAmount > 0) {
                return i;
            }
        }
        return -1;
    }
}
