using UnityEngine;

// Controla a câmera do mapa principal enquanto um desafio está aberto.
// O mapa do puzzle não tem câmera própria: a câmera existente é levada até o
// ponto da etapa e devolvida à posição original quando o desafio é fechado.
public class PuzzleCameraRig : MonoBehaviour
{
    [Tooltip("Câmera usada pelo desafio. Vazio = Camera.main (ou a primeira câmera encontrada).")]
    [SerializeField] private Camera targetCamera;

    private Camera savedCamera;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedOrthographicSize;
    private bool hasSavedState;

    public bool HasSavedState => hasSavedState;

    public Camera ResolveCamera() {
        if (targetCamera != null) return targetCamera;

        targetCamera = Camera.main;
        if (targetCamera == null) targetCamera = FindObjectOfType<Camera>();
        return targetCamera;
    }

    public void Capture(Camera camera = null) {
        Camera cam = camera != null ? camera : ResolveCamera();
        if (cam == null) return;

        targetCamera = cam;
        savedCamera = cam;
        savedPosition = cam.transform.position;
        savedRotation = cam.transform.rotation;
        savedOrthographicSize = cam.orthographicSize;
        hasSavedState = true;
    }

    public void SnapTo(Transform anchor, Vector3 fallbackPosition, float orthographicSize = -1f) {
        Vector3 position = anchor != null ? anchor.position : fallbackPosition;
        SnapTo(position, orthographicSize);
    }

    public void SnapTo(Vector3 position, float orthographicSize = -1f) {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        if (!hasSavedState || savedCamera != cam) Capture(cam);

        Vector3 target = position;
        target.z = savedPosition.z;
        cam.transform.position = target;
        cam.transform.rotation = savedRotation;

        if (orthographicSize > 0f) cam.orthographicSize = orthographicSize;
    }

    // Volta a câmera exatamente para o estado anterior ao desafio.
    public void Restore() {
        if (!hasSavedState || savedCamera == null) return;

        savedCamera.transform.position = savedPosition;
        savedCamera.transform.rotation = savedRotation;
        savedCamera.orthographicSize = savedOrthographicSize;
        hasSavedState = false;
    }
}
