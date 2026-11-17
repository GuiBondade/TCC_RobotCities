using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD do desafio. Fica na UIScene e é uma view pura: não guarda referência
// para PuzzleStateManager nem para PuzzleManager. Escuta o ciclo do desafio em
// PuzzleSignals, emite os pedidos (próxima/anterior/sair) pelos mesmos signals
// e liga/desliga o Content quando um desafio entra/sai.
//
// O GameObject que tem este script fica sempre ativo (senão OnEnable nunca roda
// e a HUD não recebe o evento que a faria aparecer); quem liga/desliga é o
// contentRoot.
public class PuzzleHUD : MonoBehaviour
{
    [Header("Visibilidade")]
    [Tooltip("Objeto ligado ao entrar no desafio e desligado ao sair. O root com este script fica sempre ativo.")]
    [SerializeField] private GameObject contentRoot;

    [Header("Textos")]
    [SerializeField] private TMP_Text stateLabel;

    [Header("Botões")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button exitButton;

    [Header("Rótulos opcionais")]
    [SerializeField] private string nextButtonLabel = "Próxima";
    [SerializeField] private string previousButtonLabel = "Anterior";
    [SerializeField] private string exitButtonLabel = "Sair";

    private PuzzleStateManager state;
    private bool subscribed;

    private void Awake() {
        EnsureReferences();
        WireButtons();
        SetVisible(false);

        if (contentRoot == null) {
            Debug.LogWarning("[PuzzleHUD] contentRoot não configurado: a HUD não será escondida ao sair do desafio.", this);
        } else if (contentRoot == gameObject) {
            Debug.LogWarning("[PuzzleHUD] contentRoot não pode ser o próprio GameObject do HUD: use um filho (ex.: Content).", this);
        }
    }

    private void OnEnable() {
        Subscribe();
    }

    private void OnDisable() {
        Unsubscribe();
    }

    private void OnDestroy() {
        Unsubscribe();

        if (nextButton != null) nextButton.onClick.RemoveListener(NextStage);
        if (previousButton != null) previousButton.onClick.RemoveListener(PreviousStage);
        if (exitButton != null) exitButton.onClick.RemoveListener(RequestExit);
    }

    private void EnsureReferences() {
        if (stateLabel == null) stateLabel = FindText("StateLabel", "StatusLabel", "PuzzleStateLabel", "StageLabel");
        if (stateLabel == null) stateLabel = CreateStateLabel();

        if (nextButton == null) nextButton = FindButton("NextPuzzleStateBtn", "NextButton", "ProximoButton");
        if (previousButton == null) previousButton = FindButton("PreviousPuzzleStateBtn", "PreviousButton", "AnteriorButton");
        if (exitButton == null) exitButton = FindButton("ExitBtn", "ExitButton", "SairButton");

        SetButtonLabel(nextButton, nextButtonLabel);
        SetButtonLabel(previousButton, previousButtonLabel);
        SetButtonLabel(exitButton, exitButtonLabel);
    }

    private void WireButtons() {
        if (nextButton != null) {
            nextButton.onClick.RemoveListener(NextStage);
            nextButton.onClick.AddListener(NextStage);
        }
        if (previousButton != null) {
            previousButton.onClick.RemoveListener(PreviousStage);
            previousButton.onClick.AddListener(PreviousStage);
        }
        if (exitButton != null) {
            exitButton.onClick.RemoveListener(RequestExit);
            exitButton.onClick.AddListener(RequestExit);
        }
    }

    private void Subscribe() {
        if (subscribed) return;
        subscribed = true;

        PuzzleSignals.OnPuzzleStarted += HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleStageChanged += HandleStageChanged;
        PuzzleSignals.OnPuzzleCompleted += HandlePuzzleCompleted;
        PuzzleSignals.OnPuzzleExited += HandlePuzzleExited;
    }

    private void Unsubscribe() {
        if (!subscribed) return;
        subscribed = false;

        PuzzleSignals.OnPuzzleStarted -= HandlePuzzleStarted;
        PuzzleSignals.OnPuzzleStageChanged -= HandleStageChanged;
        PuzzleSignals.OnPuzzleCompleted -= HandlePuzzleCompleted;
        PuzzleSignals.OnPuzzleExited -= HandlePuzzleExited;
    }

    private void HandlePuzzleStarted(PuzzleInfo puzzle) {
        EnsureReferences();
        SetVisible(true);
        Refresh();
    }

    private void HandleStageChanged(PuzzleStateManager stateManager, int stageIndex, int stageCount) {
        if (stateManager != null) state = stateManager;
        Refresh();
    }

    private void HandlePuzzleCompleted(PuzzleInfo puzzle) {
        Refresh();
    }

    private void HandlePuzzleExited(PuzzleInfo puzzle) {
        state = null;
        if (stateLabel != null) stateLabel.text = string.Empty;
        SetVisible(false);
    }

    private void SetVisible(bool visible) {
        if (contentRoot == null || contentRoot == gameObject) return;
        contentRoot.SetActive(visible);
    }

    private void Refresh() {
        if (state == null && PuzzleManager.instancia != null) state = PuzzleManager.instancia.ActiveState;
        if (state == null || stateLabel == null) return;

        int count = state.StageCount;
        int index = state.CurrentStageIndex;

        if (count <= 0) {
            stateLabel.text = state.PuzzleTitle;
        } else {
            string text = "Etapa " + (index + 1) + "/" + count;
            string stageTitle = state.CurrentStageTitle;
            if (!string.IsNullOrEmpty(stageTitle)) text += " - " + stageTitle;

            string message = state.CurrentStageMessage;
            if (!string.IsNullOrEmpty(message)) text += "\n" + message;

            if (state.IsCompleted) text = "Desafio concluído!\n" + text;
            stateLabel.text = text;
        }

        if (nextButton != null) {
            nextButton.interactable = count > 0 && !state.IsCompleted;
            SetButtonLabel(nextButton, count > 0 && index >= count - 1 ? "Concluir" : nextButtonLabel);
        }
        if (previousButton != null) {
            previousButton.interactable = count > 0 && index > 0 && !state.IsCompleted;
        }
    }

    private void NextStage() {
        PuzzleSignals.RequestNextStage();
    }

    private void PreviousStage() {
        PuzzleSignals.RequestPreviousStage();
    }

    private void RequestExit() {
        PuzzleSignals.RequestExit();
    }

    private TMP_Text CreateStateLabel() {
        var go = new GameObject("PuzzleStatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(contentRoot != null ? contentRoot.transform : transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(800f, 80f);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        label.raycastTarget = false;

        TMP_Text fontSource = GetComponentInChildren<TMP_Text>(true);
        if (fontSource != null) label.font = fontSource.font;

        return label;
    }

    private TMP_Text FindText(params string[] names) {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) {
            if (text == null) continue;
            foreach (string candidate in names) {
                if (text.gameObject.name == candidate) return text;
            }
        }
        return null;
    }

    private Button FindButton(params string[] names) {
        foreach (var button in GetComponentsInChildren<Button>(true)) {
            if (button == null) continue;
            foreach (string candidate in names) {
                if (button.gameObject.name == candidate) return button;
            }
        }
        return null;
    }

    private static void SetButtonLabel(Button button, string text) {
        if (button == null || string.IsNullOrEmpty(text)) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }
}
