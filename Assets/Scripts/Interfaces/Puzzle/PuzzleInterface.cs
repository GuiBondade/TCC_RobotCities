using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interface própria de construções especiais (ex.: Construção de Desafios).
// O GameObject já existe na cena (DynamicInterfaces/PuzzleInterface). Ao abrir,
// monta um card por item do PuzzleCatalog e usa o botão do card para carregar a
// cena aditiva do desafio.
public class PuzzleInterface : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RepositionInterface repositionInterface;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Button closeButton;

    [Header("Catálogo")]
    [Tooltip("Catálogo de desafios. Vazio = pega do PuzzleManager ou procura no projeto.")]
    [SerializeField] private PuzzleCatalog catalog;
    [Tooltip("Pai dos cards instanciados. Vazio = usa o pai do card template.")]
    [SerializeField] private Transform cardsContainer;
    [Tooltip("Card template (normalmente o PuzzlePrefab que já existe na UIScene).")]
    [SerializeField] private PuzzleEntryView cardTemplate;
    [Tooltip("Texto exibido quando o catálogo está vazio (opcional).")]
    [SerializeField] private TMP_Text emptyLabel;

    private BuildingController buildingController;
    private readonly List<PuzzleEntryView> spawnedCards = new List<PuzzleEntryView>();
    private bool objectiveSignalsSubscribed;

    public BuildingController CurrentBuilding => buildingController;
    public PuzzleCatalog Catalog => catalog;

    private void OnEnable() {
        SubscribeObjectiveSignals();
    }

    private void OnDisable() {
        UnsubscribeObjectiveSignals();
    }

    public void SetSignal() {
        PuzzleSignals.OnInterfacePopulation -= Open;
        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        if (repositionInterface != null) PuzzleSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;

        EnsureReferences();
        SubscribeObjectiveSignals();

        PuzzleSignals.OnInterfacePopulation += Open;
        PuzzleSignals.OnPuzzleStarted += HandlePuzzleStarted;
        if (repositionInterface != null) PuzzleSignals.OnInterfaceReposition += repositionInterface.SetupPosition;

        if (closeButton != null) {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy() {
        PuzzleSignals.OnInterfacePopulation -= Open;
        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        if (repositionInterface != null) PuzzleSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;
        UnsubscribeObjectiveSignals();
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void SubscribeObjectiveSignals() {
        if (objectiveSignalsSubscribed) return;

        ObjectiveSignals.OnUnlockGranted += HandleObjectiveUnlock;
        ObjectiveSignals.OnObjectivesReloaded += HandleObjectivesReloaded;
        objectiveSignalsSubscribed = true;
    }

    private void UnsubscribeObjectiveSignals() {
        if (!objectiveSignalsSubscribed) return;

        ObjectiveSignals.OnUnlockGranted -= HandleObjectiveUnlock;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
        objectiveSignalsSubscribed = false;
    }

    private void EnsureReferences() {
        if (canvasRect == null) {
            // includeInactive: o GameObject do puzzle fica desativado até abrir.
            Canvas canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
        }

        if (repositionInterface == null) repositionInterface = GetComponent<RepositionInterface>();
        if (repositionInterface == null) repositionInterface = gameObject.AddComponent<RepositionInterface>();

        RectTransform panelRect = transform as RectTransform;
        if (repositionInterface != null && panelRect != null && canvasRect != null) {
            repositionInterface.Configure(panelRect, canvasRect);
        } else if (canvasRect == null) {
            Debug.LogWarning("[PuzzleInterface] Canvas não encontrado: a interface não será reposicionada.", this);
        }

        if (catalog == null) {
            PuzzleManager manager = PuzzleManager.instancia;
            if (manager != null) catalog = manager.GetCatalog();
        }
        if (catalog == null) {
            var catalogs = Resources.FindObjectsOfTypeAll<PuzzleCatalog>();
            if (catalogs != null && catalogs.Length > 0) catalog = catalogs[0];
        }

        if (cardTemplate == null) cardTemplate = FindCardTemplate();

        if (cardsContainer == null && cardTemplate != null) {
            cardsContainer = cardTemplate.transform.parent;
        }
        if (cardsContainer == null) cardsContainer = transform;

        if (emptyLabel == null) {
            Transform found = FindChildByName(transform, "EmptyLabel");
            if (found != null) emptyLabel = found.GetComponent<TMP_Text>();
        }

        if (closeButton != null) {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Open(BuildingController building) {
        if (this == null || building == null) return;

        EnsureReferences();
        buildingController = building;
        gameObject.SetActive(true);
        RebuildCards();
        Debug.Log("[Puzzle] interface aberta para " + building.displayName);
    }

    private void RebuildCards() {
        for (int i = 0; i < spawnedCards.Count; i++) {
            if (spawnedCards[i] != null) Destroy(spawnedCards[i].gameObject);
        }
        spawnedCards.Clear();

        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);

        if (catalog == null) {
            SetEmptyMessage("Nenhum catálogo de desafios configurado.");
            return;
        }

        if (catalog.Puzzles == null || catalog.Puzzles.Count == 0) {
            SetEmptyMessage("Nenhum desafio disponível.");
            return;
        }

        if (cardTemplate == null || cardsContainer == null) {
            SetEmptyMessage("Card de desafio não configurado.");
            Debug.LogWarning("[PuzzleInterface] Card template não encontrado.", this);
            return;
        }

        SetEmptyMessage(string.Empty);

        foreach (var puzzle in catalog.Puzzles) {
            if (puzzle == null || !puzzle.IsValid) continue;

            PuzzleEntryView card = Instantiate(cardTemplate, cardsContainer);
            if (card == null) continue;

            card.gameObject.SetActive(true);
            card.Bind(puzzle, IsPuzzleUnlocked(puzzle), HandleOpenPuzzle);
            spawnedCards.Add(card);
        }
    }

    private void HandleOpenPuzzle(PuzzleInfo puzzle) {
        if (puzzle == null || !puzzle.IsValid) return;

        if (!IsPuzzleUnlocked(puzzle)) {
            Debug.LogWarning("[Puzzle] Desafio bloqueado por objetivo: " + puzzle.Id, this);
            return;
        }

        PuzzleManager manager = PuzzleManager.instancia;
        if (manager == null) manager = PuzzleManager.EnsureInstance(catalog);

        if (manager != null && manager.catalog == null) manager.catalog = catalog;

        if (manager != null && manager.OpenPuzzle(puzzle)) {
            Close();
        }
    }

    private void HandlePuzzleStarted(PuzzleInfo puzzle) {
        Close();
    }

    private void HandleObjectiveUnlock(ObjectiveUnlockType type, string key, ObjectiveData source) {
        if (type != ObjectiveUnlockType.Puzzle) return;
        if (!gameObject.activeInHierarchy) return;

        RefreshCardLocks();
    }

    private void HandleObjectivesReloaded() {
        if (!gameObject.activeInHierarchy) return;

        RefreshCardLocks();
    }

    private void RefreshCardLocks() {
        for (int i = 0; i < spawnedCards.Count; i++) {
            PuzzleEntryView card = spawnedCards[i];
            if (card == null) continue;

            card.SetUnlocked(IsPuzzleUnlocked(card.Info));
        }
    }

    private bool IsPuzzleUnlocked(PuzzleInfo puzzle) {
        if (puzzle == null) return false;

        ObjectiveManager manager = ObjectiveManager.instancia;
        if (manager == null || !manager.IsInitialized) return true;

        string puzzleId = puzzle.Id;
        if (string.IsNullOrWhiteSpace(puzzleId)) return true;
        if (!manager.IsPuzzleManaged(puzzleId)) return true;

        return manager.IsPuzzleUnlocked(puzzleId);
    }

    public void Close() {
        buildingController = null;
        gameObject.SetActive(false);
    }

    private void SetEmptyMessage(string message) {
        if (emptyLabel == null) return;
        emptyLabel.text = message;
        emptyLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private PuzzleEntryView FindCardTemplate() {
        foreach (var candidate in GetComponentsInChildren<Transform>(true)) {
            if (candidate == null) continue;
            if (candidate.gameObject.name != "PuzzlePrefab" && candidate.gameObject.name != "PuzzleCard") continue;

            var view = candidate.GetComponent<PuzzleEntryView>();
            if (view == null && candidate.gameObject.scene.IsValid()) {
                view = candidate.gameObject.AddComponent<PuzzleEntryView>();
            }
            if (view != null) return view;
        }
        return null;
    }

    private Transform FindChildByName(Transform parent, string childName) {
        foreach (var child in parent.GetComponentsInChildren<Transform>(true)) {
            if (child != null && child.gameObject.name == childName) return child;
        }
        return null;
    }
}
