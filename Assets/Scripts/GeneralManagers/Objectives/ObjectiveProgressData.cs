using System;
using System.Collections.Generic;

// DTO de save. Não contém referências de cena nem estado do mundo dos puzzles.
[Serializable]
public class ObjectiveProgressEntry
{
    public string id;
    public int progress;
    public ObjectiveState state;
    public bool activationProcessed;
    public bool completionProcessed;
}

[Serializable]
public class PuzzleObjectivePin
{
    public string puzzleId;
    public string objectiveId;
}

[Serializable]
public class PuzzleProgressEntry
{
    public string puzzleId;
    public int stageIndex;
    public bool completed;
    public string lastPlayedUtc;
}

[Serializable]
public class ObjectiveProgressData
{
    public int version = 1;
    public string pinnedMainObjectiveId;
    public List<PuzzleObjectivePin> puzzlePins = new List<PuzzleObjectivePin>();
    public List<ObjectiveProgressEntry> objectives = new List<ObjectiveProgressEntry>();
    public List<PuzzleProgressEntry> puzzles = new List<PuzzleProgressEntry>();

    public void EnsureLists()
    {
        if (puzzlePins == null) puzzlePins = new List<PuzzleObjectivePin>();
        if (objectives == null) objectives = new List<ObjectiveProgressEntry>();
        if (puzzles == null) puzzles = new List<PuzzleProgressEntry>();
    }
}
