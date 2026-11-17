using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Confirmação opcional ao sair do desafio. Pode ser um prefab de UI atribuído no
// PuzzleManager; se não existir, a saída acontece direto.
public class PuzzleExitConfirmation : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text messageLabel;

    [Header("Botões")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Comportamento")]
    [SerializeField] private bool destroyOnHide = true;

    private Action onConfirm;
    private Action onCancel;
    private bool wired;

    private void Awake() {
        EnsureReferences();
        WireButtons();
    }

    public void Show(string title, string message, Action confirm, Action cancel = null) {
        EnsureReferences();
        WireButtons();

        onConfirm = confirm;
        onCancel = cancel;

        if (titleLabel != null) titleLabel.text = title;
        if (messageLabel != null) messageLabel.text = message;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void SetDestroyOnHide(bool destroy) {
        destroyOnHide = destroy;
    }

    public void Hide() {
        onConfirm = null;
        onCancel = null;

        if (destroyOnHide) {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }

    private void Confirm() {
        Action callback = onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void Cancel() {
        Action callback = onCancel;
        Hide();
        callback?.Invoke();
    }

    private void EnsureReferences() {
        if (titleLabel == null) titleLabel = FindText("TitleLabel", "TituloLabel", "Título");
        if (messageLabel == null) messageLabel = FindText("MessageLabel", "DescriptionLabel", "MensagemLabel");

        if (confirmButton == null) confirmButton = FindButton("ConfirmButton", "ConfirmarButton", "SimButton");
        if (cancelButton == null) cancelButton = FindButton("CancelButton", "CancelarButton", "NaoButton");

        if (confirmButton == null || cancelButton == null) {
            var buttons = GetComponentsInChildren<Button>(true);
            if (confirmButton == null && buttons.Length > 0) confirmButton = buttons[0];
            if (cancelButton == null && buttons.Length > 1) cancelButton = buttons[1];
        }
    }

    private void WireButtons() {
        if (wired) return;
        wired = true;

        if (confirmButton != null) {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }
        if (cancelButton != null) {
            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);
        }
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

    private void OnDestroy() {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
    }
}
