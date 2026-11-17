using System;
using System.Collections.Generic;
using UnityEngine;

// Escopo do objetivo: principal (mapa global) ou parte de um puzzle.
public enum ObjectiveScope
{
    Main,
    Puzzle
}

// Estado inicial permitido na configuração. O estado "Completed" só existe em runtime.
public enum ObjectiveInitialState
{
    Locked,
    InProgress
}

// Tipos de evento que podem alimentar o progresso de um objetivo.
// Signal cobre eventos nomeados; os demais são transições de estado do jogo.
// Novos valores entram no fim para não deslocar os índices já serializados nos assets.
public enum ObjectiveEventType
{
    Signal,
    ConstructionPlaced,
    ConstructionCompleted,
    ResourceExtracted,
    ItemStored,
    ItemTaken,
    ToolCrafted,
    PuzzleCompleted,
    RobotEnteredTile,
    RobotExitedTile,
    BlockPlacedInWorkspace
}

// Tipo de recompensa/desbloqueio concedido por um objetivo concluído.
public enum ObjectiveUnlockType
{
    Puzzle,
    PaletteItem,
    Construction,
    Tool
}

// Um requisito de progresso. Para signals, key é o nome do signal.
// Para eventos de domínio, key é o tile, bloco, material, construção, ferramenta ou puzzle.
// A chave "*" aceita qualquer valor do mesmo tipo de evento.
[Serializable]
public class ObjectiveRequirement
{
    public ObjectiveEventType eventType = ObjectiveEventType.Signal;
    [Tooltip("Signal, tile, bloco, material, construção, ferramenta ou puzzleId, conforme o eventType. " +
             "Use * para aceitar qualquer chave do mesmo tipo de evento.")]
    public string key;
    [Min(1)]
    [Tooltip("Multiplicador aplicado à quantidade do evento. Signals normalmente emitem 1.")]
    public int amountPerEvent = 1;
}

// Configuração imutável de um objetivo. Nenhum estado mutável fica aqui.
[CreateAssetMenu(menuName = "Objectives/Objective", fileName = "Objective")]
public class ObjectiveData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("ID estável usado no save. Não reutilize IDs entre objetivos.")]
    public string id;

    [Tooltip("Nome exibido na interface.")]
    public string title;

    [TextArea(2, 5)]
    [Tooltip("Descrição curta do objetivo.")]
    public string description;

    [Header("Progresso")]
    [Tooltip("Parte do progresso exibida após o título. Placeholders: {atual} e {meta}. Ex.: {atual}/{meta}.")]
    public string progressFormat = "{atual}/{meta}";

    [Min(1)]
    [Tooltip("Meta numérica de progresso.")]
    public int targetAmount = 1;

    [Tooltip("Como o objetivo começa ao iniciar o jogo.")]
    public ObjectiveInitialState initialState = ObjectiveInitialState.Locked;

    [Tooltip("Requisitos de evento que alimentam o progresso.")]
    public List<ObjectiveRequirement> requirements = new List<ObjectiveRequirement>();

    [Header("Cadeia de desbloqueio (lógica OU)")]
    [Tooltip("Este objetivo é desbloqueado quando QUALQUER um destes objetivos estiver concluído. " +
             "Não existe lógica E nesta lista.")]
    public List<ObjectiveData> prerequisites = new List<ObjectiveData>();

    [Header("Apresentação")]
    [Tooltip("Se falso, o objetivo não aparece na lista da interface e não é escolhido automaticamente. " +
             "Útil para passos ocultos de tutorial.")]
    public bool visibleInHud = true;

    [Tooltip("Ordem de exibição dentro do escopo.")]
    public int order = 0;

    [TextArea(2, 6)]
    [Tooltip("Texto explicativo/tutorial exibido por um hint bloqueante, quando configurado.")]
    public string tutorialText;

    [Tooltip("Chave do alvo visual de highlight. O Transform real fica em um UIHighlightTarget na cena.")]
    public string highlightKey;

    [Header("Escopo")]
    public ObjectiveScope scope = ObjectiveScope.Main;

    [Tooltip("Preenchido quando scope = Puzzle. Use o Id do PuzzleInfo (puzzleId ou sceneName de fallback).")]
    public string puzzleId;

    [Tooltip("Marque se este objetivo é necessário para considerar o puzzle concluído.")]
    public bool requiredForPuzzleCompletion = false;

    [Header("Desbloqueios ao concluir")]
    [TextArea(1, 3)]
    [Tooltip("Avisos exibidos quando este objetivo for desbloqueado.")]
    public List<string> unlockNotices = new List<string>();

    [Tooltip("IDs de puzzles que serão liberados quando este objetivo for concluído.")]
    public List<string> puzzlesToUnlock = new List<string>();

    [Tooltip("BlockDatas cujos PaletteItems serão liberados quando este objetivo for concluído.")]
    public List<BlockData> paletteItemsToUnlock = new List<BlockData>();

    [Tooltip("Construções que serão liberadas quando este objetivo for concluído.")]
    public List<ConstructionData> constructionsToUnlock = new List<ConstructionData>();

    [Tooltip("Ferramentas que serão liberadas quando este objetivo for concluído.")]
    public List<ToolData> toolsToUnlock = new List<ToolData>();

    // Soma os incrementos de todos os requisitos que casam com o evento.
    public int GetEventAmount(ObjectiveEvent evt)
    {
        if (requirements == null || requirements.Count == 0) return 0;

        int total = 0;
        for (int i = 0; i < requirements.Count; i++)
        {
            ObjectiveRequirement requirement = requirements[i];
            if (requirement == null) continue;
            if (requirement.eventType != evt.type) continue;
            if (string.IsNullOrEmpty(evt.key)) continue;
            if (!KeyMatches(requirement.key, evt.key)) continue;

            total += Mathf.Max(1, requirement.amountPerEvent) * Mathf.Max(1, evt.amount);
        }
        return total;
    }

    // "*" funciona como curinga para objetivos do tipo "qualquer bloco/recurso".
    private static bool KeyMatches(string requirementKey, string eventKey)
    {
        if (string.IsNullOrEmpty(requirementKey)) return false;
        if (requirementKey == "*") return true;
        return string.Equals(requirementKey, eventKey, StringComparison.Ordinal);
    }

    // Monta o texto de progresso usando valores numéricos, sem parsear números de strings.
    public string BuildProgressText(int currentProgress)
    {
        int target = Mathf.Max(1, targetAmount);
        int current = Mathf.Clamp(currentProgress, 0, target);

        string format = string.IsNullOrEmpty(progressFormat) ? "{atual}/{meta}" : progressFormat;
        return format
            .Replace("{atual}", current.ToString())
            .Replace("{meta}", target.ToString())
            .Replace("{progress}", current.ToString())
            .Replace("{target}", target.ToString());
    }

    // Linha padrão usada pela HUD e pela lista expandida: "Título: progresso".
    public string BuildDisplayText(int currentProgress)
    {
        string progressText = BuildProgressText(currentProgress);
        if (string.IsNullOrEmpty(title)) return progressText;
        return string.IsNullOrEmpty(progressText) ? title : title + ": " + progressText;
    }

    private void OnValidate()
    {
        if (targetAmount < 1) targetAmount = 1;
        if (string.IsNullOrEmpty(progressFormat)) progressFormat = "{atual}/{meta}";

        // Facilita a configuração inicial; o manager ainda valida duplicatas.
        if (string.IsNullOrEmpty(id)) id = name;
    }
}
