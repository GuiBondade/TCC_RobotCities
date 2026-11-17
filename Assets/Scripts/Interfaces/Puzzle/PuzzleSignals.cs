using System;
using UnityEngine;

// Signals da interface própria de construções especiais (ex.: Construção de Desafios).
// Segue o mesmo padrão de BuildingSignals / RobotSignals: o trigger no MapScene
// dispara e a interface no UIScene (que já existe na cena) se popula/reposiciona.
public static class PuzzleSignals
{
    public static event Action<BuildingController> OnInterfacePopulation;
    public static event Action<Vector2> OnInterfaceReposition;

    // Ciclo de vida do desafio. A HUD e a interface usam esses eventos para se
    // atualizar sem precisar de referência cruzada entre cenas.
    public static event Action<PuzzleInfo> OnPuzzleStarted;
    public static event Action<PuzzleStateManager, int, int> OnPuzzleStageChanged;
    public static event Action<PuzzleInfo> OnPuzzleCompleted;
    public static event Action<PuzzleInfo> OnPuzzleExited;

    // Comandos da HUD. Ela não conhece o PuzzleStateManager/PuzzleManager:
    // só emite o pedido e quem estiver no desafio ativo obedece.
    public static event Action OnNextStageRequested;
    public static event Action OnPreviousStageRequested;
    public static event Action OnExitRequested;

    public static void TriggerInterfaceOpen(BuildingController buildingController, Vector2 mousePosition) {
        OnInterfacePopulation?.Invoke(buildingController);
        OnInterfaceReposition?.Invoke(mousePosition);
    }

    public static void TriggerPuzzleStarted(PuzzleInfo puzzle) {
        OnPuzzleStarted?.Invoke(puzzle);
    }

    public static void TriggerPuzzleStageChanged(PuzzleStateManager state, int stageIndex, int stageCount) {
        OnPuzzleStageChanged?.Invoke(state, stageIndex, stageCount);
    }

    public static void TriggerPuzzleCompleted(PuzzleInfo puzzle) {
        OnPuzzleCompleted?.Invoke(puzzle);
    }

    public static void TriggerPuzzleExited(PuzzleInfo puzzle) {
        OnPuzzleExited?.Invoke(puzzle);
    }

    public static void RequestNextStage() {
        OnNextStageRequested?.Invoke();
    }

    public static void RequestPreviousStage() {
        OnPreviousStageRequested?.Invoke();
    }

    public static void RequestExit() {
        OnExitRequested?.Invoke();
    }

    // Com o domain reload desligado no Play Mode, eventos estáticos sobrevivem
    // entre sessões. Limpa tudo ao iniciar o Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        OnInterfacePopulation = null;
        OnInterfaceReposition = null;
        OnPuzzleStarted = null;
        OnPuzzleStageChanged = null;
        OnPuzzleCompleted = null;
        OnPuzzleExited = null;
        OnNextStageRequested = null;
        OnPreviousStageRequested = null;
        OnExitRequested = null;
    }
}
