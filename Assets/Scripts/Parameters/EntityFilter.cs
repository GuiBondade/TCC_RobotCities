using System;
using System.Collections.Generic;
using UnityEngine;

// Filtros da seção [entidade] (operador booleano "contém").
// A chave selecionada é autodescritiva: "g:*" (gerais), "c:*" (grupos de
// construção) e "r:*" (grupos de recurso).
public static class EntityFilter
{
    public const string GeneralNone = "g:none";
    public const string GeneralAny = "g:any";
    public const string GeneralRobot = "g:robot";
    public const string GeneralConstruction = "g:construction";
    public const string GeneralResource = "g:resource";

    public const string ConstructionPrefix = "c:";
    public const string ResourcePrefix = "r:";

    private static readonly HashSet<string> warnedKeys = new HashSet<string>();

    public static bool Matches(string key, IMapEntity entity)
    {
        if (string.IsNullOrEmpty(key)) return false;

        if (key == GeneralNone) return entity == null;
        if (key == GeneralAny) return entity != null;
        if (key == GeneralRobot) return entity is RobotScript;
        if (key == GeneralConstruction) return entity is BuildingController;
        if (key == GeneralResource) return entity is ResourceController;

        if (key.StartsWith(ConstructionPrefix, StringComparison.Ordinal)) {
            if (!(entity is BuildingController building) || building.data == null) return false;
            var db = EntityDatabase.instancia;
            if (db == null) {
                WarnMissingDatabase();
                return false;
            }
            return db.MatchesConstruction(key.Substring(ConstructionPrefix.Length), building.data);
        }

        if (key.StartsWith(ResourcePrefix, StringComparison.Ordinal)) {
            if (!(entity is ResourceController resource) || resource.data == null) return false;
            var db = EntityDatabase.instancia;
            if (db == null) {
                WarnMissingDatabase();
                return false;
            }
            return db.MatchesResource(key.Substring(ResourcePrefix.Length), resource.data);
        }

        if (warnedKeys.Add(key)) {
            Debug.LogWarning($"[EntityFilter] Chave de entidade desconhecida: '{key}'.");
        }
        return false;
    }

    private static bool warnedMissingDb;
    private static void WarnMissingDatabase() {
        if (warnedMissingDb) return;
        Debug.LogWarning("[EntityFilter] EntityDatabase não está configurado — grupos de construção/recurso não podem ser resolvidos.");
        warnedMissingDb = true;
    }
}