using System;
using System.Collections.Generic;
using UnityEngine;

// Avisos centralizados de desbloqueio. Escuta o ObjectiveSignals e enfileira
// mensagens temporizadas no QueuedMessageUI. Não depende do ObjectiveUI nem
// altera o estado dos objetivos.
public class ObjectiveUnlockNoticeUI : MonoBehaviour
{
    [Header("Apresentação")]
    [SerializeField] private QueuedMessageUI presenter;
    [Tooltip("<= 0 usa a duração padrão do QueuedMessageUI.")]
    [SerializeField] private float duration = -1f;

    [Header("Textos de fallback")]
    [SerializeField] private string objectiveFallbackFormat = "Novo objetivo: {0}";
    [SerializeField] private string puzzleFallbackFormat = "Novo desafio: {0}";

    [Header("Filtros")]
    [SerializeField] private bool showPuzzleUnlocks = true;
    [SerializeField] private bool avoidDuplicates = true;

    private readonly HashSet<string> shownKeys = new HashSet<string>(StringComparer.Ordinal);
    private PuzzleCatalog cachedCatalog;
    private bool subscribed;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
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

        if (avoidDuplicates && !shownKeys.Add("objective:" + data.id)) return;

        bool enqueued = false;

        if (data.unlockNotices != null)
        {
            for (int i = 0; i < data.unlockNotices.Count; i++)
            {
                string notice = data.unlockNotices[i];
                if (string.IsNullOrWhiteSpace(notice)) continue;

                Enqueue(notice);
                enqueued = true;
            }
        }

        if (!enqueued)
        {
            string name = !string.IsNullOrWhiteSpace(data.title) ? data.title : data.id;
            Enqueue(Format(objectiveFallbackFormat, name));
        }
    }

    private void HandleUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source)
    {
        if (type != ObjectiveUnlockType.Puzzle) return;
        if (!showPuzzleUnlocks || string.IsNullOrWhiteSpace(key)) return;
        if (avoidDuplicates && !shownKeys.Add("puzzle:" + key)) return;

        PuzzleInfo puzzle = FindPuzzle(key);
        string name = puzzle != null && !string.IsNullOrWhiteSpace(puzzle.displayName)
            ? puzzle.displayName
            : key;

        string notice = puzzle != null && !string.IsNullOrWhiteSpace(puzzle.unlockNotice)
            ? puzzle.unlockNotice
            : Format(puzzleFallbackFormat, name);

        Enqueue(notice);
    }

    private void Enqueue(string text)
    {
        if (presenter == null) EnsureReferences();
        if (presenter == null) return;

        presenter.Enqueue(text, duration);
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

    private void EnsureReferences()
    {
        if (presenter != null) return;

        presenter = GetComponent<QueuedMessageUI>();
        if (presenter == null) presenter = GetComponentInChildren<QueuedMessageUI>(true);
        if (presenter == null) presenter = GetComponentInParent<QueuedMessageUI>();
    }

    private static string Format(string format, string value)
    {
        if (string.IsNullOrEmpty(format)) return value;
        return string.Format(format, value);
    }
}
