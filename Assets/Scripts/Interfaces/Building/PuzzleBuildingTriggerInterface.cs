using UnityEngine;

// Trigger próprio da Construção de Desafios (instância da cena): em vez da
// BuildingInterface padrão, abre a PuzzleInterface. Herda de BuildingTriggerInterface
// para reaproveitar o targetScript e o OnMouseDown (modo destruição / clique sobreposto).
public class PuzzleBuildingTriggerInterface : BuildingTriggerInterface
{
    private void Awake() {
        // O trigger fica no filho com o Collider2D; o BuildingController está no pai.
        if (targetScript == null) targetScript = GetComponentInParent<BuildingController>();
    }

    public override void OpenInterface(BuildingController script, Vector2 position) {
        if (script == null) {
            Debug.LogWarning("[Puzzle] PuzzleBuildingTriggerInterface sem BuildingController configurado.", this);
            return;
        }

        PuzzleSignals.TriggerInterfaceOpen(script, position);
    }
}
