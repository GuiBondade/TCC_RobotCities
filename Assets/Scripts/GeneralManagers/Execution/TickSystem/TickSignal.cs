using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Tick Update", fileName = "TickUpdate")]
public class TickSignal : ScriptableObject
{
    private event Action OnTick;

    public void RegisterListener(Action listener) => OnTick += listener;

    public void UnregisterListener(Action listener) => OnTick -= listener;

    public void InvokeTick()
    {
        OnTick?.Invoke();
    }

    private void OnDisable()
    {
        OnTick = null;
    }
}
