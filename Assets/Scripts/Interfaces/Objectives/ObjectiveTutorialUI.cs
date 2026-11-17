using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tutorial persistente da HUD. Diferente dos avisos, não usa timer:
// o texto fica visível até outro tutorial substituí-lo. O highlight associado
// também permanece até ser substituído ou removido.
public class ObjectiveTutorialUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text tutorialLabel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private UIHighlightController highlightController;
    [SerializeField] private bool hideOnStart = true;

    [Header("Toggle de highlight")]
    [Tooltip("Botão opcional que liga/desliga o highlight sem fechar o tutorial. " +
             "Se estiver no mesmo GameObject do tutorial, o próprio texto vira o botão.")]
    [SerializeField] private Button toggleHighlightButton;
    [Tooltip("Texto opcional do botão de toggle. Se vazio, usa o texto do próprio botão.")]
    [SerializeField] private TMP_Text toggleHighlightLabel;
    [SerializeField] private string highlightOnText = "Ocultar destaque";
    [SerializeField] private string highlightOffText = "Destacar";

    private string currentHighlightKey;
    private PuzzleCatalog cachedCatalog;
    private bool warnedMissingController;
    private bool warnedMissingTarget;
    private bool warnedMissingLabel;
    private bool subscribed;
    private bool toggleWired;
    private bool highlightVisible;

    private void Awake()
    {
        EnsureReferences();
        WireToggleButton();
        UpdateToggleButton();
        SetVisible(!hideOnStart);
    }

    private void OnEnable()
    {
        Subscribe();
        WireToggleButton();
        UpdateToggleButton();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnwireToggleButton();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnwireToggleButton();
        HideCurrentHighlight();
    }

    private void Subscribe()
    {
        if (subscribed) return;

        ObjectiveSignals.OnObjectiveUnlocked += HandleObjectiveUnlocked;
        ObjectiveSignals.OnUnlockGranted += HandleUnlockGranted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        ObjectiveSignals.OnObjectiveUnlocked -= HandleObjectiveUnlocked;
        ObjectiveSignals.OnUnlockGranted -= HandleUnlockGranted;
        subscribed = false;
    }

    private void HandleObjectiveUnlocked(ObjectiveData data)
    {
        if (data == null) return;
        ShowTutorial(data.tutorialText, data.highlightKey);
    }

    private void HandleUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source)
    {
        if (type != ObjectiveUnlockType.Puzzle || string.IsNullOrWhiteSpace(key)) return;

        PuzzleInfo puzzle = FindPuzzle(key);
        if (puzzle == null) return;

        ShowTutorial(puzzle.tutorialText, puzzle.highlightKey);
    }

    public void ShowTutorial(string text, string highlightKey)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        EnsureReferences();

        if (tutorialLabel != null)
        {
            tutorialLabel.text = text;
        }
        else if (!warnedMissingLabel)
        {
            Debug.LogWarning("[Objectives] ObjectiveTutorialUI sem TMP_Text configurado.", this);
            warnedMissingLabel = true;
        }

        highlightVisible = !string.IsNullOrWhiteSpace(highlightKey);
        ApplyHighlight(highlightKey);
        SetVisible(true);
        UpdateToggleButton();
    }

    public void Clear()
    {
        if (tutorialLabel != null) tutorialLabel.text = string.Empty;
        SetVisible(false);
        HideCurrentHighlight();
        UpdateToggleButton();
    }

    // Liga/desliga apenas o highlight; o texto do tutorial continua visível.
    public void ToggleHighlight()
    {
        if (string.IsNullOrWhiteSpace(currentHighlightKey)) return;

        UIHighlightController controller = ResolveController();
        if (controller == null) return;

        if (highlightVisible)
        {
            controller.Hide(currentHighlightKey);
            highlightVisible = false;
        }
        else if (!controller.HasTarget(currentHighlightKey))
        {
            currentHighlightKey = string.Empty;
            highlightVisible = false;
            if (!warnedMissingTarget)
            {
                warnedMissingTarget = true;
                Debug.LogWarning("[Objectives] ObjectiveTutorialUI não encontrou UIHighlightTarget para a chave informada.", this);
            }
        }
        else
        {
            highlightVisible = controller.ShowPersistent(currentHighlightKey);
        }

        UpdateToggleButton();
    }

    private void ApplyHighlight(string highlightKey)
    {
        string nextKey = string.IsNullOrWhiteSpace(highlightKey)
            ? string.Empty
            : highlightKey.Trim();

        UIHighlightController controller = ResolveController();

        if (!string.Equals(currentHighlightKey, nextKey, StringComparison.OrdinalIgnoreCase))
        {
            if (controller != null && !string.IsNullOrEmpty(currentHighlightKey))
                controller.Hide(currentHighlightKey);

            currentHighlightKey = nextKey;
        }

        if (string.IsNullOrEmpty(currentHighlightKey))
        {
            highlightVisible = false;
            return;
        }

        if (controller == null)
        {
            highlightVisible = false;
            if (!warnedMissingController)
            {
                warnedMissingController = true;
                Debug.LogWarning("[Objectives] ObjectiveTutorialUI sem UIHighlightController; o tutorial será exibido sem highlight.", this);
            }
            return;
        }

        if (!controller.HasTarget(currentHighlightKey))
        {
            highlightVisible = false;
            currentHighlightKey = string.Empty;
            if (!warnedMissingTarget)
            {
                warnedMissingTarget = true;
                Debug.LogWarning("[Objectives] ObjectiveTutorialUI não encontrou UIHighlightTarget para a chave informada.", this);
            }
            return;
        }

        if (highlightVisible)
            highlightVisible = controller.ShowPersistent(currentHighlightKey);
    }

    private void HideCurrentHighlight()
    {
        if (!string.IsNullOrEmpty(currentHighlightKey))
        {
            UIHighlightController controller = ResolveController();
            if (controller != null) controller.Hide(currentHighlightKey);
        }

        currentHighlightKey = string.Empty;
        highlightVisible = false;
    }

    private UIHighlightController ResolveController()
    {
        if (highlightController != null) return highlightController;
        return UIHighlightController.instancia;
    }

    private void SetVisible(bool visible)
    {
        bool interactive = visible &&
                           toggleHighlightButton != null &&
                           !string.IsNullOrWhiteSpace(currentHighlightKey);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = interactive;
            canvasGroup.interactable = interactive;
        }
        else if (tutorialLabel != null)
        {
            tutorialLabel.gameObject.SetActive(visible);
        }
    }

    private void EnsureReferences()
    {
        if (tutorialLabel == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;

                string objectName = texts[i].gameObject.name;
                if (objectName == "TutorialLabel" || objectName == "TutorialText" ||
                    objectName == "ObjectiveTutorialLabel" || objectName == "HintLabel" ||
                    objectName == "DicaLabel")
                {
                    tutorialLabel = texts[i];
                    break;
                }
            }

            if (tutorialLabel == null && texts.Length > 0) tutorialLabel = texts[0];
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (toggleHighlightButton == null)
        {
            toggleHighlightButton = FindButton(
                "HighlightToggleButton", "ToggleHighlightButton",
                "ToggleHighlight", "TutorialHighlightToggle");
        }

        if (toggleHighlightLabel == null && toggleHighlightButton != null)
        {
            TMP_Text label = toggleHighlightButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label != tutorialLabel) toggleHighlightLabel = label;
        }
    }

    private Button FindButton(params string[] names)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            for (int j = 0; j < names.Length; j++)
            {
                if (button.gameObject.name == names[j]) return button;
            }
        }

        return null;
    }

    private void WireToggleButton()
    {
        if (toggleWired || toggleHighlightButton == null) return;

        toggleHighlightButton.onClick.RemoveListener(ToggleHighlight);
        toggleHighlightButton.onClick.AddListener(ToggleHighlight);
        toggleWired = true;
    }

    private void UnwireToggleButton()
    {
        if (!toggleWired) return;

        if (toggleHighlightButton != null)
            toggleHighlightButton.onClick.RemoveListener(ToggleHighlight);

        toggleWired = false;
    }

    private void UpdateToggleButton()
    {
        bool hasHighlight = !string.IsNullOrWhiteSpace(currentHighlightKey);

        if (toggleHighlightButton != null)
        {
            if (toggleHighlightButton.gameObject != gameObject)
                toggleHighlightButton.gameObject.SetActive(hasHighlight);

            toggleHighlightButton.interactable = hasHighlight;
        }

        if (toggleHighlightLabel != null)
            toggleHighlightLabel.text = highlightVisible ? highlightOnText : highlightOffText;
    }

    private PuzzleInfo FindPuzzle(string puzzleId)
    {
        PuzzleCatalog catalog = GetCatalog();
        return catalog != null ? catalog.FindById(puzzleId) : null;
    }

    private PuzzleCatalog GetCatalog()
    {
        if (cachedCatalog != null) return cachedCatalog;

        if (PuzzleManager.instancia != null) cachedCatalog = PuzzleManager.instancia.GetCatalog();
        if (cachedCatalog != null) return cachedCatalog;

        PuzzleCatalog[] catalogs = Resources.FindObjectsOfTypeAll<PuzzleCatalog>();
        if (catalogs != null && catalogs.Length > 0) cachedCatalog = catalogs[0];
        return cachedCatalog;
    }
}
