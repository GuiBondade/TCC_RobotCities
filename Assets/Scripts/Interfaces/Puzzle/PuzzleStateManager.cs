using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Estado de UM desafio carregado em cena aditiva.
//
// A cena do puzzle tem o seu próprio MapLayers/MapContext, mas não tem câmera:
// este manager leva a câmera do mapa principal até o ponto de cada etapa e a
// devolve ao sair. O mapa principal continua rodando (não pausa a simulação).
public class PuzzleStateManager : MonoBehaviour
{
    [Serializable]
    public class PuzzleStage
    {
        [Tooltip("Nome curto da etapa exibido no HUD.")]
        public string displayName;

        [TextArea(2, 5)]
        [Tooltip("Texto de objetivo/estado da etapa.")]
        public string message;

        [Tooltip("Ponto de câmera da etapa. Se vazio, usa cameraPosition.")]
        public Transform cameraAnchor;

        [Tooltip("Posição de mundo usada quando não há cameraAnchor.")]
        public Vector3 cameraPosition;

        [Tooltip("Tamanho ortográfico da etapa. <= 0 mantém o tamanho atual da câmera.")]
        public float orthographicSize = -1f;
    }

    [Header("Etapas")]
    [SerializeField] private List<PuzzleStage> stages = new List<PuzzleStage>();

    [Header("Visualização padrão (quando não há etapas)")]
    [SerializeField] private Transform defaultCameraAnchor;
    [SerializeField] private Vector3 defaultCameraPosition;
    [SerializeField] private float defaultOrthographicSize = -1f;

    [Header("Referências")]
    [SerializeField] public PuzzleCameraRig cameraRig;
    [Tooltip("MapContext do mapa do puzzle. Vazio = procura nos filhos desta cena.")]
    [SerializeField] public MapContext mapContext;

    [SerializeField] private bool restoreCameraOnUnload = true;

    public static PuzzleStateManager instancia { get; private set; }

    public PuzzleInfo Puzzle { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsUnloading { get; private set; }

    public int StageCount => stages != null ? stages.Count : 0;
    public int CurrentStageIndex => currentStage;
    public PuzzleStage CurrentStage => stages != null && currentStage >= 0 && currentStage < stages.Count
        ? stages[currentStage]
        : null;

    public string PuzzleTitle => Puzzle != null && !string.IsNullOrEmpty(Puzzle.displayName)
        ? Puzzle.displayName
        : gameObject.scene.name;

    public string CurrentStageTitle => CurrentStage != null && !string.IsNullOrEmpty(CurrentStage.displayName)
        ? CurrentStage.displayName
        : string.Empty;

    public string CurrentStageMessage => CurrentStage != null ? CurrentStage.message : string.Empty;

    private int currentStage = -1;

    private void Awake() {
        if (instancia == null) instancia = this;
    }

    private void Start() {
        EnsureStarted();
    }

    // Chamado pelo Start e, como salvaguarda, pelo PuzzleManager depois do
    // LoadSceneAsync (a ordem de Start entre cenas aditivas não é garantida).
    public void EnsureStarted(PuzzleInfo puzzle = null) {
        if (IsUnloading) return;

        if (puzzle != null) Puzzle = puzzle;

        // O MapContext é compartilhado por prefab, então o mapId pode vir
        // configurado como "Tutorial" em outros puzzles. O Id do PuzzleInfo é a
        // identidade lógica usada pelos objetivos e precisa prevalecer.
        if (mapContext == null) mapContext = FindContextInThisScene();
        if (mapContext != null && Puzzle != null && !mapContext.isMain &&
            !string.IsNullOrWhiteSpace(Puzzle.Id)) {
            mapContext.mapId = Puzzle.Id;
        }

        if (IsStarted) return;
        IsStarted = true;

        if (mapContext != null) {
            mapContext.EnsureReferences();
            if (MapManager.instancia != null) MapManager.instancia.SetActiveContext(mapContext);
        }

        if (cameraRig == null) cameraRig = GetComponent<PuzzleCameraRig>();
        if (cameraRig == null) cameraRig = gameObject.AddComponent<PuzzleCameraRig>();

        // A HUD vive na UIScene e só emite pedidos; quem obedece é o estado ativo.
        SubscribeCommands();

        if (StageCount > 0) {
            GoToStage(0);
        } else {
            ShowDefaultView();
            PuzzleSignals.TriggerPuzzleStageChanged(this, -1, 0);
        }
    }

    public void GoToStage(int index) {
        if (stages == null || index < 0 || index >= stages.Count) {
            if (StageCount == 0) ShowDefaultView();
            return;
        }

        currentStage = index;
        PuzzleStage stage = stages[index];

        if (cameraRig != null) {
            cameraRig.SnapTo(stage.cameraAnchor, stage.cameraPosition, stage.orthographicSize);
        }

        PuzzleSignals.TriggerPuzzleStageChanged(this, currentStage, StageCount);
    }

    public void NextStage() {
        if (IsUnloading) return;

        if (StageCount == 0) {
            CompletePuzzle();
            return;
        }

        if (currentStage + 1 >= StageCount) {
            CompletePuzzle();
            return;
        }

        GoToStage(currentStage + 1);
    }

    public void PreviousStage() {
        if (IsUnloading || currentStage <= 0) return;
        GoToStage(currentStage - 1);
    }

    public void CompletePuzzle() {
        if (IsCompleted || IsUnloading) return;

        IsCompleted = true;
        PuzzleSignals.TriggerPuzzleCompleted(Puzzle);
        PuzzleSignals.TriggerPuzzleStageChanged(this, currentStage, StageCount);
    }

    // Restaura etapa/conclusão sem reemitir PuzzleCompleted nem recompensas.
    // O HUD/câmera são atualizados pelo evento de etapa.
    public void RestoreProgress(int savedStageIndex, bool savedCompleted) {
        if (IsUnloading || !IsStarted) return;

        if (savedCompleted) {
            IsCompleted = true;

            if (StageCount > 0) {
                int target = savedStageIndex >= 0
                    ? Mathf.Clamp(savedStageIndex, 0, StageCount - 1)
                    : StageCount - 1;

                if (target != currentStage) GoToStage(target);
                else PuzzleSignals.TriggerPuzzleStageChanged(this, currentStage, StageCount);
            } else {
                PuzzleSignals.TriggerPuzzleStageChanged(this, currentStage, StageCount);
            }

            return;
        }

        if (StageCount <= 0 || savedStageIndex < 0) return;

        int stage = Mathf.Clamp(savedStageIndex, 0, StageCount - 1);
        if (stage != currentStage) GoToStage(stage);
    }

    // Chamado pelo PuzzleManager antes do UnloadSceneAsync. Também pode ser
    // chamado por um botão de saída que não passe pelo manager.
    public void PrepareForUnload() {
        if (IsUnloading) return;
        IsUnloading = true;

        UnsubscribeCommands();

        if (restoreCameraOnUnload && cameraRig != null) cameraRig.Restore();

        if (mapContext != null) {
            mapContext.MarkUnloading();
        } else {
            foreach (var layers in GetComponentsInChildren<MapLayers>(true)) {
                if (layers != null) layers.MarkUnloading();
            }
        }

        foreach (var resource in GetComponentsInChildren<ResourceController>(true)) {
            if (resource != null) resource.PrepareForMapUnload();
        }

        if (MapManager.instancia != null) MapManager.instancia.ClearActiveContext();
        if (instancia == this) instancia = null;
    }

    private void SubscribeCommands() {
        PuzzleSignals.OnNextStageRequested -= HandleNextStageRequested;
        PuzzleSignals.OnPreviousStageRequested -= HandlePreviousStageRequested;
        PuzzleSignals.OnNextStageRequested += HandleNextStageRequested;
        PuzzleSignals.OnPreviousStageRequested += HandlePreviousStageRequested;
    }

    private void UnsubscribeCommands() {
        PuzzleSignals.OnNextStageRequested -= HandleNextStageRequested;
        PuzzleSignals.OnPreviousStageRequested -= HandlePreviousStageRequested;
    }

    private void HandleNextStageRequested() {
        if (instancia != this || IsUnloading) return;
        NextStage();
    }

    private void HandlePreviousStageRequested() {
        if (instancia != this || IsUnloading) return;
        PreviousStage();
    }

    private void ShowDefaultView() {
        if (cameraRig == null) return;
        cameraRig.SnapTo(defaultCameraAnchor, defaultCameraPosition, defaultOrthographicSize);
    }

    private MapContext FindContextInThisScene() {
        foreach (var context in GetComponentsInChildren<MapContext>(true)) {
            if (context != null && context.gameObject.scene == gameObject.scene) return context;
        }
        return null;
    }

    private void OnDestroy() {
        UnsubscribeCommands();
        if (!IsUnloading && !MapLayers.IsApplicationQuitting) PrepareForUnload();
        if (instancia == this) instancia = null;
    }
}
