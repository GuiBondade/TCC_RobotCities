using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Controla os highlights de UI. Não conhece objetivos: recebe uma chave
// (ObjectiveData.highlightKey / PuzzleInfo.highlightKey) ou um Transform e
// liga o UIHighlightTarget correspondente. O visual é criado manualmente na
// cena; se o alvo não tiver um, o prefab opcional é instanciado como fallback.
[DefaultExecutionOrder(-50)]
public class UIHighlightController : MonoBehaviour
{
    public static UIHighlightController instancia { get; private set; }

    [Header("Fallback")]
    [Tooltip("Prefab opcional instanciado sob o alvo quando ele não tem UIHighlightTarget.")]
    [SerializeField] private UIHighlightTarget highlightPrefab;

    [Tooltip("Duração padrão de um highlight. <= 0 mantém até Hide/HideAll.")]
    [SerializeField, Min(0f)] private float defaultDuration = 0f;

    private readonly Dictionary<string, UIHighlightTarget> keyedTargets =
        new Dictionary<string, UIHighlightTarget>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Coroutine> hideRoutines =
        new Dictionary<string, Coroutine>(StringComparer.OrdinalIgnoreCase);

    private bool warnedMissingTarget;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Debug.LogWarning("[Highlight] Mais de um UIHighlightController ativo. O duplicado será destruído.", this);
            Destroy(gameObject);
            return;
        }

        instancia = this;
        RebuildRegistry();
        HideAll();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
        StopAllCoroutines();
    }

    // Domain reload e scene reload estão desativados no projeto.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
    }

    public void RebuildRegistry()
    {
        keyedTargets.Clear();

        UIHighlightTarget[] targets = FindObjectsOfType<UIHighlightTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            UIHighlightTarget target = targets[i];
            if (target == null) continue;

            string key = target.Key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (keyedTargets.ContainsKey(key)) continue;

            keyedTargets.Add(key, target);
        }
    }

    public bool Show(string key, float duration = -1f)
    {
        return ShowInternal(key, duration, false);
    }

    // Ignora o defaultDuration e mantém o highlight até Hide/HideAll.
    public bool ShowPersistent(string key)
    {
        return ShowInternal(key, 0f, true);
    }

    public bool Show(Transform targetTransform, string key = null, float duration = -1f)
    {
        return ShowInternal(targetTransform, key, duration, false);
    }

    public bool ShowPersistent(Transform targetTransform, string key = null)
    {
        return ShowInternal(targetTransform, key, 0f, true);
    }

    public bool HasTarget(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return FindTarget(key) != null;
    }

    private bool ShowInternal(string key, float duration, bool forcePersistent)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        UIHighlightTarget target = FindTarget(key);
        if (target == null)
        {
            WarnMissingTarget("Chave de highlight não encontrada: " + key);
            return false;
        }

        return ShowTarget(target, key, duration, forcePersistent);
    }

    private bool ShowInternal(
        Transform targetTransform, string key, float duration, bool forcePersistent)
    {
        if (targetTransform == null) return false;

        UIHighlightTarget target = targetTransform.GetComponentInChildren<UIHighlightTarget>(true);
        if (target == null && highlightPrefab != null)
        {
            target = Instantiate(highlightPrefab, targetTransform, false);
            if (target != null && !string.IsNullOrWhiteSpace(key)) target.SetKey(key);
        }

        if (target == null)
        {
            WarnMissingTarget("Alvo sem UIHighlightTarget: " + targetTransform.name);
            return false;
        }

        return ShowTarget(target, key, duration, forcePersistent);
    }

    public void Hide(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        UIHighlightTarget target = FindTarget(key);
        if (target != null) target.Hide();

        StopHideRoutine(key);
    }

    public void Hide(Transform targetTransform)
    {
        if (targetTransform == null) return;

        UIHighlightTarget target = targetTransform.GetComponentInChildren<UIHighlightTarget>(true);
        if (target == null) return;

        target.Hide();
        StopHideRoutine(target.Key);
    }

    public void HideAll()
    {
        UIHighlightTarget[] targets = FindObjectsOfType<UIHighlightTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) targets[i].Hide();
        }

        StopAllCoroutines();
        hideRoutines.Clear();
    }

    public void Highlight(ObjectiveData data, float duration = -1f)
    {
        if (data == null) return;
        Show(data.highlightKey, duration);
    }

    private bool ShowTarget(
        UIHighlightTarget target, string key, float duration, bool forcePersistent)
    {
        if (target == null) return false;

        target.Show();

        string timerKey = !string.IsNullOrWhiteSpace(key) ? key : target.Key;
        StopHideRoutine(timerKey);

        float wait = forcePersistent ? 0f : (duration > 0f ? duration : defaultDuration);
        if (wait > 0f)
            hideRoutines[timerKey] = StartCoroutine(HideAfter(target, timerKey, wait));

        return true;
    }

    private IEnumerator HideAfter(UIHighlightTarget target, string timerKey, float wait)
    {
        yield return new WaitForSecondsRealtime(wait);

        hideRoutines.Remove(timerKey);
        if (target != null) target.Hide();
    }

    private UIHighlightTarget FindTarget(string key)
    {
        if (keyedTargets.TryGetValue(key, out UIHighlightTarget target) && target != null)
            return target;

        RebuildRegistry();
        keyedTargets.TryGetValue(key, out target);
        return target;
    }

    private void StopHideRoutine(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!hideRoutines.TryGetValue(key, out Coroutine routine)) return;

        if (routine != null) StopCoroutine(routine);
        hideRoutines.Remove(key);
    }

    private void WarnMissingTarget(string message)
    {
        if (warnedMissingTarget) return;
        warnedMissingTarget = true;
        Debug.LogWarning("[Highlight] " + message, this);
    }
}
