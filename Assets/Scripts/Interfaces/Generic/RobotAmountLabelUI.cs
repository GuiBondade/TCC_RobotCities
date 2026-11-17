using TMPro;
using UnityEngine;

// Mostra no TMP_Text quantos robôs já foram criados no mapa. Valor de runtime:
// não é salvo — ao carregar a cena os spawns rodam de novo e o contador recomeça.
public class RobotAmountLabelUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text label;

    private int contador;
    private bool inscrito;

    private void Awake() {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable() {
        if (inscrito) return;
        RobotSignals.OnRobotSpawned += OnRobotSpawned;
        inscrito = true;
        SincronizarComMapa(); // pega robôs que já existiam quando a HUD ligou
    }

    private void OnDisable() {
        if (!inscrito) return;
        RobotSignals.OnRobotSpawned -= OnRobotSpawned;
        inscrito = false;
    }

    private void OnRobotSpawned(RobotScript robot) {
        contador++;
        AtualizarLabel();
    }

    private void SincronizarComMapa() {
        var map = MapManager.instancia;
        if (map != null && map.allRobots != null) contador = map.allRobots.Count;
        AtualizarLabel();
    }

    private void AtualizarLabel() {
        if (label != null) label.text = contador.ToString();
    }
}
