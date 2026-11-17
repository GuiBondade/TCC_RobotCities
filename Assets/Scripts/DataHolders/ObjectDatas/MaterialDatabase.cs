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

    // Lista completa de materiais (somente leitura).
    // Usada pra popular a seção de materiais do SectionDatabase (fonte única dos materiais).
    public IReadOnlyList<MaterialData> Materials => materialList;

    public MaterialData ObterPorId(int id) {
        // Busca direta pelo índice: só é válida se o id estiver dentro da lista E bater com o typeId.
        // Fora da faixa (-1 quando um slot destravado zera, por exemplo) cai na busca linear abaixo.
        if (id >= 0 && id < materialList.Count && materialList[id].typeId == id) {
            return materialList[id];
        }
        
        for(var i = 0; i < materialList.Count; i++) {
            if (materialList[i].typeId == id) {
                return materialList[i];
            }
        }

        Debug.LogWarning("Não encontrou material com id: " + id);
        return default;
    }

    // Busca pelo nome do material (ignora maiúsculas/minúsculas).
    public bool TryGetByName(string itemName, out MaterialData data) {
        data = default;
        if (string.IsNullOrEmpty(itemName)) return false;

        for (var i = 0; i < materialList.Count; i++) {
            if (string.Equals(materialList[i].itemName, itemName, StringComparison.OrdinalIgnoreCase)) {
                data = materialList[i];
                return true;
            }
        }

        return false;
    }

    private void OnValidate() {        HashSet<int> verifiedIds = new HashSet<int>();

        foreach(var item in materialList) {
            if (verifiedIds.Contains(item.typeId)) {
                Debug.LogError("typeId: " + item.typeId + " já utilizado");
            } else {
                verifiedIds.Add(item.typeId);
            }
        }
    }
}
