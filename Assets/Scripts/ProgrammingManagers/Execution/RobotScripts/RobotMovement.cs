using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotMovement : MonoBehaviour
{
    public bool isMoving = false;

    public Vector2 tilePosition;
    public Vector2 tileSize;
    public float robotSpeed;

    private Queue<Vector2> tileQueue = new Queue<Vector2>();
    private Vector2 currentTile;

    public void MoveTo(Vector2Int direction, int distance) {
        tileQueue.Clear();
        Debug.Log("Move: " + distance);
        for (int i = 1; i <= distance; i++) {
            Vector2 nextTile = (Vector2)transform.position + (direction * i * tileSize);
            tileQueue.Enqueue(nextTile);
        }
        Debug.Log(tileQueue.Count);

        if (tileQueue.Count > 0) {
            currentTile = tileQueue.Dequeue();
            isMoving = true;
        }
    }

    void Update() {
        if (!isMoving) return;

        var step = robotSpeed * Time.deltaTime;

        transform.position = Vector2.MoveTowards(transform.position, currentTile, step);

        if ((Vector2)transform.position == currentTile) {
            if (tileQueue.Count > 0) {
                Debug.Log("deu um passo");
                currentTile = tileQueue.Dequeue();
                tilePosition = currentTile;
            } else {
                isMoving = false;
            }
        }
    } 
}
