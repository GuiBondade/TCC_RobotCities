using System;
using System.Collections.Generic;
using UnityEngine;

// Liga os desbloqueios de objetivos aos PaletteItems da cena.
// Só altera os BlockDatas citados em algum ObjectiveData; itens não gerenciados
// preservam o estado configurado manualmente no Unity.
public class ObjectivePaletteGate : MonoBehaviour
{
    [Header("Comportamento")]
    [SerializeField] private bool hideManagedOnStart = true;
    [SerializeField] private bool highlightOnUnlock = true;
    [Tooltip("<= 0 usa a duração padrão do UIHighlightController.")]
    [SerializeField, Min(0f)] private float highlightDuration = 0f;

    [Header("Referências")]
    [SerializeField] private UIHighlightController highlightController;

    private readonly Dictionary<string, List<PaletteItem>> itemsByKey =
        new Dictionary<string, List<PaletteItem>>(StringComparer.Ordinal);

    private readonly HashSet<string> highlightedKeys = new HashSet<string>(StringComparer.Ordinal);
    private bool warnedMissingController;
    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        RebuildIndex();
        RefreshAll();
    }

    private void Subscribe()
    {
        if (subscribed) return;

        ObjectiveSignals.OnUnlockGranted += HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded += HandleObjectivesReloaded;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        ObjectiveSignals.OnUnlockGranted -= HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
        subscribed = false;
    }

    private void HandleUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source)
    {
        if (type != ObjectiveUnlockType.PaletteItem) return;
        if (string.IsNullOrWhiteSpace(key)) return;
        if (highlightedKeys.Contains(key)) return;

        RebuildIndex();
        bool applied = Apply(key, true, true);
        if (applied) highlightedKeys.Add(key);
    }

    private void HandleObjectivesReloaded()
    {
        RebuildIndex();
        RefreshAll();
    }

    private void RefreshAll()
    {
        ObjectiveManager manager = ObjectiveManager.instancia;
        if (manager == null || !manager.IsInitialized) return;

        RebuildIndex();

        foreach (KeyValuePair<string, List<PaletteItem>> pair in itemsByKey)
        {
            string key = pair.Key;
            if (!manager.IsPaletteItemManaged(key)) continue;

            bool unlocked = manager.IsPaletteItemUnlocked(key);
            if (!unlocked && !hideManagedOnStart) continue;

            Apply(key, unlocked, false);
        }
    }

    private void RebuildIndex()
    {
        itemsByKey.Clear();

        PaletteItem[] items = FindObjectsOfType<PaletteItem>(true);
        for (int i = 0; i < items.Length; i++)
        {
            PaletteItem item = items[i];
            if (item == null || item.blockData == null) continue;

            string key = GetKey(item.blockData);
            if (string.IsNullOrEmpty(key)) continue;

            if (!itemsByKey.TryGetValue(key, out List<PaletteItem> list))
            {
                list = new List<PaletteItem>();
                itemsByKey.Add(key, list);
            }

            list.Add(item);
        }
    }

    // Retorna true se encontrou pelo menos um PaletteItem para a chave.
    private bool Apply(string key, bool active, bool highlight)
    {
        if (!itemsByKey.TryGetValue(key, out List<PaletteItem> list)) return false;

        bool applied = false;

        for (int i = 0; i < list.Count; i++)
        {
            PaletteItem item = list[i];
            if (item == null) continue;

            applied = true;

            if (item.gameObject.activeSelf != active)
                item.gameObject.SetActive(active);

            if (active && highlight && highlightOnUnlock)
                Highlight(item, key);
        }

        return applied;
    }

    private void Highlight(PaletteItem item, string key)
    {
        UIHighlightController controller = ResolveController();
        if (controller == null)
        {
            if (!warnedMissingController)
            {
                warnedMissingController = true;
                Debug.LogWarning("[Objectives] ObjectivePaletteGate sem UIHighlightController; o item será ativado sem destaque.", this);
            }
            return;
        }

        controller.Show(item.transform, key, highlightDuration);
    }

    // Usado por sistemas futuros que queiram remover o destaque de um item.
    public void HideHighlight(BlockData blockData)
    {
        string key = GetKey(blockData);
        if (string.IsNullOrEmpty(key)) return;
        if (!itemsByKey.TryGetValue(key, out List<PaletteItem> list)) return;

        UIHighlightController controller = ResolveController();
        if (controller == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) controller.Hide(list[i].transform);
        }
    }

    private UIHighlightController ResolveController()
    {
        if (highlightController != null) return highlightController;
        return UIHighlightController.instancia;
    }

    private static string GetKey(BlockData blockData)
    {
        if (blockData == null) return null;

        string key = !string.IsNullOrWhiteSpace(blockData.id) ? blockData.id : blockData.name;
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
