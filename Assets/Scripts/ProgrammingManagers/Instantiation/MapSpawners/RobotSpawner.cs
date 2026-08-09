using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotSpawner : MonoBehaviour
{
    [SerializeField] public ExecutionManager executionManager;
    [SerializeField] public Grid tileGrid;

    [SerializeField] public Transform robotContainer;
    [SerializeField] public GameObject robotPrefab;

    [SerializeField] public Vector3 spawnPosition;

    public Vector2 tileSize;
    public float robotSpeed {get; private set;} 

    void Start() {
        tileSize = new Vector2(tileGrid.cellSize.x, tileGrid.cellSize.y);
        robotSpeed = tileSize.x/executionManager.tickTime;
    }

    void SpawnRobot() {
         GameObject robot = Instantiate(robotPrefab, robotContainer);
         Vector3Int spawnCell = tileGrid.WorldToCell(spawnPosition);
         robot.transform.position = spawnPosition;
         robot.GetComponent<RobotScript>().StartSetup(spawnCell, tileSize, robotSpeed);
         //animation?
    }
}
