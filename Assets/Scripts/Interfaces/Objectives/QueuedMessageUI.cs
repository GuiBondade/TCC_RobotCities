using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Fila de mensagens temporizadas usada pelos avisos de desbloqueio.
// Não conhece objetivos nem desafios: só recebe textos e os exibe em ordem.
public class QueuedMessageUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Comportamento")]
    [SerializeField, Min(0.1f)] private float defaultDuration = 3f;
    [SerializeField, Min(1)] private int maxQueued = 4;
    [SerializeField] private bool hideWhenIdle = true;

    private struct Message
    {
        public readonly string text;
        public readonly float duration;

        public Message(string text, float duration)
        {
            this.text = text;
            this.duration = duration;
        }
    }

    private readonly Queue<Message> queue = new Queue<Message>();
    private Coroutine routine;
    private bool warnedMissingLabel;

    public bool IsShowing => routine != null;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnDisable()
    {
        StopRoutine();
        queue.Clear();
        if (hideWhenIdle) SetVisible(false);
    }

    public void Enqueue(string text, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        EnsureReferences();
        if (messageLabel == null)
        {
            if (!warnedMissingLabel)
            {
                Debug.LogWarning("[Objectives] QueuedMessageUI sem TMP_Text configurado.", this);
                warnedMissingLabel = true;
            }
            return;
        }

        if (queue.Count >= Mathf.Max(1, maxQueued)) queue.Dequeue();
        queue.Enqueue(new Message(text, duration));

        if (routine == null) routine = StartCoroutine(PlayQueue());
    }

    public void Clear()
    {
        queue.Clear();
        StopRoutine();
        if (hideWhenIdle) SetVisible(false);
    }

    private IEnumerator PlayQueue()
    {
        while (queue.Count > 0)
        {
            Message message = queue.Dequeue();
            if (messageLabel != null) messageLabel.text = message.text;
            SetVisible(true);

            float wait = message.duration > 0f ? message.duration : defaultDuration;
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);
            else
                yield return null;
        }

        routine = null;
        if (hideWhenIdle) SetVisible(false);
    }

    private void StopRoutine()
    {
        if (routine == null) return;

        StopCoroutine(routine);
        routine = null;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else if (messageLabel != null)
        {
            messageLabel.gameObject.SetActive(visible);
        }
    }

    private void EnsureReferences()
    {
        if (messageLabel == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;

                string objectName = texts[i].gameObject.name;
                if (objectName == "MessageLabel" || objectName == "NoticeLabel" ||
                    objectName == "UnlockLabel")
                {
                    messageLabel = texts[i];
                    break;
                }
            }

            if (messageLabel == null && texts.Length > 0) messageLabel = texts[0];
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }
}
