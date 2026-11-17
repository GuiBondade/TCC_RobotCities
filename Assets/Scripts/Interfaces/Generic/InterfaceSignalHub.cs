using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterfaceSignalHub : MonoBehaviour
{
    [SerializeField] private RobotInterface robotInterface;
    [SerializeField] private BuildingInterface buildingInterface;
    [SerializeField] private DeleteConfirmationInterface deleteInterface;
    [SerializeField] private PuzzleInterface puzzleInterface;
    [SerializeField] private ResourceInterface resourceInterface;

    private void Start() {
        robotInterface.SetSignal();
        buildingInterface.SetSignal();
        deleteInterface.SetSignal();

        // O GameObject do puzzle já existe na cena (e fica inativo até abrir),
        // então o fallback procura inclusive entre objetos inativos.
        if (puzzleInterface == null) puzzleInterface = FindObjectOfType<PuzzleInterface>(true);
        if (puzzleInterface != null) puzzleInterface.SetSignal();

        // Mesmo padrão para o recurso: o GameObject fica inativo até abrir.
        if (resourceInterface == null) resourceInterface = FindObjectOfType<ResourceInterface>(true);
        if (resourceInterface != null) resourceInterface.SetSignal();
    }
}
