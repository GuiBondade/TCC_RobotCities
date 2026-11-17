using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel expandido da lista de objetivos. Recebe os dados já filtrados pelo
// ObjectiveUI e só cuida da renderização, do ScrollView e da seleção de uma linha.
public class ObjectivePanel : MonoBehaviour
{
    [Header("Lista")]
    [SerializeField] private RectTransform content;
    [SerializeField] private ObjectiveEntryView entryTemplate;
    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private string emptyMessage = "Nenhum objetivo disponível.";
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private bool resetScrollOnOpen = true;

    [Header("Botões")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;

    public event Action<ObjectiveData> OnObjectiveSelected;

    private readonly List<ObjectiveEntryView> rows = new List<ObjectiveEntryView>();
    private bool buttonsWired;
    private bool warnedMissingConfig;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void Open()
    {
        EnsureReferences();
        WireButtons();

        gameObject.SetActive(true);

        if (scrollRect != null && resetScrollOnOpen)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh(IReadOnlyList<ObjectiveRuntime> objectives, string pinnedId)
    {
        EnsureReferences();
        WireButtons();

        int count = objectives != null ? objectives.Count : 0;

        if (content == null || entryTemplate == null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) rows[i].gameObject.SetActive(false);
            }

            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(true);
                emptyLabel.text = "Lista de objetivos não configurada.";
            }

            if (!warnedMissingConfig)
            {
                Debug.LogWarning(
                    "[Objectives] ObjectivePanel sem Content ou EntryTemplate configurado.", this);
                warnedMissingConfig = true;
            }
            return;
        }

        HideSceneTemplate();

        for (int i = 0; i < count; i++)
        {
            ObjectiveEntryView row = GetOrCreateRow(i);
            row.gameObject.SetActive(true);
            row.Bind(objectives[i], IsPinned(objectives[i], pinnedId), HandleRowSelected);
        }

        for (int i = count; i < rows.Count; i++)
        {
            if (rows[i] != null) rows[i].gameObject.SetActive(false);
        }

        if (emptyLabel != null)
        {
            emptyLabel.gameObject.SetActive(count == 0);
            if (count == 0) emptyLabel.text = emptyMessage;
        }
    }

    // Usado quando só o progresso de um objetivo mudou, sem reconstruir a lista.
    public void RefreshObjective(ObjectiveRuntime runtime, string pinnedId)
    {
        if (runtime == null || runtime.data == null) return;

        for (int i = 0; i < rows.Count; i++)
        {
            ObjectiveEntryView row = rows[i];
            if (row == null || !row.gameObject.activeSelf) continue;
            if (!string.Equals(row.ObjectiveId, runtime.data.id, StringComparison.Ordinal)) continue;

            row.Bind(runtime, IsPinned(runtime, pinnedId), HandleRowSelected);
            return;
        }
    }

    private void HandleRowSelected(ObjectiveData data)
    {
        OnObjectiveSelected?.Invoke(data);
    }

    private static bool IsPinned(ObjectiveRuntime runtime, string pinnedId)
    {
        return runtime != null &&
               runtime.data != null &&
               !string.IsNullOrEmpty(pinnedId) &&
               string.Equals(runtime.data.id, pinnedId, StringComparison.Ordinal);
    }

    private ObjectiveEntryView GetOrCreateRow(int index)
    {
        while (rows.Count <= index)
        {
            ObjectiveEntryView row = Instantiate(entryTemplate, content);
            row.gameObject.SetActive(false);
            rows.Add(row);
        }

        ObjectiveEntryView result = rows[index];
        result.transform.SetSiblingIndex(index);
        return result;
    }

    private void HideSceneTemplate()
    {
        if (entryTemplate == null) return;
        if (entryTemplate.transform.IsChildOf(content))
            entryTemplate.gameObject.SetActive(false);
    }

    private void WireButtons()
    {
        if (buttonsWired) return;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        buttonsWired = true;
    }

    private void UnwireButtons()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
        buttonsWired = false;
    }

    private void EnsureReferences()
    {
        if (content == null)
        {
            Transform foundContent = FindChild("Content");
            if (foundContent != null) content = foundContent as RectTransform;
        }

        if (entryTemplate == null)
        {
            Transform foundTemplate = FindChild("ObjectiveEntryTemplate");
            if (foundTemplate != null)
                entryTemplate = foundTemplate.GetComponent<ObjectiveEntryView>();

            if (entryTemplate == null)
            {
                ObjectiveEntryView[] candidates = GetComponentsInChildren<ObjectiveEntryView>(true);
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] != null && !candidates[i].gameObject.activeSelf)
                    {
                        entryTemplate = candidates[i];
                        break;
                    }
                }
            }
        }

        if (emptyLabel == null)
            emptyLabel = FindText("EmptyLabel", "EmptyMessage");

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (closeButton == null)
            closeButton = FindButton("CloseButton", "CloseBtn");

        if (backdropButton == null)
            backdropButton = FindButton("Backdrop", "BackdropButton");
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
