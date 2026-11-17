using System;

public static class TickSpeedSignals
{
    public static event Action<float> OnTickSpeedChangeRequested;

    // interface chama esse método ao alterar velocidade do tick, com delta sendo o valor int negativo ou positivo referente a mudança
    public static void TriggerTickSpeedChange(float delta) {
        OnTickSpeedChangeRequested?.Invoke(delta);
    }
}
