using System;
using System.Collections.Generic;
using UnityEngine;

// Catálogo de entidades do mapa (construções e recursos) agrupadas em grupos
// (categorias). Referenciado pelo SectionDatabase e usado pela seção [entidade]
// do parâmetro (operador booleano "contém").
//
// Cada grupo tem uma `key` estável (é o que é gravado nos scripts) e um `label`
// exibido no popup (pode mudar livremente). A associação grupo -> assets é feita
// aqui no Inspector, sem precisar alterar os assets de ConstructionData/ResourceData.
[CreateAssetMenu(fileName = "EntityDatabase", menuName = "Block Programming/Entity Database")]
public class EntityDatabase : ScriptableObject
{
    [Serializable]
    public class ConstructionGroupEntry
    {
        [Tooltip("Chave estável (minúscula, sem espaços). É o valor gravado nos scripts.")]
        public string key;
        [Tooltip("Texto exibido no popup. Pode mudar livremente.")]
        public string label;
        [Tooltip("Construções que pertencem a este grupo.")]
        public List<ConstructionData> constructions = new List<ConstructionData>();
    }

    [Serializable]
    public class ResourceGroupEntry
    {
        [Tooltip("Chave estável (minúscula, sem espaços). É o valor gravado nos scripts.")]
        public string key;
        [Tooltip("Texto exibido no popup. Pode mudar livremente.")]
        public string label;
        [Tooltip("Recursos que pertencem a este grupo.")]
        public List<ResourceData> resources = new List<ResourceData>();
    }

    [SerializeField] private List<ConstructionGroupEntry> constructionGroups = new List<ConstructionGroupEntry>();
    [SerializeField] private List<ResourceGroupEntry> resourceGroups = new List<ResourceGroupEntry>();

    public IReadOnlyList<ConstructionGroupEntry> ConstructionGroups => constructionGroups;
    public IReadOnlyList<ResourceGroupEntry> ResourceGroups => resourceGroups;

    public static EntityDatabase instancia { get; private set; }

    public static void SetInstance(EntityDatabase db) { if (db != null) instancia = db; }
    public static void ClearInstance(EntityDatabase db) { if (instancia == db) instancia = null; }

    private void OnEnable() { SetInstance(this); }
    private void OnDisable() { ClearInstance(this); }

    public bool MatchesConstruction(string key, ConstructionData data) {
        if (string.IsNullOrEmpty(key) || data == null) return false;
        foreach (var group in constructionGroups) {
            if (group == null || group.key != key) continue;
            if (group.constructions == null) return false;
            return group.constructions.Contains(data);
        }
        return false;
    }

    public bool MatchesResource(string key, ResourceData data) {
        if (string.IsNullOrEmpty(key) || data == null) return false;
        foreach (var group in resourceGroups) {
            if (group == null || group.key != key) continue;
            if (group.resources == null) return false;
            return group.resources.Contains(data);
        }
        return false;
    }
}