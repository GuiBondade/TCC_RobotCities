using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TesteInventario : MonoBehaviour
{
    [SerializeField] public InventoryManager inventarioAtor;
    [SerializeField] public InventoryManager inventario;
    [SerializeField] public int amount;
    [SerializeField] public int typeId;
    

    public void RetirarItem() {
        inventario.BackupSlots();
        inventarioAtor.BackupSlots();
        if (inventario.SubtractItems(amount, typeId) &&
        inventarioAtor.AddItems(amount, typeId)) {
            Debug.Log("tudo certo");
        } else {
            Debug.Log("rollback");
            inventarioAtor.RollBackSlots();
            inventario.RollBackSlots();
        }
    } 

    public void GuardarItem() {
        inventario.BackupSlots();
        inventarioAtor.BackupSlots();
        if (inventario.AddItems(amount, typeId) &&
        inventarioAtor.SubtractItems(amount, typeId)) {
            Debug.Log("tudo certo");
        } else {
            Debug.Log("rollback");
            inventarioAtor.RollBackSlots();
            inventario.RollBackSlots();
        }
    }
}
