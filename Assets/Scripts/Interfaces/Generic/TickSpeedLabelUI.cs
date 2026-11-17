using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mostra no TMP_Text o speedTick atual do TickSpeedSO — o mesmo valor que
// robôs, spawners e ExecutionManager recebem pelo listener de saída do asset.
// Também desabilita os botões do stepper quando o limite é atingido.
public class TickSpeedLabelUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TickSpeedSO tickSpeedSO;
    [SerializeField] private TMP_Text label;

    [Header("Botões do stepper")]
    [SerializeField] private Button botaoMais;       // StepPlus  (+1)
    [SerializeField] private Button botaoMaisTurbo;  // TurboPlus (+5)
    [SerializeField] private Button botaoMenos;      // StepMinus (-1)
    [SerializeField] private Button botaoMenosTurbo; // TurboMinus (-5)

    [Header("Formato")]
    [SerializeField] private string formato = "0.##";
    [SerializeField] private string sufixo = "x";

    private bool inscrito;

    private void Awake() {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable() {
        if (inscrito) return;

        if (tickSpeedSO == null) {
            Debug.LogWarning("[TickSpeed] TickSpeedLabelUI sem TickSpeedSO configurado.", this);
            return;
        }

        tickSpeedSO.RegisterListener(AtualizarLabel);
        inscrito = true;
        AtualizarLabel(tickSpeedSO.speedTick); // valor atual, igual ExecutionManager/RobotScript
    }

    private void OnDisable() {
        if (!inscrito) return;

        tickSpeedSO.UnregisterListener(AtualizarLabel);
        inscrito = false;
    }

    private void AtualizarLabel(float speedTick) {
        if (label != null) label.text = speedTick.ToString(formato) + sufixo;

        AtualizarBotoes();
    }

    // No máximo, os botões de aumentar não teriam efeito (ChangeSpeed clamparia);
    // no mínimo, o mesmo pros de reduzir.
    private void AtualizarBotoes() {
        if (tickSpeedSO == null) return;

        bool podeAumentar = !tickSpeedSO.AtingiuMaximo;
        bool podeReduzir = !tickSpeedSO.AtingiuMinimo;

        if (botaoMais != null) botaoMais.interactable = podeAumentar;
        if (botaoMaisTurbo != null) botaoMaisTurbo.interactable = podeAumentar;
        if (botaoMenos != null) botaoMenos.interactable = podeReduzir;
        if (botaoMenosTurbo != null) botaoMenosTurbo.interactable = podeReduzir;
    }
}
