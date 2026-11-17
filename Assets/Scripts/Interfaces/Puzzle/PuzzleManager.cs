using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Carrega/descarrega a cena aditiva de um desafio.
//
// O mapa principal não é pausado nem recarregado: o puzzle entra como cena
// adicional, com MapContext/MapLayers próprios. Ao sair, a cena é descarregada
// sem salvar progresso e a câmera volta ao estado anterior.
public class PuzzleManager : MonoBehaviour
{
    [Header("Catálogo")]
    [Tooltip("Catálogo usado pela PuzzleInterface. Vazio = procura um PuzzleCatalog no projeto.")]
    [SerializeField] public PuzzleCatalog catalog;

    [Header("Saída")]
    [SerializeField] private bool askBeforeExit = true;
    [Tooltip("Confirmação opcional (prefab de UI). Vazio = sai direto.")]
    [SerializeField] public PuzzleExitConfirmation exitConfirmation;
    [Tooltip("Canvas onde a confirmação é instanciada. Vazio = procura um Canvas na cena.")]
    [SerializeField] public Canvas confirmationCanvas;

    [Header("Ciclo de vida")]
    [SerializeField] private bool persistAcrossScenes = true;

    public static PuzzleManager instancia { get; private set; }

    public PuzzleInfo ActivePuzzle { get; private set; }
    public PuzzleStateManager ActiveState { get; private set; }
    public bool IsOpen => isOpen;
    public bool IsLoading => loading;

    private bool loading;
    private bool isOpen;
    private bool unloading;
    private bool closeRequested;
    private Scene activeScene;

    public PuzzleCatalog GetCatalog() {
        if (catalog != null) return catalog;

        var catalogs = Resources.FindObjectsOfTypeAll<PuzzleCatalog>();
        if (catalogs != null && catalogs.Length > 0) catalog = catalogs[0];
        return catalog;
    }

    // Garante um manager em runtime mesmo se o usuário ainda não o colocou na
    // cena. A PuzzleInterface usa isso como fallback.
    public static PuzzleManager EnsureInstance(PuzzleCatalog fallbackCatalog = null) {
        if (instancia != null) {
            if (fallbackCatalog != null && instancia.catalog == null) instancia.catalog = fallbackCatalog;
            return instancia;
        }

        var existing = FindObjectOfType<PuzzleManager>(true);
        if (existing != null) {
            instancia = existing;
            existing.gameObject.SetActive(true);
            if (fallbackCatalog != null && existing.catalog == null) existing.catalog = fallbackCatalog;
            return existing;
        }

        var go = new GameObject("PuzzleManager");
        var manager = go.AddComponent<PuzzleManager>();
        if (fallbackCatalog != null) manager.catalog = fallbackCatalog;
        return manager;
    }

    private void Awake() {
        if (instancia != null && instancia != this) {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        if (persistAcrossScenes && transform.parent == null) {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable() {
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        PuzzleSignals.OnExitRequested += HandleExitRequested;
    }

    private void OnDisable() {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        PuzzleSignals.OnExitRequested -= HandleExitRequested;
    }

    private void OnDestroy() {
        if (instancia == this) instancia = null;
    }

    public bool OpenPuzzle(PuzzleInfo puzzle) {
        if (puzzle == null || !puzzle.IsValid) {
            Debug.LogWarning("[PuzzleManager] Desafio inválido: falta a cena.", this);
            return false;
        }

        if (loading || unloading || isOpen) {
            Debug.LogWarning("[PuzzleManager] Já existe um desafio carregado/abrindo.", this);
            return false;
        }

        string sceneName = puzzle.sceneName.Trim();
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) {
            Debug.LogError("[PuzzleManager] A cena '" + sceneName + "' não está nos Build Settings.", this);
            return false;
        }

        ActivePuzzle = puzzle;
        loading = true;
        closeRequested = false;
        StartCoroutine(LoadPuzzleRoutine(puzzle));
        return true;
    }

    // Pedido de saída: mostra a confirmação opcional e, se confirmado, descarrega.
    public void RequestClose() {
        if (!isOpen && !loading) return;

        if (askBeforeExit && exitConfirmation != null) {
            PuzzleExitConfirmation confirmation = GetConfirmationInstance();
            if (confirmation != null) {
                confirmation.Show(
                    "Sair do desafio?",
                    "O mundo do desafio será reiniciado. O progresso dos objetivos será mantido.",
                    () => ClosePuzzleImmediate());
                return;
            }
        }

        ClosePuzzleImmediate();
    }

    // skipConfirmation = true força a saída sem perguntar.
    public void ClosePuzzle(bool skipConfirmation = false) {
        if (skipConfirmation || !askBeforeExit) {
            ClosePuzzleImmediate();
            return;
        }
        RequestClose();
    }

    private void ClosePuzzleImmediate() {
        if (unloading) return;

        // Ainda carregando: marca para descarregar assim que a cena terminar de entrar.
        if (loading && !activeScene.IsValid()) {
            closeRequested = true;
            return;
        }

        if (!isOpen && !activeScene.IsValid()) {
            loading = false;
            ActivePuzzle = null;
            ActiveState = null;
            return;
        }

        unloading = true;
        isOpen = false;
        StartCoroutine(UnloadPuzzleRoutine());
    }

    private IEnumerator LoadPuzzleRoutine(PuzzleInfo puzzle) {
        string sceneName = puzzle.sceneName.Trim();

        Scene alreadyLoaded = FindScene(sceneName);
        if (alreadyLoaded.IsValid() && alreadyLoaded.isLoaded) {
            Debug.LogError("[PuzzleManager] A cena '" + sceneName + "' já está carregada.", this);
            ResetAfterFailedLoad();
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (operation == null) {
            Debug.LogError("[PuzzleManager] Não foi possível iniciar o carregamento de '" + sceneName + "'.", this);
            ResetAfterFailedLoad();
            yield break;
        }

        yield return operation;

        Scene scene = FindScene(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) {
            Debug.LogError("[PuzzleManager] A cena '" + sceneName + "' não terminou de carregar.", this);
            ResetAfterFailedLoad();
            yield break;
        }

        activeScene = scene;

        if (closeRequested) {
            unloading = true;
            yield return UnloadActiveScene();
            ResetAfterUnload();
            yield break;
        }

        PuzzleStateManager state = FindComponentInScene<PuzzleStateManager>(scene);
        if (state == null) {
            Debug.LogError("[PuzzleManager] A cena '" + sceneName + "' não tem PuzzleStateManager.", this);
            unloading = true;
            yield return UnloadActiveScene();
            ResetAfterFailedLoad();
            yield break;
        }

        MapContext context = FindComponentInScene<MapContext>(scene);
        if (context != null) {
            context.EnsureReferences();
            if (MapManager.instancia != null) MapManager.instancia.SetActiveContext(context);
        } else {
            Debug.LogWarning("[PuzzleManager] A cena '" + sceneName + "' não tem MapContext: a colocação continuará no mapa principal.", this);
        }

        ActiveState = state;
        state.EnsureStarted(puzzle);

        ObjectiveManager objectives = ObjectiveManager.instancia;
        if (objectives != null &&
            objectives.TryGetPuzzleProgress(puzzle.Id, out int savedStage, out bool savedCompleted))
        {
            state.RestoreProgress(savedStage, savedCompleted);
        }

        loading = false;
        isOpen = true;
        PuzzleSignals.TriggerPuzzleStarted(puzzle);
    }

    private IEnumerator UnloadPuzzleRoutine() {
        PuzzleInfo puzzle = ActivePuzzle;

        if (ActiveState != null) ActiveState.PrepareForUnload();
        if (MapManager.instancia != null) MapManager.instancia.ClearActiveContext();

        // Avisa antes de destruir a HUD/interface da cena do puzzle.
        PuzzleSignals.TriggerPuzzleExited(puzzle);

        if (activeScene.IsValid() && activeScene.isLoaded) {
            yield return UnloadActiveScene();
        }

        ResetAfterUnload();
    }

    private IEnumerator UnloadActiveScene() {
        if (!activeScene.IsValid() || !activeScene.isLoaded) yield break;
        AsyncOperation operation = SceneManager.UnloadSceneAsync(activeScene);
        if (operation != null) yield return operation;
    }

    private void ResetAfterFailedLoad() {
        loading = false;
        isOpen = false;
        unloading = false;
        closeRequested = false;
        ActivePuzzle = null;
        ActiveState = null;
        activeScene = default;
    }

    private void ResetAfterUnload() {
        loading = false;
        isOpen = false;
        unloading = false;
        closeRequested = false;
        ActivePuzzle = null;
        ActiveState = null;
        activeScene = default;

        if (MapManager.instancia != null) MapManager.instancia.ClearActiveContext();
    }

    private void HandleExitRequested() {
        if (instancia != this) return;
        RequestClose();
    }

    private void HandleSceneUnloaded(Scene scene) {
        if (!activeScene.IsValid() || scene != activeScene) return;

        // Se o descarregamento foi iniciado pelo manager, a rotina cuida do estado.
        if (!unloading) {
            if (ActiveState != null) ActiveState.PrepareForUnload();
            PuzzleSignals.TriggerPuzzleExited(ActivePuzzle);
            ResetAfterUnload();
        }
    }

    private PuzzleExitConfirmation GetConfirmationInstance() {
        if (exitConfirmation == null) return null;

        // Objeto já existente na cena (normalmente desativado) pode ser usado direto.
        if (exitConfirmation.gameObject.scene.IsValid()) {
            exitConfirmation.gameObject.SetActive(true);
            exitConfirmation.SetDestroyOnHide(false);
            return exitConfirmation;
        }

        Canvas canvas = confirmationCanvas != null ? confirmationCanvas : FindCanvas();
        PuzzleExitConfirmation instance = canvas != null
            ? Instantiate(exitConfirmation, canvas.transform, false)
            : Instantiate(exitConfirmation);

        if (instance != null) {
            instance.SetDestroyOnHide(true);
            instance.transform.SetAsLastSibling();
        }
        return instance;
    }

    private Canvas FindCanvas() {
        var canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases) {
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return canvas;
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private static Scene FindScene(string sceneName) {
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid()) continue;
            if (string.Equals(scene.name, sceneName, System.StringComparison.OrdinalIgnoreCase)) return scene;
            if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(scene.path), sceneName, System.StringComparison.OrdinalIgnoreCase)) return scene;
        }
        return default;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component {
        if (!scene.IsValid()) return null;

        foreach (var root in scene.GetRootGameObjects()) {
            if (root == null) continue;
            var component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }
}
