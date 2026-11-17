using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Tick Speed", fileName = "TickSpeed")]
public class TickSpeedSO : ScriptableObject
{
    public float speedTick = 5f; // ticks por segundo (5 = tickTime de 0.2s)

    [SerializeField] private float minSpeedTick = 0.5f;
    [SerializeField] private float maxSpeedTick = 20f;

    private event Action<float> OnSpeedChanged;

    // interface -> esse asset (a interface chama TickSpeedSignals.TriggerTickSpeedChange)
    private void OnEnable() {
        TickSpeedSignals.OnTickSpeedChangeRequested += ChangeSpeed;
    }

    private void OnDisable() {
        TickSpeedSignals.OnTickSpeedChangeRequested -= ChangeSpeed;
        OnSpeedChanged = null;
    }

    // chamado pelo signal enviado pela interface
    public void ChangeSpeed(float delta) {
        speedTick = Mathf.Clamp(speedTick + delta, minSpeedTick, maxSpeedTick);
        Debug.Log("speedTick = " + speedTick);
        InvokeSpeedChanged();
    }

    // limites expostos pra UI (o clamp do ChangeSpeed continua sendo a fonte da verdade)
    public bool AtingiuMaximo => speedTick >= maxSpeedTick;
    public bool AtingiuMinimo => speedTick <= minSpeedTick;

    // esse asset -> robos (inscritos no spawn/setup) e executionManager (inscrito no start)
    public void RegisterListener(Action<float> listener) => OnSpeedChanged += listener;

    public void UnregisterListener(Action<float> listener) => OnSpeedChanged -= listener;

    private void InvokeSpeedChanged() {
        OnSpeedChanged?.Invoke(speedTick);
    }
}
