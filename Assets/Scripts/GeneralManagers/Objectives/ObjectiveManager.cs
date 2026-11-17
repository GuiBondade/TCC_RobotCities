using System;
using System.Collections.Generic;
using UnityEngine;

// Núcleo de objetivos/missões. Não conhece a interface, não abre puzzles e não altera o mapa.
// A entrada é ObjectiveSignals; a saída são eventos para a UI e sistemas de desbloqueio.
[DefaultExecutionOrder(-100)]
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager instancia { get; private set; }

    [Header("Catálogo")]
    [SerializeField] private ObjectiveCatalog catalog;

    [Header("Persistência")]
    [SerializeField] private bool loadSavedProgress = true;
    [SerializeField] private bool saveProgress = true;
    [SerializeField] private string saveFileName = ObjectiveProgressStore.DefaultFileName;
    [Tooltip("Intervalo mínimo entre mudanças e gravação em disco, para não salvar a cada evento.")]
    [SerializeField, Min(0f)] private float saveDebounce = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logProgress = false;
    [SerializeField] private string debugSignalName;
    [SerializeField, Min(1)] private int debugSignalAmount = 1;

    private readonly Dictionary<string, ObjectiveRuntime> runtimeById =
        new Dictionary<string, ObjectiveRuntime>(StringComparer.Ordinal);

    private readonly List<ObjectiveRuntime> orderedRuntime = new List<ObjectiveRuntime>();
    private readonly List<ObjectiveRuntime> eventCandidates = new List<ObjectiveRuntime>();
    private readonly Queue<ObjectiveEvent> queuedEvents = new Queue<ObjectiveEvent>();
    private bool isProcessingEvent;

    private readonly HashSet<string> grantedPuzzleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> grantedPaletteIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> grantedConstructionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> grantedToolIds = new HashSet<string>(StringComparer.Ordinal);

    // Itens citados por algum objetivo. Só esses devem ser escondidos/desativados
    // pela integração futura; os demais preservam a configuração original.
    private readonly HashSet<string> managedPuzzleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> managedPaletteIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> managedConstructionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> managedToolIds = new HashSet<string>(StringComparer.Ordinal);

    private string pinnedMainObjectiveId;
    private readonly Dictionary<string, string> pinnedPuzzleObjectiveIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Cada puzzle tem o seu próprio contexto de execução. O dicionário é a
    // separação lógica entre os objetivos Main e os objetivos de cada puzzle.
    private readonly Dictionary<string, PuzzleObjectiveContext> puzzleContexts =
        new Dictionary<string, PuzzleObjectiveContext>(StringComparer.OrdinalIgnoreCase);
    private string activePuzzleId;
    private bool puzzleSignalsSubscribed;

    private bool initialized;
    private bool dirty;
    private float lastDirtyTime;

    public ObjectiveCatalog Catalog => catalog;
    public bool IsInitialized => initialized;
    public IReadOnlyList<ObjectiveRuntime> RuntimeObjectives => orderedRuntime;
    public string ActivePuzzleId => activePuzzleId ?? string.Empty;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Debug.LogWarning("[Objectives] Mais de um ObjectiveManager ativo. O duplicado será destruído.", this);
            Destroy(gameObject);
            return;
        }

        instancia = this;

        if (catalog == null) catalog = FindCatalog();
        InitializeRuntime();
        if (loadSavedProgress) LoadProgress();
    }

    private void OnEnable()
    {
        if (instancia != this) return;
        ObjectiveSignals.OnEvent -= HandleEvent;
        ObjectiveSignals.OnEvent += HandleEvent;
        SubscribePuzzleSignals();
        SyncActivePuzzleFromManager();
    }

    private void Start()
    {
        if (instancia != this || !initialized) return;

        // Cobre o caso de o manager ser habilitado com um puzzle já aberto.
        SyncActivePuzzleFromManager();

        // Se o save não trouxe um pin válido, escolhe o primeiro objetivo do mapa principal.
        EnsurePinnedObjective(ObjectiveScope.Main);

        // A UI assina antes de Start e recebe o estado inicial aqui.
        ObjectiveSignals.RaiseObjectivesReloaded();
        if (logProgress) LogState();
    }

    private void Update()
    {
        if (!dirty || !saveProgress) return;

        float debounce = Mathf.Max(0.05f, saveDebounce);
        if (Time.unscaledTime - lastDirtyTime >= debounce) SaveNow();
    }

    private void OnDisable()
    {
        ObjectiveSignals.OnEvent -= HandleEvent;
        UnsubscribePuzzleSignals();
        if (dirty) SaveNow();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && dirty) SaveNow();
    }

    private void OnApplicationQuit()
    {
        if (dirty) SaveNow();
    }

    private void OnDestroy()
    {
        ObjectiveSignals.OnEvent -= HandleEvent;
        UnsubscribePuzzleSignals();
        if (dirty) SaveNow();
        if (instancia == this) instancia = null;
    }

    // Ajuda a criar o manager em cenas que não têm o componente, sem duplicar instâncias.
    public static ObjectiveManager EnsureInstance(ObjectiveCatalog fallbackCatalog = null)
    {
        if (instancia != null)
        {
            if (fallbackCatalog != null && instancia.catalog == null)
            {
                instancia.catalog = fallbackCatalog;
                instancia.InitializeRuntime();
                if (instancia.loadSavedProgress) instancia.LoadProgress();
                instancia.SyncActivePuzzleFromManager();
            }
            return instancia;
        }

        ObjectiveManager existing = FindObjectOfType<ObjectiveManager>(true);
        if (existing != null)
        {
            if (fallbackCatalog != null && existing.catalog == null) existing.catalog = fallbackCatalog;

            if (!existing.gameObject.activeInHierarchy)
            {
                existing.gameObject.SetActive(true);
            }
            else if (!existing.IsInitialized && existing.catalog != null)
            {
                existing.InitializeRuntime();
                if (existing.loadSavedProgress) existing.LoadProgress();
                existing.SyncActivePuzzleFromManager();
            }

            return existing;
        }

        GameObject go = new GameObject("ObjectiveManager");
        go.SetActive(false);
        ObjectiveManager manager = go.AddComponent<ObjectiveManager>();
        manager.catalog = fallbackCatalog;
        go.SetActive(true);
        return manager;
    }

    private void InitializeRuntime()
    {
        runtimeById.Clear();
        orderedRuntime.Clear();
        grantedPuzzleIds.Clear();
        grantedPaletteIds.Clear();
        grantedConstructionIds.Clear();
        grantedToolIds.Clear();
        managedPuzzleIds.Clear();
        managedPaletteIds.Clear();
        managedConstructionIds.Clear();
        managedToolIds.Clear();
        pinnedMainObjectiveId = string.Empty;
        pinnedPuzzleObjectiveIds.Clear();
        puzzleContexts.Clear();
        activePuzzleId = string.Empty;
        queuedEvents.Clear();
        isProcessingEvent = false;
        dirty = false;
        initialized = false;

        if (catalog == null)
        {
            Debug.LogError("[Objectives] ObjectiveCatalog não configurado. O sistema de objetivos ficará inativo.", this);
            return;
        }

        IReadOnlyList<ObjectiveData> objectives = catalog.Objectives;
        if (objectives == null || objectives.Count == 0)
        {
            initialized = true;
            return;
        }

        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < objectives.Count; i++)
        {
            ObjectiveData data = objectives[i];
            if (data == null)
            {
                Debug.LogWarning($"[Objectives] Entrada nula no catálogo, índice {i}.", catalog);
                continue;
            }

            if (string.IsNullOrWhiteSpace(data.id))
            {
                Debug.LogWarning($"[Objectives] Objetivo '{data.name}' sem id. Ele será ignorado.", data);
                continue;
            }

            if (!seenIds.Add(data.id))
            {
                Debug.LogWarning($"[Objectives] Id duplicado '{data.id}'. A segunda entrada será ignorada.", data);
                continue;
            }

            if (data.scope == ObjectiveScope.Puzzle && string.IsNullOrWhiteSpace(data.puzzleId))
            {
                Debug.LogWarning($"[Objectives] Objetivo de puzzle '{data.id}' sem puzzleId.", data);
            }

            if (data.initialState == ObjectiveInitialState.Locked &&
                (data.prerequisites == null || data.prerequisites.Count == 0))
            {
                Debug.LogWarning(
                    $"[Objectives] Objetivo '{data.id}' começa Locked, mas não tem prerequisites. " +
                    "Use InProgress para objetivos raiz.", data);
            }

            ObjectiveRuntime runtime = new ObjectiveRuntime(data)
            {
                catalogIndex = i,
                state = data.initialState == ObjectiveInitialState.InProgress
                    ? ObjectiveState.InProgress
                    : ObjectiveState.Locked,
                progress = 0,
                activationProcessed = data.initialState == ObjectiveInitialState.InProgress,
                completionProcessed = false
            };

            runtimeById.Add(data.id, runtime);
            orderedRuntime.Add(runtime);
        }

        orderedRuntime.Sort(CompareRuntime);

        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            RegisterManagedRewards(runtime.data);

            ObjectiveData data = runtime.data;
            if (data.scope != ObjectiveScope.Puzzle || string.IsNullOrWhiteSpace(data.puzzleId))
                continue;

            PuzzleObjectiveContext context = GetOrCreatePuzzleContext(data.puzzleId);
            if (context != null) context.runtimes.Add(runtime);
        }

        ValidatePrerequisites();
        initialized = true;
    }

    private PuzzleObjectiveContext GetOrCreatePuzzleContext(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId)) return null;

        if (puzzleContexts.TryGetValue(puzzleId, out PuzzleObjectiveContext context))
            return context;

        context = new PuzzleObjectiveContext(puzzleId);
        puzzleContexts.Add(puzzleId, context);
        return context;
    }

    private void ValidatePrerequisites()
    {
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveData data = orderedRuntime[i].data;
            if (data == null || data.prerequisites == null) continue;

            for (int j = 0; j < data.prerequisites.Count; j++)
            {
                ObjectiveData prerequisite = data.prerequisites[j];
                if (prerequisite == null) continue;

                if (prerequisite.scope != data.scope)
                {
                    Debug.LogWarning(
                        $"[Objectives] Objetivo '{data.id}' tem prerequisite '{prerequisite.id}' " +
                        $"de escopo diferente ({prerequisite.scope} -> {data.scope}). " +
                        "Mantenha a cadeia dentro do mesmo ambiente.", data);
                    continue;
                }

                if (data.scope == ObjectiveScope.Puzzle &&
                    !string.Equals(prerequisite.puzzleId, data.puzzleId, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[Objectives] Objetivo '{data.id}' (puzzle '{data.puzzleId}') tem prerequisite " +
                        $"'{prerequisite.id}' do puzzle '{prerequisite.puzzleId}'. " +
                        "Objetivos de puzzle devem encadear apenas objetivos do mesmo puzzle.", data);
                }
            }
        }
    }

    private void RegisterManagedRewards(ObjectiveData data)
    {
        if (data == null) return;

        if (data.puzzlesToUnlock != null)
        {
            for (int i = 0; i < data.puzzlesToUnlock.Count; i++)
            {
                string puzzleId = data.puzzlesToUnlock[i];
                if (!string.IsNullOrWhiteSpace(puzzleId)) managedPuzzleIds.Add(puzzleId);
            }
        }

        if (data.paletteItemsToUnlock != null)
        {
            for (int i = 0; i < data.paletteItemsToUnlock.Count; i++)
            {
                string blockId = GetPaletteKey(data.paletteItemsToUnlock[i]);
                if (!string.IsNullOrEmpty(blockId)) managedPaletteIds.Add(blockId);
            }
        }

        if (data.constructionsToUnlock != null)
        {
            for (int i = 0; i < data.constructionsToUnlock.Count; i++)
            {
                string constructionId = GetConstructionKey(data.constructionsToUnlock[i]);
                if (!string.IsNullOrEmpty(constructionId)) managedConstructionIds.Add(constructionId);
            }
        }

        if (data.toolsToUnlock != null)
        {
            for (int i = 0; i < data.toolsToUnlock.Count; i++)
            {
                string toolId = GetToolKey(data.toolsToUnlock[i]);
                if (!string.IsNullOrEmpty(toolId)) managedToolIds.Add(toolId);
            }
        }
    }

    private void LoadProgress()
    {
        ObjectiveProgressData saved = ObjectiveProgressStore.Load(saveFileName);
        if (saved == null) return;

        if (saved.objectives != null)
        {
            for (int i = 0; i < saved.objectives.Count; i++)
            {
                ObjectiveProgressEntry entry = saved.objectives[i];
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (!runtimeById.TryGetValue(entry.id, out ObjectiveRuntime runtime)) continue;

                ApplySavedEntry(runtime, entry);
            }

            // Reconstitui recompensas a partir dos objetivos concluídos.
            // Assim um save antigo/parcial não deixa a paleta ou os puzzles bloqueados.
            for (int i = 0; i < orderedRuntime.Count; i++)
            {
                ObjectiveRuntime runtime = orderedRuntime[i];
                if (runtime.state != ObjectiveState.Completed) continue;

                runtime.progress = runtime.Target;
                runtime.completionProcessed = true;
                AddRewardsToSets(runtime.data);
            }

            RefreshLockedObjectivesSilent();
        }

        // O pin principal e os pins de cada puzzle são independentes.
        pinnedMainObjectiveId = saved.pinnedMainObjectiveId ?? string.Empty;
        LoadPuzzlePins(saved);
        LoadPuzzleProgress(saved);
        dirty = false;
    }

    private void LoadPuzzlePins(ObjectiveProgressData saved)
    {
        pinnedPuzzleObjectiveIds.Clear();
        if (saved == null || saved.puzzlePins == null) return;

        for (int i = 0; i < saved.puzzlePins.Count; i++)
        {
            PuzzleObjectivePin pin = saved.puzzlePins[i];
            if (pin == null) continue;
            if (string.IsNullOrWhiteSpace(pin.puzzleId) || string.IsNullOrWhiteSpace(pin.objectiveId))
                continue;

            ObjectiveRuntime runtime = GetRuntime(pin.objectiveId);
            if (!IsValidPinnedRuntime(runtime, ObjectiveScope.Puzzle, pin.puzzleId))
                continue;

            pinnedPuzzleObjectiveIds[pin.puzzleId] = pin.objectiveId;
        }
    }

    private void LoadPuzzleProgress(ObjectiveProgressData saved)
    {
        if (saved == null || saved.puzzles == null) return;

        for (int i = 0; i < saved.puzzles.Count; i++)
        {
            PuzzleProgressEntry entry = saved.puzzles[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.puzzleId)) continue;

            PuzzleObjectiveContext context = GetOrCreatePuzzleContext(entry.puzzleId);
            if (context == null) continue;

            context.stageIndex = entry.stageIndex;
            context.completed = entry.completed;
            context.lastPlayedUtc = entry.lastPlayedUtc;
        }
    }

    private static void ApplySavedEntry(ObjectiveRuntime runtime, ObjectiveProgressEntry entry)
    {
        runtime.progress = Mathf.Clamp(entry.progress, 0, runtime.Target);

        if (entry.state == ObjectiveState.Completed || runtime.progress >= runtime.Target)
        {
            runtime.state = ObjectiveState.Completed;
            runtime.progress = runtime.Target;
            runtime.activationProcessed = true;
            runtime.completionProcessed = true;
            return;
        }

        if (entry.state == ObjectiveState.InProgress)
        {
            runtime.state = ObjectiveState.InProgress;
            runtime.activationProcessed = true;
            runtime.completionProcessed = false;
            return;
        }

        runtime.state = ObjectiveState.Locked;
        runtime.activationProcessed = false;
        runtime.completionProcessed = false;
    }

    private void RefreshLockedObjectivesSilent()
    {
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            if (runtime.state != ObjectiveState.Locked) continue;
            if (!HasAnyCompletedPrerequisite(runtime.data)) continue;

            runtime.state = ObjectiveState.InProgress;
            runtime.activationProcessed = true;
        }
    }

    private bool HasAnyCompletedPrerequisite(ObjectiveData data)
    {
        if (data == null || data.prerequisites == null) return false;

        // Lógica OU: qualquer prerequisite concluído basta.
        for (int i = 0; i < data.prerequisites.Count; i++)
        {
            ObjectiveData prerequisite = data.prerequisites[i];
            if (prerequisite == null || string.IsNullOrEmpty(prerequisite.id)) continue;
            if (!runtimeById.TryGetValue(prerequisite.id, out ObjectiveRuntime runtime)) continue;
            if (runtime.state == ObjectiveState.Completed) return true;
        }
        return false;
    }

    private void HandleEvent(ObjectiveEvent evt)
    {
        if (!initialized) return;
        if (evt.amount <= 0 || string.IsNullOrEmpty(evt.key)) return;

        // Se um handler de saída emitir outro evento, ele entra na fila em vez de
        // reentrar no mesmo eventCandidates.
        if (isProcessingEvent)
        {
            queuedEvents.Enqueue(evt);
            return;
        }

        isProcessingEvent = true;
        try
        {
            ProcessEvent(evt);
            while (queuedEvents.Count > 0) ProcessEvent(queuedEvents.Dequeue());
        }
        finally
        {
            isProcessingEvent = false;
        }
    }

    private void ProcessEvent(ObjectiveEvent evt)
    {
        // Snapshot dos objetivos que já estavam em progresso antes deste evento.
        // Um desbloqueio disparado por este mesmo evento não pode consumi-lo retroativamente.
        eventCandidates.Clear();
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            if (runtime.state != ObjectiveState.InProgress) continue;
            if (!MatchesScope(runtime.data, evt)) continue;
            eventCandidates.Add(runtime);
        }

        for (int i = 0; i < eventCandidates.Count; i++)
        {
            ObjectiveRuntime runtime = eventCandidates[i];
            if (runtime.state != ObjectiveState.InProgress) continue;

            int amount = runtime.data.GetEventAmount(evt);
            if (amount > 0) AddProgress(runtime, amount);
        }
    }

    private bool MatchesScope(ObjectiveData data, ObjectiveEvent evt)
    {
        if (data == null) return false;
        if (!IsRuntimeActive(data)) return false;

        // Origem nula = chamada global/debug. O fluxo real sempre passa o MapContext.
        if (evt.originContext == null) return true;

        if (evt.IsGlobal)
        {
            if (data.scope == ObjectiveScope.Main) return true;
            return data.scope == ObjectiveScope.Puzzle &&
                   !string.IsNullOrEmpty(data.puzzleId) &&
                   string.Equals(data.puzzleId, evt.key, StringComparison.OrdinalIgnoreCase);
        }

        if (data.scope == ObjectiveScope.Main)
        {
            if (evt.originContext.isMain) return true;
            return MapManager.instancia != null &&
                   evt.originContext == MapManager.instancia.mainContext;
        }

        if (string.IsNullOrWhiteSpace(data.puzzleId)) return false;
        if (string.Equals(data.puzzleId, evt.originContext.mapId, StringComparison.OrdinalIgnoreCase)) return true;

        return PuzzleManager.instancia != null &&
               PuzzleManager.instancia.ActiveState != null &&
               PuzzleManager.instancia.ActiveState.mapContext == evt.originContext;
    }

    // Main está sempre ativo. Um objetivo de puzzle só pode processar eventos
    // quando o seu contexto é o puzzle atualmente aberto.
    private bool IsRuntimeActive(ObjectiveData data)
    {
        if (data == null) return false;
        if (data.scope == ObjectiveScope.Main) return true;
        if (string.IsNullOrWhiteSpace(data.puzzleId)) return false;
        if (string.IsNullOrWhiteSpace(activePuzzleId)) return false;

        return string.Equals(data.puzzleId, activePuzzleId, StringComparison.OrdinalIgnoreCase);
    }

    private void AddProgress(ObjectiveRuntime runtime, int amount)
    {
        if (runtime == null || runtime.data == null) return;
        if (runtime.state != ObjectiveState.InProgress) return;

        int target = runtime.Target;
        if (runtime.progress >= target)
        {
            CompleteObjective(runtime);
            return;
        }

        int previous = runtime.progress;
        int next = Mathf.Clamp(previous + Mathf.Max(1, amount), 0, target);
        if (next == previous) return;

        runtime.progress = next;
        ObjectiveSignals.RaiseProgressChanged(runtime.data, runtime.progress, target);
        if (logProgress) Debug.Log($"[Objectives] {runtime.data.id}: {runtime.progress}/{target}", runtime.data);

        MarkDirty();

        if (runtime.progress >= target) CompleteObjective(runtime);
    }

    private void CompleteObjective(ObjectiveRuntime runtime)
    {
        if (runtime == null || runtime.data == null) return;
        if (runtime.state == ObjectiveState.Completed && runtime.completionProcessed) return;

        runtime.state = ObjectiveState.Completed;
        runtime.progress = runtime.Target;
        runtime.activationProcessed = true;
        runtime.completionProcessed = true;

        ObjectiveSignals.RaiseStateChanged(runtime.data, ObjectiveState.Completed);
        ObjectiveSignals.RaiseProgressChanged(runtime.data, runtime.progress, runtime.Target);
        ObjectiveSignals.RaiseObjectiveCompleted(runtime.data);
        if (logProgress) Debug.Log($"[Objectives] Concluído: {runtime.data.id}", runtime.data);

        GrantRewards(runtime.data);
        UnlockObjectivesWithCompletedPrerequisites();
        RevalidatePinnedObjectiveAfterCompletion(runtime.data);
        MarkDirty();
    }

    private void UnlockObjective(ObjectiveRuntime runtime)
    {
        if (runtime == null || runtime.data == null) return;
        if (runtime.state != ObjectiveState.Locked) return;

        runtime.state = ObjectiveState.InProgress;
        runtime.activationProcessed = true;

        ObjectiveSignals.RaiseStateChanged(runtime.data, ObjectiveState.InProgress);
        ObjectiveSignals.RaiseObjectiveUnlocked(runtime.data);
        if (logProgress) Debug.Log($"[Objectives] Desbloqueado: {runtime.data.id}", runtime.data);

        MarkDirty();

        // Se o jogador ainda não fixou nada, o primeiro objetivo em progresso
        // aparece na HUD. Um pin existente nunca é substituído por um desbloqueio.
        EnsurePinnedObjectiveIfMissing(runtime.data.scope, runtime.data.puzzleId);
    }

    private void UnlockObjectivesWithCompletedPrerequisites()
    {
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            if (runtime.state != ObjectiveState.Locked) continue;
            if (HasAnyCompletedPrerequisite(runtime.data)) UnlockObjective(runtime);
        }
    }

    private void GrantRewards(ObjectiveData data)
    {
        if (data == null) return;

        if (data.puzzlesToUnlock != null)
        {
            for (int i = 0; i < data.puzzlesToUnlock.Count; i++)
            {
                string puzzleId = data.puzzlesToUnlock[i];
                if (string.IsNullOrWhiteSpace(puzzleId)) continue;
                if (!grantedPuzzleIds.Add(puzzleId)) continue;
                ObjectiveSignals.RaiseUnlockGranted(ObjectiveUnlockType.Puzzle, puzzleId, data);
            }
        }

        if (data.paletteItemsToUnlock != null)
        {
            for (int i = 0; i < data.paletteItemsToUnlock.Count; i++)
            {
                string blockId = GetPaletteKey(data.paletteItemsToUnlock[i]);
                if (string.IsNullOrEmpty(blockId)) continue;
                if (!grantedPaletteIds.Add(blockId)) continue;
                ObjectiveSignals.RaiseUnlockGranted(ObjectiveUnlockType.PaletteItem, blockId, data);
            }
        }

        if (data.constructionsToUnlock != null)
        {
            for (int i = 0; i < data.constructionsToUnlock.Count; i++)
            {
                string constructionId = GetConstructionKey(data.constructionsToUnlock[i]);
                if (string.IsNullOrEmpty(constructionId)) continue;
                if (!grantedConstructionIds.Add(constructionId)) continue;
                ObjectiveSignals.RaiseUnlockGranted(ObjectiveUnlockType.Construction, constructionId, data);
            }
        }

        if (data.toolsToUnlock != null)
        {
            for (int i = 0; i < data.toolsToUnlock.Count; i++)
            {
                string toolId = GetToolKey(data.toolsToUnlock[i]);
                if (string.IsNullOrEmpty(toolId)) continue;
                if (!grantedToolIds.Add(toolId)) continue;
                ObjectiveSignals.RaiseUnlockGranted(ObjectiveUnlockType.Tool, toolId, data);
            }
        }
    }

    private void AddRewardsToSets(ObjectiveData data)
    {
        if (data == null) return;

        if (data.puzzlesToUnlock != null)
        {
            for (int i = 0; i < data.puzzlesToUnlock.Count; i++)
            {
                string puzzleId = data.puzzlesToUnlock[i];
                if (!string.IsNullOrWhiteSpace(puzzleId)) grantedPuzzleIds.Add(puzzleId);
            }
        }

        if (data.paletteItemsToUnlock != null)
        {
            for (int i = 0; i < data.paletteItemsToUnlock.Count; i++)
            {
                string blockId = GetPaletteKey(data.paletteItemsToUnlock[i]);
                if (!string.IsNullOrEmpty(blockId)) grantedPaletteIds.Add(blockId);
            }
        }

        if (data.constructionsToUnlock != null)
        {
            for (int i = 0; i < data.constructionsToUnlock.Count; i++)
            {
                string constructionId = GetConstructionKey(data.constructionsToUnlock[i]);
                if (!string.IsNullOrEmpty(constructionId)) grantedConstructionIds.Add(constructionId);
            }
        }

        if (data.toolsToUnlock != null)
        {
            for (int i = 0; i < data.toolsToUnlock.Count; i++)
            {
                string toolId = GetToolKey(data.toolsToUnlock[i]);
                if (!string.IsNullOrEmpty(toolId)) grantedToolIds.Add(toolId);
            }
        }
    }

    private static string GetPaletteKey(BlockData blockData)
    {
        if (blockData == null) return null;
        return !string.IsNullOrEmpty(blockData.id) ? blockData.id : blockData.name;
    }

    private static string GetConstructionKey(ConstructionData constructionData)
    {
        if (constructionData == null) return null;
        return !string.IsNullOrEmpty(constructionData.id) ? constructionData.id.Trim() : constructionData.name.Trim();
    }

    private static string GetToolKey(ToolData toolData)
    {
        if (toolData == null) return null;
        return !string.IsNullOrEmpty(toolData.id.ToString()) ? toolData.id.ToString().Trim() : toolData.displayName.Trim();
    }

    public ObjectiveRuntime GetRuntime(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        runtimeById.TryGetValue(id, out ObjectiveRuntime runtime);
        return runtime;
    }

    public bool TryGetRuntime(string id, out ObjectiveRuntime runtime)
    {
        runtime = null;
        if (string.IsNullOrEmpty(id)) return false;
        return runtimeById.TryGetValue(id, out runtime);
    }

    public bool IsUnlocked(string id)
    {
        ObjectiveRuntime runtime = GetRuntime(id);
        return runtime != null && runtime.state != ObjectiveState.Locked;
    }

    public bool IsCompleted(string id)
    {
        ObjectiveRuntime runtime = GetRuntime(id);
        return runtime != null && runtime.state == ObjectiveState.Completed;
    }

    public ObjectiveState GetState(string id)
    {
        ObjectiveRuntime runtime = GetRuntime(id);
        return runtime != null ? runtime.state : ObjectiveState.Locked;
    }

    public int GetProgress(string id)
    {
        ObjectiveRuntime runtime = GetRuntime(id);
        return runtime != null ? runtime.progress : 0;
    }

    public string GetFormattedProgress(string id)
    {
        ObjectiveRuntime runtime = GetRuntime(id);
        return runtime != null ? runtime.BuildProgressText() : string.Empty;
    }

    public IReadOnlyList<ObjectiveData> GetVisibleObjectives()
    {
        List<ObjectiveData> visible = new List<ObjectiveData>();
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveData data = orderedRuntime[i].data;
            if (data != null && data.visibleInHud) visible.Add(data);
        }
        return visible;
    }

    // Versão usada pela UI: já filtra escopo, puzzle e visibilidade.
    public IReadOnlyList<ObjectiveRuntime> GetVisibleRuntimeObjectives(
        ObjectiveScope scope, string puzzleId = null)
    {
        List<ObjectiveRuntime> result = new List<ObjectiveRuntime>();
        if (!initialized) return result;

        string normalizedPuzzleId = scope == ObjectiveScope.Puzzle
            ? (puzzleId ?? string.Empty)
            : string.Empty;

        // Sem puzzleId não há como separar os objetivos de puzzles diferentes.
        if (scope == ObjectiveScope.Puzzle && string.IsNullOrWhiteSpace(normalizedPuzzleId))
            return result;

        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            ObjectiveData data = runtime != null ? runtime.data : null;
            if (data == null || !data.visibleInHud) continue;
            if (data.scope != scope) continue;

            if (scope == ObjectiveScope.Puzzle &&
                !string.Equals(data.puzzleId, normalizedPuzzleId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(runtime);
        }

        return result;
    }

    public string GetPinnedObjectiveId(ObjectiveScope scope, string puzzleId = null)
    {
        if (scope == ObjectiveScope.Main)
            return pinnedMainObjectiveId ?? string.Empty;

        if (string.IsNullOrWhiteSpace(puzzleId)) return string.Empty;
        return pinnedPuzzleObjectiveIds.TryGetValue(puzzleId, out string objectiveId)
            ? (objectiveId ?? string.Empty)
            : string.Empty;
    }

    public ObjectiveRuntime GetPinnedObjectiveRuntime(ObjectiveScope scope, string puzzleId = null)
    {
        return GetRuntime(GetPinnedObjectiveId(scope, puzzleId));
    }

    public bool TryPinObjective(string objectiveId)
    {
        if (!initialized || string.IsNullOrEmpty(objectiveId)) return false;
        if (!runtimeById.TryGetValue(objectiveId, out ObjectiveRuntime runtime)) return false;
        if (runtime.data == null || runtime.state != ObjectiveState.InProgress) return false;
        if (!runtime.data.visibleInHud) return false;

        if (runtime.data.scope == ObjectiveScope.Puzzle &&
            string.IsNullOrWhiteSpace(runtime.data.puzzleId))
        {
            return false;
        }

        SetPinnedObjective(runtime.data.scope, runtime.data.puzzleId, runtime.data.id);
        return true;
    }

    public void ClearPinnedObjective(ObjectiveScope scope, string puzzleId = null)
    {
        SetPinnedObjective(scope, puzzleId, string.Empty);
    }

    public string EnsurePinnedObjective(ObjectiveScope scope, string puzzleId = null)
    {
        if (!initialized) return string.Empty;

        ObjectiveRuntime current = GetPinnedObjectiveRuntime(scope, puzzleId);
        if (IsValidPinnedRuntime(current, scope, puzzleId))
            return current.data.id;

        string nextId = string.Empty;
        IReadOnlyList<ObjectiveRuntime> visible = GetVisibleRuntimeObjectives(scope, puzzleId);

        for (int i = 0; i < visible.Count; i++)
        {
            ObjectiveRuntime candidate = visible[i];
            if (candidate != null && candidate.state == ObjectiveState.InProgress)
            {
                nextId = candidate.data.id;
                break;
            }
        }

        SetPinnedObjective(scope, puzzleId, nextId);
        return nextId;
    }

    private bool IsValidPinnedRuntime(
        ObjectiveRuntime runtime, ObjectiveScope scope, string puzzleId)
    {
        if (runtime == null || runtime.data == null) return false;
        if (runtime.state != ObjectiveState.InProgress) return false;
        if (!runtime.data.visibleInHud) return false;
        if (runtime.data.scope != scope) return false;

        if (scope == ObjectiveScope.Puzzle)
        {
            if (string.IsNullOrWhiteSpace(puzzleId)) return false;
            if (!string.Equals(runtime.data.puzzleId, puzzleId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void SetPinnedObjective(ObjectiveScope scope, string puzzleId, string objectiveId)
    {
        string normalizedId = string.IsNullOrWhiteSpace(objectiveId)
            ? string.Empty
            : objectiveId;

        string previous = GetPinnedObjectiveId(scope, puzzleId);
        if (string.Equals(previous, normalizedId, StringComparison.Ordinal)) return;

        if (scope == ObjectiveScope.Main)
        {
            pinnedMainObjectiveId = normalizedId;
            MarkDirty();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(puzzleId)) return;

            if (string.IsNullOrEmpty(normalizedId))
                pinnedPuzzleObjectiveIds.Remove(puzzleId);
            else
                pinnedPuzzleObjectiveIds[puzzleId] = normalizedId;

            MarkDirty();
        }

        ObjectiveSignals.RaisePinnedObjectiveChanged(
            scope,
            scope == ObjectiveScope.Puzzle ? puzzleId : string.Empty,
            normalizedId);
    }

    private void EnsurePinnedObjectiveIfMissing(
        ObjectiveScope scope, string puzzleId = null)
    {
        if (!initialized) return;

        // Mantém a escolha do jogador enquanto ela for válida. Só escolhe
        // automaticamente quando não há pin ou quando o pin atual não serve mais.
        ObjectiveRuntime current = GetPinnedObjectiveRuntime(scope, puzzleId);
        if (IsValidPinnedRuntime(current, scope, puzzleId)) return;

        EnsurePinnedObjective(scope, puzzleId);
    }

    private void RevalidatePinnedObjectiveAfterCompletion(ObjectiveData completed)
    {
        if (!initialized || completed == null) return;

        string pinnedId = GetPinnedObjectiveId(completed.scope, completed.puzzleId);
        if (string.IsNullOrEmpty(pinnedId)) return;
        if (!string.Equals(pinnedId, completed.id, StringComparison.Ordinal)) return;

        EnsurePinnedObjective(completed.scope, completed.puzzleId);
    }

    public bool IsPuzzleActive(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || string.IsNullOrWhiteSpace(activePuzzleId))
            return false;

        if (!string.Equals(puzzleId, activePuzzleId, StringComparison.OrdinalIgnoreCase))
            return false;

        return puzzleContexts.TryGetValue(puzzleId, out PuzzleObjectiveContext context) &&
               context != null &&
               context.isActive;
    }

    public bool TryGetPuzzleProgress(string puzzleId, out int stageIndex, out bool completed)
    {
        stageIndex = -1;
        completed = false;

        if (string.IsNullOrWhiteSpace(puzzleId)) return false;
        if (!puzzleContexts.TryGetValue(puzzleId, out PuzzleObjectiveContext context) || context == null)
            return false;

        stageIndex = context.stageIndex;
        completed = context.completed;
        return true;
    }

    // Ativa o contexto do puzzle e restaura o pin daquele ambiente.
    public void ActivatePuzzle(string puzzleId)
    {
        if (!initialized || string.IsNullOrWhiteSpace(puzzleId)) return;

        if (!string.IsNullOrWhiteSpace(activePuzzleId) &&
            !string.Equals(activePuzzleId, puzzleId, StringComparison.OrdinalIgnoreCase))
        {
            DeactivatePuzzleInternal(activePuzzleId, false);
        }

        PuzzleObjectiveContext context = GetOrCreatePuzzleContext(puzzleId);
        if (context == null) return;

        if (context.isActive &&
            string.Equals(activePuzzleId, context.puzzleId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.isActive = true;
        activePuzzleId = context.puzzleId;
        context.lastPlayedUtc = DateTime.UtcNow.ToString("o");

        PuzzleStateManager state = PuzzleManager.instancia != null
            ? PuzzleManager.instancia.ActiveState
            : null;
        if (context.stageIndex < 0 && state != null)
            context.stageIndex = state.CurrentStageIndex;

        // Cobre o caso de o ObjectiveManager ser inicializado depois da abertura
        // do puzzle: restaura etapa/conclusão sem depender do fluxo normal.
        if (state != null)
        {
            bool needsRestore = (context.completed && !state.IsCompleted) ||
                                (context.stageIndex >= 0 && context.stageIndex != state.CurrentStageIndex);
            if (needsRestore)
                state.RestoreProgress(context.stageIndex, context.completed);
        }

        EnsurePinnedObjective(ObjectiveScope.Puzzle, context.puzzleId);
        MarkDirty();

        if (logProgress)
            Debug.Log($"[Objectives] Contexto ativado: puzzle '{context.puzzleId}'.", this);
    }

    // Encerra o processamento do puzzle. O progresso e o pin continuam salvos.
    public void DeactivatePuzzle(string puzzleId)
    {
        DeactivatePuzzleInternal(puzzleId, true);
    }

    private void DeactivatePuzzleInternal(string puzzleId, bool saveImmediately)
    {
        string normalizedId = !string.IsNullOrWhiteSpace(puzzleId) ? puzzleId : activePuzzleId;
        if (string.IsNullOrWhiteSpace(normalizedId)) return;

        bool isCurrent = !string.IsNullOrWhiteSpace(activePuzzleId) &&
                         string.Equals(activePuzzleId, normalizedId, StringComparison.OrdinalIgnoreCase);

        if (isCurrent) CaptureActivePuzzleState();

        if (puzzleContexts.TryGetValue(normalizedId, out PuzzleObjectiveContext context) && context != null)
            context.isActive = false;

        if (isCurrent) activePuzzleId = string.Empty;

        MarkDirty();
        if (saveImmediately && saveProgress) SaveNow();

        if (logProgress)
            Debug.Log($"[Objectives] Contexto desativado: puzzle '{normalizedId}'.", this);
    }

    private void CaptureActivePuzzleState()
    {
        PuzzleManager manager = PuzzleManager.instancia;
        if (manager == null) return;

        PuzzleInfo puzzle = manager.ActivePuzzle;
        PuzzleStateManager state = manager.ActiveState;
        if (puzzle == null || state == null) return;

        PuzzleObjectiveContext context = GetOrCreatePuzzleContext(puzzle.Id);
        if (context == null) return;

        context.stageIndex = state.CurrentStageIndex;
        context.completed = state.IsCompleted;
    }

    private void SubscribePuzzleSignals()
    {
        if (puzzleSignalsSubscribed) return;

        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleExited -= HandlePuzzleExited;
        PuzzleSignals.OnPuzzleCompleted -= HandlePuzzleCompleted;
        PuzzleSignals.OnPuzzleStageChanged -= HandlePuzzleStageChanged;

        PuzzleSignals.OnPuzzleStarted += HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleExited += HandlePuzzleExited;
        PuzzleSignals.OnPuzzleCompleted += HandlePuzzleCompleted;
        PuzzleSignals.OnPuzzleStageChanged += HandlePuzzleStageChanged;

        puzzleSignalsSubscribed = true;
    }

    private void UnsubscribePuzzleSignals()
    {
        if (!puzzleSignalsSubscribed) return;

        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleExited -= HandlePuzzleExited;
        PuzzleSignals.OnPuzzleCompleted -= HandlePuzzleCompleted;
        PuzzleSignals.OnPuzzleStageChanged -= HandlePuzzleStageChanged;

        puzzleSignalsSubscribed = false;
    }

    private void SyncActivePuzzleFromManager()
    {
        if (!initialized) return;

        PuzzleManager manager = PuzzleManager.instancia;
        if (manager != null && manager.IsOpen && manager.ActivePuzzle != null)
        {
            ActivatePuzzle(manager.ActivePuzzle.Id);
            return;
        }

        if (!string.IsNullOrWhiteSpace(activePuzzleId))
            DeactivatePuzzleInternal(activePuzzleId, false);
    }

    private void HandlePuzzleStarted(PuzzleInfo puzzle)
    {
        if (puzzle == null) return;
        ActivatePuzzle(puzzle.Id);
    }

    private void HandlePuzzleExited(PuzzleInfo puzzle)
    {
        DeactivatePuzzle(puzzle != null ? puzzle.Id : activePuzzleId);
    }

    private void HandlePuzzleCompleted(PuzzleInfo puzzle)
    {
        if (puzzle == null) return;

        PuzzleObjectiveContext context = GetOrCreatePuzzleContext(puzzle.Id);
        if (context != null)
        {
            context.completed = true;

            PuzzleStateManager state = PuzzleManager.instancia != null
                ? PuzzleManager.instancia.ActiveState
                : null;
            if (state != null) context.stageIndex = state.CurrentStageIndex;
        }

        MarkDirty();

        // PuzzleCompleted é global: pode alimentar objetivos do mapa principal.
        ObjectiveSignals.TriggerEvent(
            new ObjectiveEvent(ObjectiveEventType.PuzzleCompleted, puzzle.Id, 1));
    }

    private void HandlePuzzleStageChanged(PuzzleStateManager state, int stageIndex, int stageCount)
    {
        if (state == null) return;

        string puzzleId = state.Puzzle != null ? state.Puzzle.Id : activePuzzleId;
        if (string.IsNullOrWhiteSpace(puzzleId)) return;
        if (!string.Equals(puzzleId, activePuzzleId, StringComparison.OrdinalIgnoreCase)) return;

        PuzzleObjectiveContext context = GetOrCreatePuzzleContext(puzzleId);
        if (context == null || !context.isActive) return;

        context.stageIndex = stageIndex;
        MarkDirty();
    }

    // Inclui objetivos ocultos: é consulta de lógica, não de HUD.
    public IReadOnlyList<ObjectiveData> GetObjectivesByScope(ObjectiveScope scope, string puzzleId = null)
    {
        List<ObjectiveData> result = new List<ObjectiveData>();
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveData data = orderedRuntime[i].data;
            if (data == null || data.scope != scope) continue;

            if (scope == ObjectiveScope.Puzzle &&
                !string.IsNullOrEmpty(puzzleId) &&
                !string.Equals(data.puzzleId, puzzleId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(data);
        }
        return result;
    }

    public bool IsPuzzleManaged(string puzzleId)
    {
        return !string.IsNullOrEmpty(puzzleId) && managedPuzzleIds.Contains(puzzleId);
    }

    public bool IsPuzzleUnlocked(string puzzleId)
    {
        return !string.IsNullOrEmpty(puzzleId) && grantedPuzzleIds.Contains(puzzleId);
    }

    public bool IsPaletteItemManaged(string blockId)
    {
        return !string.IsNullOrEmpty(blockId) && managedPaletteIds.Contains(blockId);
    }

    public bool IsPaletteItemManaged(BlockData blockData)
    {
        return IsPaletteItemManaged(GetPaletteKey(blockData));
    }

    public bool IsPaletteItemUnlocked(string blockId)
    {
        return !string.IsNullOrEmpty(blockId) && grantedPaletteIds.Contains(blockId);
    }

    public bool IsPaletteItemUnlocked(BlockData blockData)
    {
        return IsPaletteItemUnlocked(GetPaletteKey(blockData));
    }

    public bool IsConstructionManaged(string constructionId)
    {
        return !string.IsNullOrEmpty(constructionId) && managedConstructionIds.Contains(constructionId);
    }

    public bool IsConstructionManaged(ConstructionData constructionData)
    {
        return IsConstructionManaged(GetConstructionKey(constructionData));
    }

    public bool IsConstructionUnlocked(string constructionId)
    {
        return !string.IsNullOrEmpty(constructionId) && grantedConstructionIds.Contains(constructionId);
    }

    public bool IsConstructionUnlocked(ConstructionData constructionData)
    {
        return IsConstructionUnlocked(GetConstructionKey(constructionData));
    }

    public bool IsToolManaged(string toolId)
    {
        return !string.IsNullOrEmpty(toolId) && managedToolIds.Contains(toolId);
    }

    public bool IsToolManaged(ToolData toolData)
    {
        return IsToolManaged(GetToolKey(toolData));
    }

    public bool IsToolUnlocked(string toolId)
    {
        return !string.IsNullOrEmpty(toolId) && grantedToolIds.Contains(toolId);
    }

    public bool IsToolUnlocked(ToolData toolData)
    {
        return IsToolUnlocked(GetToolKey(toolData));
    }

    [ContextMenu("Salvar Progresso Agora")]
    public void SaveNow()
    {
        if (!saveProgress || !initialized) return;
        if (!Application.isPlaying) return;

        // Captura etapa/conclusão do puzzle aberto antes de gravar.
        CaptureActivePuzzleState();

        // Preserva campos que não são gerenciados aqui (quando existirem).
        ObjectiveProgressData data = ObjectiveProgressStore.Load(saveFileName);
        if (data == null) data = new ObjectiveProgressData();
        data.version = 1;
        data.pinnedMainObjectiveId = pinnedMainObjectiveId ?? string.Empty;
        data.objectives = new List<ObjectiveProgressEntry>(orderedRuntime.Count);

        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            data.objectives.Add(new ObjectiveProgressEntry
            {
                id = runtime.data.id,
                progress = runtime.progress,
                state = runtime.state,
                activationProcessed = runtime.activationProcessed,
                completionProcessed = runtime.completionProcessed
            });
        }

        data.puzzlePins = new List<PuzzleObjectivePin>();
        List<string> pinnedPuzzleIds = new List<string>(pinnedPuzzleObjectiveIds.Keys);
        pinnedPuzzleIds.Sort(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pinnedPuzzleIds.Count; i++)
        {
            string puzzleId = pinnedPuzzleIds[i];
            if (string.IsNullOrWhiteSpace(puzzleId)) continue;
            if (!pinnedPuzzleObjectiveIds.TryGetValue(puzzleId, out string objectiveId)) continue;
            if (string.IsNullOrWhiteSpace(objectiveId)) continue;

            data.puzzlePins.Add(new PuzzleObjectivePin
            {
                puzzleId = puzzleId,
                objectiveId = objectiveId
            });
        }

        data.puzzles = new List<PuzzleProgressEntry>();
        List<PuzzleObjectiveContext> contexts = new List<PuzzleObjectiveContext>(puzzleContexts.Values);
        contexts.Sort((a, b) => string.Compare(
            a != null ? a.puzzleId : string.Empty,
            b != null ? b.puzzleId : string.Empty,
            StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < contexts.Count; i++)
        {
            PuzzleObjectiveContext context = contexts[i];
            if (context == null || !context.HasPersistentState) continue;

            data.puzzles.Add(new PuzzleProgressEntry
            {
                puzzleId = context.puzzleId,
                stageIndex = context.stageIndex,
                completed = context.completed,
                lastPlayedUtc = context.lastPlayedUtc
            });
        }

        ObjectiveProgressStore.Save(data, saveFileName);
        dirty = false;
    }

    [ContextMenu("Emitir Signal de Teste")]
    private void DebugEmitSignal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Objectives] O signal de teste só funciona em Play Mode.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(debugSignalName))
        {
            Debug.LogWarning("[Objectives] Preencha 'Debug Signal Name' para emitir um signal de teste.", this);
            return;
        }

        ObjectiveSignals.TriggerSignal(debugSignalName, Mathf.Max(1, debugSignalAmount));
    }

    private void LogState()
    {
        for (int i = 0; i < orderedRuntime.Count; i++)
        {
            ObjectiveRuntime runtime = orderedRuntime[i];
            Debug.Log($"[Objectives] {runtime.data.id} | {runtime.state} | {runtime.BuildProgressText()}", runtime.data);
        }
    }

    private void MarkDirty()
    {
        if (!saveProgress) return;
        dirty = true;
        lastDirtyTime = Time.unscaledTime;
    }

    private static int CompareRuntime(ObjectiveRuntime a, ObjectiveRuntime b)
    {
        int orderA = a != null && a.data != null ? a.data.order : 0;
        int orderB = b != null && b.data != null ? b.data.order : 0;
        if (orderA != orderB) return orderA.CompareTo(orderB);

        int indexA = a != null ? a.catalogIndex : 0;
        int indexB = b != null ? b.catalogIndex : 0;
        return indexA.CompareTo(indexB);
    }

    private static ObjectiveCatalog FindCatalog()
    {
        ObjectiveCatalog[] catalogs = Resources.FindObjectsOfTypeAll<ObjectiveCatalog>();
        if (catalogs == null || catalogs.Length == 0)
        {
            Debug.LogError("[Objectives] Nenhum ObjectiveCatalog encontrado no projeto.");
            return null;
        }

        if (catalogs.Length > 1)
        {
            Debug.LogWarning($"[Objectives] {catalogs.Length} ObjectiveCatalogs encontrados. Usando '{catalogs[0].name}'.", catalogs[0]);
        }

        return catalogs[0];
    }
}
