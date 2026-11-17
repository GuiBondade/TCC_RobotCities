using UnityEngine;

// Clique no recurso (BoxCollider2D): dispara a abertura da interface dele.
public class ResourceTriggerInterface : TriggerInterface<ResourceController>
{
    private void Awake() {
        // Fallback: o trigger pode ficar no filho que tem o Collider2D.
        if (targetScript == null) targetScript = GetComponentInParent<ResourceController>();
    }

    public override void OpenInterface(ResourceController script, Vector2 position) {
        if (script == null) {
            Debug.LogWarning("[Recurso] ResourceTriggerInterface sem ResourceController configurado.", this);
            return;
        }

        ResourceSignals.TriggerInterfaceOpen(script, position);
    }
}
