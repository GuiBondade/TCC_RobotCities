using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class BuildingSignals
{
    public static event Action<BuildingController> OnInterfacePopulation;
    public static event Action<Vector2> OnInterfaceReposition;

    public static void TriggerInterfaceOpen(BuildingController buildingController, Vector2 mousePosition) {
        OnInterfacePopulation?.Invoke(buildingController);   
        OnInterfaceReposition?.Invoke(mousePosition);   
    }
}
