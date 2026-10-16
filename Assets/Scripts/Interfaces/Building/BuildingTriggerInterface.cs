using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingTriggerInterface : TriggerInterface<BuildingController>
{
    public override void OpenInterface(BuildingController script, Vector2 position) {
        BuildingSignals.TriggerInterfaceOpen(script, position); 
        Debug.Log("building interface open");
    }
}
