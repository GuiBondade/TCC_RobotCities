using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD compacta + painel expandido dos objetivos.
// A UI não altera progresso: só lê o ObjectiveManager, pede para fixar um
// objetivo e reage aos eventos do ObjectiveSignals/PuzzleSignals.
public class ObjectiveUI : MonoBehaviour
{
    [Header("Escopo")]
    [SerializeField] private ObjectiveScope scope = ObjectiveScope.Main;
    [Tooltip("Usado quando followActivePuzzle = false e scope = Puzzle.")]
    [SerializeField] private string puzzleId;
    [Tooltip("Ao abrir um puzzle, mostra automaticamente os objetivos e o pin daquele puzzle.")]
    [SerializeField] private bool followActivePuzzle = true;

    [Header("HUD compacta")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button expandButton;
    [SerializeField] private TMP_Text objectiveLabel;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private string emptyText = "Nenhum objetivo em andamento";

    [Header("Painel")]
    [SerializeField] private ObjectivePanel panel;

    private ObjectiveManager manager;
    private ObjectiveScope currentScope = ObjectiveScope.Main;
    private string currentPuzzleId;
    private bool compactButtonsWired;
    private bool subscribed;

    private void Awake()
    {
        EnsureReferences();
        WireCompactButtons();

        if (panel != null)
            panel.OnObjectiveSelected += HandlePanelSelected;
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshScope();
        EnsureCurrentPin();
        RefreshAll();
    }

    private void Start()
    {
        manager = ObjectiveManager.instancia;
        if (manager == null) manager = ObjectiveManager.EnsureInstance();

        RefreshScope();
        EnsureCurrentPin();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnwireCompactButtons();

        if (panel != null)
            panel.OnObjectiveSelected -= HandlePanelSelected;
    }

    private void Subscribe()
    {
        if (subscribed) return;

        ObjectiveSignals.OnObjectiveProgressChanged += HandleProgressChanged;
        ObjectiveSignals.OnObjectiveStateChanged += HandleStateChanged;
        ObjectiveSignals.OnObjectiveUnlocked += HandleObjectiveUnlocked;
        ObjectiveSignals.OnObjectiveCompleted += HandleObjectiveCompleted;
        ObjectiveSignals.OnObjectivesReloaded += HandleObjectivesReloaded;
        ObjectiveSignals.OnPinnedObjectiveChanged += HandlePinnedObjectiveChanged;
        PuzzleSignals.OnPuzzleStarted += HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleExited += HandlePuzzleExited;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        ObjectiveSignals.OnObjectiveProgressChanged -= HandleProgressChanged;
        ObjectiveSignals.OnObjectiveStateChanged -= HandleStateChanged;
        ObjectiveSignals.OnObjectiveUnlocked -= HandleObjectiveUnlocked;
        ObjectiveSignals.OnObjectiveCompleted -= HandleObjectiveCompleted;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
        ObjectiveSignals.OnPinnedObjectiveChanged -= HandlePinnedObjectiveChanged;
        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleExited -= HandlePuzzleExited;

        subscribed = false;
    }

    private void HandleObjectivesReloaded()
    {
        manager = ObjectiveManager.instancia;
        RefreshScope();
        EnsureCurrentPin();
        RefreshAll();
    }

    private void HandleProgressChanged(ObjectiveData data, int current, int target)
    {
        if (!MatchesCurrentScope(data)) return;
        if (manager == null || !manager.IsInitialized) return;

        string pinnedId = manager.GetPinnedObjectiveId(currentScope, currentPuzzleId);
        ObjectiveRuntime runtime = manager.GetRuntime(data.id);

        if (runtime != null &&
            !string.IsNullOrEmpty(pinnedId) &&
            string.Equals(data.id, pinnedId, StringComparison.Ordinal))
        {
            SetCompact(runtime);
        }

        if (panel != null && panel.IsOpen && runtime != null)
            panel.RefreshObjective(runtime, pinnedId);
    }

    private void HandleStateChanged(ObjectiveData data, ObjectiveState state)
    {
        if (!MatchesCurrentScope(data)) return;
        RefreshAll();
    }

    private void HandleObjectiveUnlocked(ObjectiveData data)
    {
        if (!MatchesCurrentScope(data)) return;
        RefreshAll();
    }

    private void HandleObjectiveCompleted(ObjectiveData data)
    {
        if (!MatchesCurrentScope(data)) return;
        RefreshAll();
    }

    private void HandlePinnedObjectiveChanged(
        ObjectiveScope changedScope, string changedPuzzleId, string objectiveId)
    {
        if (changedScope != currentScope) return;

        if (currentScope == ObjectiveScope.Puzzle &&
            !string.Equals(changedPuzzleId, currentPuzzleId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshAll();
    }

    private void HandlePuzzleStarted(PuzzleInfo puzzle)
    {
        if (!followActivePuzzle) return;

        RefreshScope();
        EnsureCurrentPin();
        RefreshAll();
    }

    private void HandlePuzzleExited(PuzzleInfo puzzle)
    {
        if (!followActivePuzzle) return;

        RefreshScope();
        EnsureCurrentPin();
        RefreshAll();
    }

    private void RefreshScope()
    {
        if (followActivePuzzle &&
            PuzzleManager.instancia != null &&
            PuzzleManager.instancia.IsOpen &&
            PuzzleManager.instancia.ActivePuzzle != null)
        {
            string activePuzzleId = PuzzleManager.instancia.ActivePuzzle.Id;
            if (!string.IsNullOrWhiteSpace(activePuzzleId))
            {
                currentScope = ObjectiveScope.Puzzle;
                currentPuzzleId = activePuzzleId;
                return;
            }
        }

        currentScope = scope;
        currentPuzzleId = scope == ObjectiveScope.Puzzle ? puzzleId : string.Empty;
    }

    private void EnsureCurrentPin()
    {
        if (manager == null) manager = ObjectiveManager.instancia;
        if (manager == null || !manager.IsInitialized) return;

        manager.EnsurePinnedObjective(currentScope, currentPuzzleId);
    }

    private void RefreshAll()
    {
        RefreshCompact();

        if (panel != null && panel.IsOpen)
            RefreshPanel();
    }

    private void RefreshCompact()
    {
        if (manager == null || !manager.IsInitialized)
        {
            SetEmpty();
            return;
        }

        ObjectiveRuntime pinned = manager.GetPinnedObjectiveRuntime(currentScope, currentPuzzleId);
        if (pinned == null || pinned.state != ObjectiveState.InProgress)
        {
            SetEmpty();
            return;
        }

        SetCompact(pinned);
    }

    private void SetCompact(ObjectiveRuntime runtime)
    {
        if (runtime == null || runtime.data == null)
        {
            SetEmpty();
            return;
        }

        if (objectiveLabel != null)
            objectiveLabel.text = runtime.data.BuildDisplayText(runtime.progress);

        if (emptyState != null)
            emptyState.SetActive(false);
    }

    private void SetEmpty()
    {
        if (objectiveLabel != null)
            objectiveLabel.text = emptyText;

        if (emptyState != null)
            emptyState.SetActive(true);
    }

    private void RefreshPanel()
    {
        if (panel == null) return;

        if (manager == null || !manager.IsInitialized)
        {
            panel.Refresh(null, string.Empty);
            return;
        }

        panel.Refresh(
            manager.GetVisibleRuntimeObjectives(currentScope, currentPuzzleId),
            manager.GetPinnedObjectiveId(currentScope, currentPuzzleId));
    }

    private void OpenPanel()
    {
        if (panel == null) return;

        panel.Open();
        RefreshPanel();
    }

    private void HandlePanelSelected(ObjectiveData data)
    {
        if (data == null || !MatchesCurrentScope(data)) return;
        if (manager == null || !manager.IsInitialized) return;

        if (manager.TryPinObjective(data.id))
            RefreshAll();
    }

    private bool MatchesCurrentScope(ObjectiveData data)
    {
        if (data == null || data.scope != currentScope) return false;

        if (currentScope == ObjectiveScope.Puzzle)
        {
            if (string.IsNullOrEmpty(currentPuzzleId)) return false;
            if (!string.Equals(data.puzzleId, currentPuzzleId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void WireCompactButtons()
    {
        if (compactButtonsWired) return;

        if (openButton != null) openButton.onClick.AddListener(OpenPanel);
        if (expandButton != null) expandButton.onClick.AddListener(OpenPanel);

        compactButtonsWired = true;
    }

    private void UnwireCompactButtons()
    {
        if (openButton != null) openButton.onClick.RemoveListener(OpenPanel);
        if (expandButton != null) expandButton.onClick.RemoveListener(OpenPanel);

        compactButtonsWired = false;
    }

    private void EnsureReferences()
    {
        if (openButton == null)
            openButton = FindButton("OpenPanelButton", "OpenButton", "ObjectiveButton");

        if (expandButton == null)
            expandButton = FindButton("ExpandButton", "DetailsButton");

        if (objectiveLabel == null)
            objectiveLabel = FindText(
                "ObjectiveLabel", "ObjectiveText", "TitleLabel", "Text (TMP)", "Text");

        if (emptyState == null)
        {
            Transform foundEmpty = FindChild("EmptyState", "EmptyIndicator");
            if (foundEmpty != null) emptyState = foundEmpty.gameObject;
        }

        if (panel == null)
            panel = GetComponentInChildren<ObjectivePanel>(true);
    }

    private TMP_Text FindText(params string[] names)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && NameMatches(texts[i].name, names)) return texts[i];
        }
        return null;
    }

    private Button FindButton(params string[] names)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && NameMatches(buttons[i].name, names)) return buttons[i];
        }
        return null;
    }

    private Transform FindChild(params string[] names)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && NameMatches(children[i].name, names)) return children[i];
        }
        return null;
    }

    private static bool NameMatches(string value, string[] names)
    {
        if (string.IsNullOrEmpty(value) || names == null) return false;

        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(value, names[i], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
