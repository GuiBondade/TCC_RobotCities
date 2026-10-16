using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public struct MaterialData 
{
    public int typeId;
    public string itemName;
    public Sprite icon;
}

[CreateAssetMenu(fileName="MaterialList", menuName="Inventory/Material List")]
public class MaterialDatabase : ScriptableObject
{
    [SerializeField] private List<MaterialData> materialList = new List<MaterialData>();

    public MaterialData ObterPorId(int id) {
        if (materialList[id].typeId == id) {
            return materialList[id];
        }
        
        for(var i = 0; i < materialList.Count; i++) {
            if (materialList[i].typeId == id) {
                return materialList[i];
            }
        }

        Debug.Log("Não encontrou material com id: " + id);
        return default;
    }

    private void OnValidate() {
        HashSet<int> verifiedIds = new HashSet<int>();

        foreach(var item in materialList) {
            if (verifiedIds.Contains(item.typeId)) {
                Debug.LogError("typeId: " + item.typeId + " já utilizado");
            } else {
                verifiedIds.Add(item.typeId);
            }
        }
    }
}
