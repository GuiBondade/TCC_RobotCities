using System;
using UnityEngine;

/// <summary>
/// Signals globais de construções.
/// Segue o mesmo padrão de RobotSignals / BuildingSignals / ScriptSignals,
/// funcionando entre as cenas (a UI vive no UIScene e o PlacementManager no MapScene).
/// </summary>
public static class ConstructionSignals
{
    /// <summary>Construção escolhida pelo jogador na paleta de construções.</summary>
    public static event Action<ConstructionData> OnConstructionSelected;

    /// <summary>Pedido para entrar no modo de destruição (botão "Destruir" da UI).</summary>
    public static event Action OnDestroyModeRequested;

    /// <summary>Pedido para cancelar o modo atual (posicionamento ou destruição).</summary>
    public static event Action OnPlacementCancelled;

    /// <summary>Construção recém-colocada, entrando na fase de obra.</summary>
    public static event Action<BuildingController> OnConstructionStarted;

    /// <summary>Construção concluída e liberada (virou funcional).</summary>
    public static event Action<BuildingController> OnBuildingActivated;

    /// <summary>Construção clicada enquanto o modo de destruição está ativo.</summary>
    public static event Action<BuildingController> OnConstructionDestroyRequested;

    public static bool IsDestroyModeActive { get; private set; }
    public static bool IsPlacementModeActive { get; private set; }

    public static void TriggerConstructionSelected(ConstructionData data) {
        OnConstructionSelected?.Invoke(data);
    }

    public static void TriggerDestroyModeRequested() {
        OnDestroyModeRequested?.Invoke();
    }

    public static void TriggerPlacementCancelled() {
        OnPlacementCancelled?.Invoke();
    }

    public static void TriggerConstructionStarted(BuildingController building) {
        OnConstructionStarted?.Invoke(building);
    }

    public static void TriggerBuildingActivated(BuildingController building) {
        OnBuildingActivated?.Invoke(building);
    }

    public static void SetDestroyModeActive(bool active) {
        IsDestroyModeActive = active;
    }

    public static void SetPlacementModeActive(bool active) {
        IsPlacementModeActive = active;
    }

    public static void TriggerConstructionDestroyRequested(BuildingController building) {
        OnConstructionDestroyRequested?.Invoke(building);
    }
}
