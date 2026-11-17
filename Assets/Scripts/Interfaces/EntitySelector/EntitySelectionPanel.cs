using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel que lista as entidades sobrepostas no ponto clicado.
// Cada botão recebe o nome da entidade e dispara o TriggerInterface real dela.
public class EntitySelectionPanel : MonoBehaviour
{
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Transform content;
    [SerializeField] private Canvas canvas;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private static EntitySelectionPanel instance;

    public static EntitySelectionPanel Instance {
        get {
            if (instance == null) instance = FindObjectOfType<EntitySelectionPanel>(true);
            return instance;
        }
    }

    private void Awake() {
        if (instance == null) instance = this;
        EnsureReferences();
    }

    private void OnDestroy() {
        if (instance == this) instance = null;
    }

    // Usado pelo TriggerInterface para não reabrir o painel quando o clique
    // é no botão (o collider do mapa continua embaixo da UI).
    public static bool IsPointerOverPanel(Vector2 screenPosition) {
        if (instance == null || !instance.isActiveAndEnabled || instance.panelRect == null) return false;

        Canvas panelCanvas = instance.canvas;
        Camera uiCamera = (panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? panelCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(instance.panelRect, screenPosition, uiCamera);
    }

    public static void HideCurrent() {
        if (instance != null) instance.Hide();
    }

    public void Show(IReadOnlyList<EntitySelectionOption> options, Vector2 screenPosition) {
        if (options == null || options.Count == 0) return;

        EnsureReferences();
        ClearButtons();
        gameObject.SetActive(true);

        for (int i = 0; i < options.Count; i++) {
            EntitySelectionOption option = options[i];
            Button button = CreateButton(option.Label);
            if (button == null) continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => {
                Hide();
                option.OnSelected?.Invoke();
            });
            spawnedButtons.Add(button.gameObject);
        }

        // Precisa recalcular o tamanho antes de posicionar/clamp na tela.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        PositionPanel(screenPosition);
    }

    public void Hide() {
        gameObject.SetActive(false);
        ClearButtons();
    }

    private void EnsureReferences() {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        if (content == null) content = transform;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (buttonPrefab == null) buttonPrefab = Resources.Load<Button>("EntitySelectionButton");
    }

    private Button CreateButton(string label) {
        Button button = null;

        if (buttonPrefab != null) {
            button = Instantiate(buttonPrefab, content);
        }

        if (button == null) {
            Button fallbackPrefab = Resources.Load<Button>("EntitySelectionButton");
            if (fallbackPrefab != null) button = Instantiate(fallbackPrefab, content);
        }

        if (button == null) button = CreateRuntimeButton();
        if (button == null) return null;

        button.gameObject.SetActive(true);

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null) {
            tmpText.text = label;
        } else {
            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null) legacyText.text = label;
        }

        return button;
    }

    private Button CreateRuntimeButton() {
        GameObject buttonObject = new GameObject(
            "EntitySelectionButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        buttonObject.transform.SetParent(content, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        image.raycastTarget = true;

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 40f;
        layoutElement.preferredHeight = 40f;
        layoutElement.flexibleWidth = 1f;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
        text.text = string.Empty;
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private void ClearButtons() {
        for (int i = 0; i < spawnedButtons.Count; i++) {
            if (spawnedButtons[i] != null) Destroy(spawnedButtons[i]);
        }
        spawnedButtons.Clear();
    }

    private void PositionPanel(Vector2 screenPosition) {
        if (panelRect == null || canvas == null) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 viewportPosition = uiCamera != null
            ? (Vector2)uiCamera.ScreenToViewportPoint(screenPosition)
            : new Vector2(screenPosition.x / Screen.width, screenPosition.y / Screen.height);

        Vector2 centeredPosition = viewportPosition - new Vector2(0.5f, 0.5f);
        float pivotX = centeredPosition.x > 0f ? 1f : 0f;
        float pivotY = centeredPosition.y > 0f ? 1f : 0f;
        panelRect.pivot = new Vector2(pivotX, pivotY);

        // Usa posição em world space para funcionar mesmo se o painel for
        // filho de outro objeto (ex.: DynamicInterfaces) e não do Canvas direto.
        Vector3 worldPoint;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPosition, uiCamera, out worldPoint)) {
            panelRect.position = worldPoint;
        }

        ForceInsideCanvas(canvasRect);
    }

    private void ForceInsideCanvas(RectTransform canvasRect) {
        Vector3[] canvasCorners = new Vector3[4];
        Vector3[] panelCorners = new Vector3[4];

        canvasRect.GetWorldCorners(canvasCorners);
        panelRect.GetWorldCorners(panelCorners);

        float shiftX = 0f;
        float shiftY = 0f;

        if (panelCorners[0].x < canvasCorners[0].x) shiftX = canvasCorners[0].x - panelCorners[0].x;
        if (panelCorners[2].x > canvasCorners[2].x) shiftX = canvasCorners[2].x - panelCorners[2].x;
        if (panelCorners[0].y < canvasCorners[0].y) shiftY = canvasCorners[0].y - panelCorners[0].y;
        if (panelCorners[1].y > canvasCorners[1].y) shiftY = canvasCorners[1].y - panelCorners[1].y;

        panelRect.position += new Vector3(shiftX, shiftY, 0f);
    }

}
