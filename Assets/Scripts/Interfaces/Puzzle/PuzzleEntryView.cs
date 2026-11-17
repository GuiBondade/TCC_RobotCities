using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Um card do catálogo de desafios. O card pode vir de um prefab (ou do objeto
// PuzzlePrefab que já existe na UIScene); os campos são resolvidos pelo nome dos
// filhos, então não é obrigatório arrastar cada label no Inspector.
public class PuzzleEntryView : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text rewardLabel;

    [Header("Bloqueio")]
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private TMP_Text lockedLabel;
    [SerializeField] private string lockedText = "Bloqueado";
    [SerializeField] private bool hideDetailsWhenLocked = true;

    [Header("Botões")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button expandButton;

    private PuzzleInfo info;
    private bool isUnlocked = true;
    private Action<PuzzleInfo> openAction;
    private Action<PuzzleInfo> expandAction;

    public PuzzleInfo Info => info;
    public string PuzzleId => info != null ? info.Id : string.Empty;
    public bool IsUnlocked => isUnlocked;

    private void Awake()
    {
        EnsureReferences();
    }

    public void Bind(PuzzleInfo puzzle, Action<PuzzleInfo> onOpen, Action<PuzzleInfo> onExpand = null)
    {
        Bind(puzzle, true, onOpen, onExpand);
    }

    public void Bind(
        PuzzleInfo puzzle, bool unlocked, Action<PuzzleInfo> onOpen, Action<PuzzleInfo> onExpand = null)
    {
        EnsureReferences();

        info = puzzle;
        isUnlocked = unlocked;
        openAction = onOpen;
        expandAction = onExpand;

        WireButtons();
        Refresh();
    }

    public void SetUnlocked(bool unlocked)
    {
        if (isUnlocked == unlocked) return;

        isUnlocked = unlocked;
        Refresh();
    }

    private void Refresh()
    {
        if (info == null)
        {
            if (nameLabel != null) nameLabel.text = string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = string.Empty;
            if (rewardLabel != null) rewardLabel.text = string.Empty;
            if (lockedOverlay != null) lockedOverlay.SetActive(false);
            if (openButton != null) openButton.interactable = false;
            if (expandButton != null) expandButton.interactable = false;
            return;
        }

        bool locked = !isUnlocked;

        if (nameLabel != null)
        {
            if (locked)
            {
                nameLabel.text = lockedText;
            }
            else
            {
                nameLabel.text = !string.IsNullOrWhiteSpace(info.displayName)
                    ? info.displayName
                    : info.sceneName;
            }
        }

        bool hideDetails = locked && hideDetailsWhenLocked;

        if (descriptionLabel != null)
            descriptionLabel.text = hideDetails ? string.Empty : (info.description ?? string.Empty);

        if (rewardLabel != null)
            rewardLabel.text = hideDetails ? string.Empty : (info.reward ?? string.Empty);

        if (lockedOverlay != null)
            lockedOverlay.SetActive(locked);

        if (lockedLabel != null)
            lockedLabel.text = lockedText;

        if (openButton != null)
            openButton.interactable = !locked && info.IsValid;

        if (expandButton != null)
            expandButton.interactable = !locked && expandAction != null;
    }

    private void WireButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpen);
            openButton.onClick.AddListener(HandleOpen);
        }

        if (expandButton != null)
        {
            expandButton.onClick.RemoveListener(HandleExpand);
            expandButton.onClick.AddListener(HandleExpand);
        }
    }

    public void EnsureReferences()
    {
        if (nameLabel == null) nameLabel = FindText("InterfaceLabel", "NameLabel", "TitleLabel");
        if (descriptionLabel == null) descriptionLabel = FindText("DescriptionLabel", "DescLabel");
        if (rewardLabel == null) rewardLabel = FindText("RewardLabel", "RecompensaLabel");

        if (lockedOverlay == null)
        {
            Transform foundOverlay = FindChild("LockedOverlay", "LockedState", "BlockedOverlay");
            if (foundOverlay != null) lockedOverlay = foundOverlay.gameObject;
        }

        if (lockedLabel == null) lockedLabel = FindText("LockedLabel", "LockedText", "BlockedLabel");

        if (openButton == null) openButton = FindButton("OpenButton", "AbrirButton", "PlayButton");
        if (expandButton == null) expandButton = FindButton("ExpandButton", "ExpandirButton");

        if (openButton == null) openButton = GetComponentInChildren<Button>(true);
    }

    private TMP_Text FindText(params string[] names)
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null) continue;
            foreach (string candidate in names)
            {
                if (text.gameObject.name == candidate) return text;
            }
        }
        return null;
    }

    private Button FindButton(params string[] names)
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            foreach (string candidate in names)
            {
                if (button.gameObject.name == candidate) return button;
            }
        }
        return null;
    }

    private Transform FindChild(params string[] names)
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            foreach (string candidate in names)
            {
                if (child.gameObject.name == candidate) return child;
            }
        }
        return null;
    }

    private void HandleOpen()
    {
        if (!isUnlocked) return;
        if (info == null || !info.IsValid) return;
        openAction?.Invoke(info);
    }

    private void HandleExpand()
    {
        if (!isUnlocked) return;
        if (info == null) return;
        expandAction?.Invoke(info);
    }

    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(HandleOpen);
        if (expandButton != null) expandButton.onClick.RemoveListener(HandleExpand);
    }
}
