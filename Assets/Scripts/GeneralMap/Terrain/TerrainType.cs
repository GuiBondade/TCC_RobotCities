using UnityEngine;

// Tipo de terreno do chão (grama, água, montanha...).
//
// É um enum de flags porque o mesmo requisito pode aceitar mais de um terreno
// numa célula (ex.: uma margem que serve tanto como grama quanto como água).
// Nos dados de construção/recurso, "Nenhum" significa "sem exigência": a célula
// pode ter qualquer terreno, inclusive nenhum. Para exigir um terreno, marque
// um ou mais valores (Grama, Agua, Montanha...).
[System.Flags]
public enum TerrainType
{
    Nenhum = 0,
    Grama = 1 << 0,
    Agua = 1 << 1,
    Montanha = 1 << 2,
}

public static class TerrainTypeExtensions
{
    // O requisito "allowed" aceita o terreno "actual"?
    // - Nenhum (0) no requisito = qualquer terreno (compatibilidade com os
    //   assets antigos, que não tinham esse campo).
    // - Sem terreno marcado na célula (actual == Nenhum), só passa se não houver
    //   exigência. Assim, um mapa sem os tiles marcados não libera construções
    //   que pedem grama/água/montanha por engano.
    public static bool Allows(this TerrainType allowed, TerrainType actual)
    {
        if (allowed == TerrainType.Nenhum) return true;
        if (actual == TerrainType.Nenhum) return false;
        return (allowed & actual) != 0;
    }

    // true quando a célula tem alguma exigência (não é "qualquer terreno").
    public static bool HasRequirement(this TerrainType allowed)
    {
        return allowed != TerrainType.Nenhum;
    }
}
