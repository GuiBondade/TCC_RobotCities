using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base não-genérica: permite que o resolvedor de clique trate qualquer
// TriggerInterface (construção, robô ou recurso) sem conhecer o tipo concreto.
public abstract class TriggerInterfaceBase : MonoBehaviour
{
    public abstract MonoBehaviour TargetScript { get; }

    // Abre a interface real do alvo (o mesmo comportamento do clique direto).
    public abstract void OpenTarget(Vector2 position);
}

public abstract class TriggerInterface<T> : TriggerInterfaceBase where T : MonoBehaviour
{
    [SerializeField] protected T targetScript;
    private Vector2 mousePosition;

    public override MonoBehaviour TargetScript => targetScript;

    public override void OpenTarget(Vector2 position) {
        OpenInterface(targetScript, position);
    }

    public virtual void OnMouseDown() {
        mousePosition = Input.mousePosition;
        if (targetScript is BuildingController building && ConstructionSignals.IsDestroyModeActive) {
            ConstructionSignals.TriggerConstructionDestroyRequested(building);
            return;
        }

        if (ConstructionSignals.IsPlacementModeActive || ConstructionSignals.IsDestroyModeActive) return;

        // Em vez de abrir direto, resolve todos os colliders sob o mouse:
        // com várias entidades sobrepostas, o jogador escolhe qual interface abrir.
        EntitySelectionResolver.ResolveClick(mousePosition);
    }

    public virtual void OpenInterface(T script, Vector2 position){}
}
