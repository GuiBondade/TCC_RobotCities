using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class TriggerInterface<T> :  MonoBehaviour where T : MonoBehaviour
{
    [SerializeField] protected T targetScript;
    private Vector2 mousePosition;

    public virtual void OnMouseDown() {
        mousePosition = Input.mousePosition;
        OpenInterface(targetScript, mousePosition); 
        Debug.Log("interface open");
    }

    public virtual void OpenInterface(T script, Vector2 position){}
}
