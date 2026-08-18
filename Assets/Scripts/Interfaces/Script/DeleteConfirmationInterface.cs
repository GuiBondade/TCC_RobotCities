using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeleteConfirmationInterface : MonoBehaviour
{
    private GameObject scriptGO;
    private ScriptData scriptData;

    public void SetSignal() {
        ScriptSignals.OnDeleteInterfaceOpen += DeleteInterfaceOpen;
    }

    private void DeleteInterfaceOpen(GameObject GO, ScriptData data) {
        gameObject.SetActive(true);
        scriptGO = GO;
        scriptData = data;
    }

    public void DeletionAccepted() {
        ScriptSignals.TriggerDeletionInterfaceAccepted(scriptGO, scriptData);
        gameObject.SetActive(false);
    }

    public void DeletionRejected() {
        scriptGO = null;
        scriptData = null;
        gameObject.SetActive(false);
    }
}
