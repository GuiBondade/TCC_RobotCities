using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Balão de debug: expira sozinho depois de N ticks e desbota nos últimos 10.
public class DebugBalloon : MonoBehaviour
{
    [Tooltip("Duração em ticks. Ao chegar a 0 o balão é destruído.")]
    [SerializeField] public int ticksDuracao = 20;

    [Tooltip("Fonte dos ticks. Vazio = usa o TickSignal do ExecutionManager.")]
    [SerializeField] public TickSignal tickSignal;

    [Tooltip("Opcional: se vazio, pega o TMP_Text do filho DebugLabel.")]
    [SerializeField] public TMP_Text label;

    [Tooltip("Opcional: se vazio, procura um CanvasGroup na raiz.")]
    [SerializeField] public CanvasGroup canvasGroup;

    private Image fundo;
    private int ticksRestantes;
    private bool inscrito;

    public int TicksRestantes => ticksRestantes;

    private void Awake() {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) fundo = GetComponent<Image>();
        ticksRestantes = Mathf.Max(1, ticksDuracao);
    }

    private void OnEnable() => TrySubscribe();

    private void OnDisable() {
        if (inscrito && tickSignal != null) tickSignal.UnregisterListener(ProcessTick);
        inscrito = false;
    }

    // Mesmo padrão do CraftingManager/ResourceSpawnManager: o prefab pode ter o
    // TickSignal setado no Inspector, senão usa o tick global do ExecutionManager.
    private void TrySubscribe() {
        if (inscrito) return;

        if (tickSignal == null && ExecutionManager.instancia != null) {
            tickSignal = ExecutionManager.instancia.tickSignal;
        }

        if (tickSignal == null) {
            if (Application.isPlaying) {
                Debug.LogError("[DebugBalloon] TickSignal não encontrado: o balão não vai expirar.", this);
            }
            return;
        }

        tickSignal.RegisterListener(ProcessTick);
        inscrito = true;
    }

    public void DefinirTexto(string texto) {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        label.text = texto;      // texto do bloco (1 linha) ou "nome: valor" da variável
        label.ForceMeshUpdate(); // preferred size correto antes do rebuild do layout
    }

    private void ProcessTick() {
        ticksRestantes--;

        if (ticksRestantes <= 0) {
            // Sai do tick na hora: o OnDisable do Destroy só roda no fim do frame.
            if (inscrito && tickSignal != null) tickSignal.UnregisterListener(ProcessTick);
            inscrito = false;

            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        AtualizarOpacidade();
    }

    // 10+ ticks restantes: 100% (padrão, nada a fazer). A partir de 9 começa a reduzir:
    // 9 = 90%, 8 = 80%, ... 1 = 10%.
    private void AtualizarOpacidade() {
        if (ticksRestantes >= 10) return;

        float alpha = ticksRestantes * 0.1f;

        if (canvasGroup != null) {
            canvasGroup.alpha = alpha;
            return;
        }

        if (fundo != null) {
            Color cor = fundo.color;
            cor.a = alpha;
            fundo.color = cor;
        }
        if (label != null) label.alpha = alpha;
    }

    private void OnValidate() {
        if (ticksDuracao < 1) ticksDuracao = 1;
    }
}
