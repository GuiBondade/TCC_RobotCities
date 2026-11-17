using System;
using System.Collections.Generic;

// Contexto de execução de UM puzzle. Não é um manager: apenas agrupa os runtimes
// daquele puzzle e o estado necessário para ativar, desativar e restaurar a sessão.
// O ObjectiveManager continua sendo o único dono do barramento e do save.
public sealed class PuzzleObjectiveContext
{
    public readonly string puzzleId;
    public readonly List<ObjectiveRuntime> runtimes = new List<ObjectiveRuntime>();

    public bool isActive;
    public int stageIndex = -1;
    public bool completed;
    public string lastPlayedUtc;

    public PuzzleObjectiveContext(string puzzleId)
    {
        this.puzzleId = puzzleId ?? string.Empty;
    }

    public bool HasPersistentState =>
        stageIndex >= 0 || completed || !string.IsNullOrEmpty(lastPlayedUtc);
}
