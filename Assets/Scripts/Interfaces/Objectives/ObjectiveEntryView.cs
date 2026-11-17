using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma linha da lista expandida de objetivos.
// O texto e as cores são atualizados pelo ObjectivePanel; este componente não
// conhece progresso, desbloqueios nem persistência.
public class ObjectiveEntryView : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text objectiveLabel;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject pinnedIndicator;

    [Header("Aparência por estado")]
    [SerializeField] private Color inProgressColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color completedColor = new Color(0.35f, 0.85f, 0.35f, 1f);
    [SerializeField] private bool strikethroughCompleted = true;

    private ObjectiveRuntime runtime;
    private Action<ObjectiveData> selectAction;
    private bool buttonListenerAdded;

    public string ObjectiveId => runtime != null && runtime.data != null
        ? runtime.data.id
        : string.Empty;

    private void Awake()
    {
        EnsureReferences();
        EnsureButtonListener();
    }

    private void OnDestroy()
    {
        if (selectButton != null && buttonListenerAdded)
            selectButton.onClick.RemoveListener(HandleSelect);
    }

    public void Bind(ObjectiveRuntime value, bool pinned, Action<ObjectiveData> onSelect)
    {
        runtime = value;
        selectAction = onSelect;

        EnsureReferences();
        EnsureButtonListener();
        Refresh(pinned);
    }

    private void Refresh(bool pinned)
    {
        if (runtime == null || runtime.data == null)
        {
            if (objectiveLabel != null)
            {
                objectiveLabel.text = string.Empty;
                objectiveLabel.color = lockedColor;
                objectiveLabel.fontStyle = FontStyles.Normal;
            }

            if (selectButton != null) selectButton.interactable = false;
            if (pinnedIndicator != null) pinnedIndicator.SetActive(false);
            return;
        }

        ObjectiveState state = runtime.state;

        if (objectiveLabel != null)
        {
            objectiveLabel.text = runtime.data.BuildDisplayText(runtime.progress);
            objectiveLabel.color = GetStateColor(state);
            objectiveLabel.fontStyle = state == ObjectiveState.Completed && strikethroughCompleted
                ? FontStyles.Strikethrough
                : FontStyles.Normal;
        }

        if (selectButton != null)
            selectButton.interactable = state == ObjectiveState.InProgress;

        if (pinnedIndicator != null)
            pinnedIndicator.SetActive(pinned && state == ObjectiveState.InProgress);
    }

    private Color GetStateColor(ObjectiveState state)
    {
        if (state == ObjectiveState.Completed) return completedColor;
        if (state == ObjectiveState.Locked) return lockedColor;
        return inProgressColor;
    }

    private void HandleSelect()
    {
        if (runtime == null || runtime.data == null) return;
        if (runtime.state != ObjectiveState.InProgress) return;
        selectAction?.Invoke(runtime.data);
    }

    private void EnsureButtonListener()
    {
        if (selectButton == null || buttonListenerAdded) return;

        selectButton.onClick.AddListener(HandleSelect);
        buttonListenerAdded = true;
    }

    private void EnsureReferences()
    {
        if (objectiveLabel == null)
            objectiveLabel = FindText("ObjectiveLabel", "ObjectiveText", "Label", "Text (TMP)", "Text");

        if (selectButton == null)
        {
            selectButton = FindButton("SelectButton", "PinButton", "ObjectiveButton");
            if (selectButton == null) selectButton = GetComponent<Button>();
        }

        if (pinnedIndicator == null)
        {
            Transform pinned = FindChild("PinnedIndicator", "Pinned");
            if (pinned != null) pinnedIndicator = pinned.gameObject;
        }
    }

    private TMP_Text FindText(params string[] names)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && NameMatches(texts[i].name, names)) return texts[i];
        }
        return texts.Length > 0 ? texts[0] : null;
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
