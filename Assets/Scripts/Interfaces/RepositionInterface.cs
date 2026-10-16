using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RepositionInterface : MonoBehaviour
{
    [SerializeField] private RectTransform interfaceRect;
    [SerializeField] private RectTransform canvasRect;
    private Vector2 offset; 

    public void SetupPosition(Vector2 mousePosition) 
    {
        // 1. Converte a posição do mouse para Viewport (0 a 1) e centraliza (-0.5 a +0.5)
        Vector2 viewportPos = Camera.main.ScreenToViewportPoint(mousePosition);
        Vector2 centeredPos = viewportPos - new Vector2(0.5f, 0.5f);

        // 2. Define qual quina do painel será a âncora (Pivot)
        // Se o mouse estiver na direita (X > 0), usa a quina direita (1). Caso contrário, esquerda (0).
        float pivotX = centeredPos.x > 0f ? 1f : 0f;
        float pivotY = centeredPos.y > 0f ? 1f : 0f;
        
        interfaceRect.pivot = new Vector2(pivotX, pivotY);

        // 3. Converte as coordenadas do mouse (pixels) para as coordenadas locais do Canvas
        Vector2 localPoint;
        Camera uiCamera = canvasRect.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePosition, uiCamera, out localPoint);

        // 4. Calcula a direção do offset (empurra na direção oposta à quina)
        float dirX = pivotX == 1f ? -1f : 1f;
        float dirY = pivotY == 1f ? -1f : 1f;

        Vector2 appliedOffset = new Vector2(offset.x * dirX, offset.y * dirY);

        // 5. Aplica a posição final no painel
        interfaceRect.localPosition = localPoint + appliedOffset;

        // 6. Trava de segurança para garantir que a UI fique 100% visível na tela
        ForceUIInsideCanvas();
    }

    // O método auxiliar para garantir que a interface não vaze as bordas da tela
    private void ForceUIInsideCanvas()
    {
        Vector3[] canvasCorners = new Vector3[4];
        Vector3[] panelCorners = new Vector3[4];

        canvasRect.GetWorldCorners(canvasCorners);
        interfaceRect.GetWorldCorners(panelCorners);

        float shiftX = 0f;
        float shiftY = 0f;

        if (panelCorners[0].x < canvasCorners[0].x) shiftX = canvasCorners[0].x - panelCorners[0].x;
        if (panelCorners[2].x > canvasCorners[2].x) shiftX = canvasCorners[2].x - panelCorners[2].x;
        if (panelCorners[0].y < canvasCorners[0].y) shiftY = canvasCorners[0].y - panelCorners[0].y;
        if (panelCorners[1].y > canvasCorners[1].y) shiftY = canvasCorners[1].y - panelCorners[1].y;

        interfaceRect.position += new Vector3(shiftX, shiftY, 0f);
    }
}
