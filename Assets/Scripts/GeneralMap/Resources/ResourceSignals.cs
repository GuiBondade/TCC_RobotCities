using System;
using UnityEngine;

// Signals do recurso (mesmo padrão de BuildingSignals/RobotSignals): o clique no
// collider dispara a abertura da interface e o reposicionamento dela no mouse.
public static class ResourceSignals
{
    public static event Action<ResourceController> OnInterfacePopulation;
    public static event Action<Vector2> OnInterfaceReposition;

    public static void TriggerInterfaceOpen(ResourceController resource, Vector2 mousePosition) {
        // InvokeAlive: remove assinantes mortos (domain reload desligado no Play Mode).
        InvokeAlive(OnInterfacePopulation, cb => cb(resource), cb => OnInterfacePopulation -= cb);
        InvokeAlive(OnInterfaceReposition, cb => cb(mousePosition), cb => OnInterfaceReposition -= cb);
    }

    private static void InvokeAlive<T>(Action<T> callbacks, Action<Action<T>> invoke, Action<Action<T>> remove) {
        if (callbacks == null) return;
        foreach (var callback in callbacks.GetInvocationList()) {
            var typedCallback = (Action<T>)callback;
            var target = typedCallback.Target as UnityEngine.Object;
            if (typedCallback.Target != null && target == null) {
                remove(typedCallback);
                continue;
            }
            invoke(typedCallback);
        }
    }

    // Com o domain reload desligado no Play Mode, eventos estáticos sobrevivem
    // entre sessões. Limpa tudo ao iniciar o Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        OnInterfacePopulation = null;
        OnInterfaceReposition = null;
    }
}