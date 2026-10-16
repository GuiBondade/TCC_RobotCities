using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterfaceSignalHub : MonoBehaviour
{
    [SerializeField] private RobotInterface robotInterface;
    [SerializeField] private BuildingInterface buildingInterface;
    [SerializeField] private DeleteConfirmationInterface deleteInterface;

    private void Start() {
        robotInterface.SetSignal();
        buildingInterface.SetSignal();
        deleteInterface.SetSignal();
    }
}
