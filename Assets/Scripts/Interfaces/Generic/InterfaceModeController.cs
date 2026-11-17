using UnityEngine;

public enum InterfaceMode
{
    None,
    Building,
    Programming
}

public class InterfaceModeController : MonoBehaviour
{
    [SerializeField] private GameObject buildingInterface;
    [SerializeField] private GameObject programmingInterface;

    public InterfaceMode CurrentMode { get; private set; } = InterfaceMode.None;

    public void ShowBuildingInterface()
    {
        SetMode(InterfaceMode.Building);
    }

    public void ShowProgrammingInterface()
    {
        SetMode(InterfaceMode.Programming);
    }

    public void HideInterfaces()
    {
        SetMode(InterfaceMode.None);
    }

    public void RequestRobotSpawn()
    {
        RobotSignals.TriggerSpawnRobotRequested();
    }

    public void SetMode(InterfaceMode mode)
    {
        CurrentMode = mode;
        SetActive(buildingInterface, mode == InterfaceMode.Building);
        SetActive(programmingInterface, mode == InterfaceMode.Programming);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null) target.SetActive(value);
    }
}
