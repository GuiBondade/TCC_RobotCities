using System;
using System.Collections.Generic;
using UnityEngine;

// Dados de um desafio exibidos no card da PuzzleInterface.
// A cena é o dado de carregamento; puzzleId é a chave estável para objetivos.
// Nome/descrição/recompensa são apresentação.
[Serializable]
public class PuzzleInfo
{
    [Tooltip("ID estável usado por objetivos e pelo save. Se vazio, usa o sceneName como fallback.")]
    public string puzzleId;

    [Tooltip("Nome exibido no card e no HUD.")]
    public string displayName;

    [TextArea(2, 5)]
    [Tooltip("Descrição curta do objetivo do desafio.")]
    public string description;

    [Tooltip("Texto de recompensa exibido no card (pode ficar vazio).")]
    public string reward;

    [TextArea(2, 5)]
    [Tooltip("Texto de tutorial exibido na HUD quando o desafio for desbloqueado.")]
    public string tutorialText;

    [TextArea(1, 3)]
    [Tooltip("Aviso exibido quando o desafio for desbloqueado. Vazio = mensagem padrão da interface.")]
    public string unlockNotice;

    [Tooltip("Chave do alvo de highlight exibido junto com o tutorial de desbloqueio.")]
    public string highlightKey;

    [Tooltip("Nome exato da cena nos Build Settings (ex.: PuzzleTemplate).")]
    public string sceneName;

    public bool IsValid => !string.IsNullOrEmpty(sceneName) && !string.IsNullOrEmpty(sceneName.Trim());

    // Fallback evita ter que preencher um campo novo em todos os assets existentes.
    public string Id => !string.IsNullOrEmpty(puzzleId) ? puzzleId : sceneName;
}

// Catálogo de desafios. A interface monta os cards em runtime a partir desta lista.
// O puzzleId é a chave estável usada pelos objetivos; o bloqueio visual dos cards
// será ligado em uma etapa futura.
[CreateAssetMenu(menuName = "Puzzles/Puzzle Catalog", fileName = "PuzzleCatalog")]
public class PuzzleCatalog : ScriptableObject
{
    [SerializeField] private List<PuzzleInfo> puzzles = new List<PuzzleInfo>();

    public IReadOnlyList<PuzzleInfo> Puzzles => puzzles;

    public PuzzleInfo FindByScene(string sceneName) {
        if (string.IsNullOrEmpty(sceneName) || puzzles == null) return null;

        foreach (var puzzle in puzzles) {
            if (puzzle == null) continue;
            if (string.Equals(puzzle.sceneName, sceneName, StringComparison.OrdinalIgnoreCase)) return puzzle;
        }
        return null;
    }

    public PuzzleInfo FindById(string puzzleId) {
        if (string.IsNullOrEmpty(puzzleId) || puzzles == null) return null;

        foreach (var puzzle in puzzles) {
            if (puzzle == null) continue;
            if (string.Equals(puzzle.Id, puzzleId, StringComparison.OrdinalIgnoreCase)) return puzzle;
        }
        return null;
    }
}
