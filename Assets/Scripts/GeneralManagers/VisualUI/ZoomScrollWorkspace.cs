using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ZoomScrollWorkspace : MonoBehaviour, IScrollHandler
{
    [Header("Alvo do Zoom e Movimento")]
    [SerializeField] private RectTransform conteudoTransform;

    private Canvas canvas;
    private RectTransform paiTransform;
    private bool isDragging = false;
    private Vector2 lastMousePosition;

    [Header("Configurações de Zoom")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float maxScale = 3f;

    [Header("Botões de Zoom")]
    [SerializeField] private Button btnZoomIn;
    [SerializeField] private Button btnZoomOut;

    [HideInInspector] public RectTransform blockInDrag;

    void Awake()
    {
        paiTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        
        if (conteudoTransform == null && transform.childCount > 0)
        {
            conteudoTransform = transform.GetChild(0).GetComponent<RectTransform>();
        }

        blockInDrag = null;

        if (btnZoomIn != null) btnZoomIn.onClick.AddListener(ZoomIn);
        if (btnZoomOut != null) btnZoomOut.onClick.AddListener(ZoomOut);
    }

    void Start()
    {
        AtualizarEstadoBotoes();
    }

    void Update()
    {
        HandleRightClickDrag();
    }

    // Chame este método apenas quando alterar o tamanho do painel (ex: no evento do Toggle de Fullscreen)
    public void RecalculateBounds()
    {
        if (conteudoTransform == null || paiTransform == null) return;

        float minScale = CalcularMinScale();
        if (conteudoTransform.localScale.x < minScale)
        {
            conteudoTransform.localScale = new Vector3(minScale, minScale, 1f);
        }

        conteudoTransform.localPosition = ClampPosition(conteudoTransform.localPosition);
        AtualizarEstadoBotoes();
    }

    private void HandleRightClickDrag()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(paiTransform, Input.mousePosition, canvas.worldCamera))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
        }

        if (Input.GetMouseButton(1) && isDragging)
        {
            Vector2 currentMousePosition = Input.mousePosition;
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;

            Vector3 novaPosicao = conteudoTransform.localPosition + (Vector3)(mouseDelta / canvas.scaleFactor);
            conteudoTransform.localPosition = ClampPosition(novaPosicao);
            
            lastMousePosition = currentMousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (conteudoTransform == null) return;

        float zoomFactor = eventData.scrollDelta.y * zoomSpeed;
        AplicarZoom(zoomFactor, eventData.position, eventData.pressEventCamera);
    }

    public void ZoomIn()
    {
        Vector2 centroTela = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, paiTransform.position);
        AplicarZoom(zoomSpeed, centroTela, canvas.worldCamera);
    }

    public void ZoomOut()
    {
        Vector2 centroTela = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, paiTransform.position);
        AplicarZoom(-zoomSpeed, centroTela, canvas.worldCamera);
    }

    private void AplicarZoom(float deltaZoom, Vector2 posicaoPivo, Camera cam)
    {
        if (conteudoTransform == null) return;

        float oldScale = conteudoTransform.localScale.x;
        float minScale = CalcularMinScale();
        float targetScaleValue = Mathf.Clamp(oldScale + deltaZoom, minScale, maxScale);

        if (Mathf.Approximately(oldScale, targetScaleValue)) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(paiTransform, posicaoPivo, cam, out Vector2 mouseInParent);

        Vector2 localPosAtual = conteudoTransform.localPosition;
        Vector2 pontoNoFilho = (mouseInParent - localPosAtual) / oldScale;

        Vector3 targetScale = new Vector3(targetScaleValue, targetScaleValue, 1f);
        conteudoTransform.localScale = targetScale;

        Vector3 novaPosicao = mouseInParent - (pontoNoFilho * targetScaleValue);
        conteudoTransform.localPosition = ClampPosition(novaPosicao);

        if (blockInDrag) {
            AdjustBlockInDrag(blockInDrag, targetScale);
        }

        AtualizarEstadoBotoes();
    }

    private void AtualizarEstadoBotoes()
    {
        if (conteudoTransform == null) return;

        float menorZoomPermitido = CalcularMinScale();
        float escAtual = conteudoTransform.localScale.x;

        if (btnZoomIn != null)
            btnZoomIn.interactable = escAtual < maxScale - 0.001f;

        if (btnZoomOut != null)
            btnZoomOut.interactable = escAtual > menorZoomPermitido + 0.001f;
    }

    private float CalcularMinScale()
    {
        Vector2 tamanhoPai = paiTransform.rect.size;
        Vector2 tamanhoFilhoOriginal = conteudoTransform.rect.size;

        if (tamanhoFilhoOriginal.x == 0 || tamanhoFilhoOriginal.y == 0) return 1f;

        float scaleNecessarioParaLargura = tamanhoPai.x / tamanhoFilhoOriginal.x;
        float scaleNecessarioParaAltura = tamanhoPai.y / tamanhoFilhoOriginal.y;

        return Mathf.Max(scaleNecessarioParaLargura, scaleNecessarioParaAltura);
    }

    // Cálculo exato para o Pai com Pivot X = 1, Y = 0.5 e Filho com Pivot X = 0.5, Y = 0.5
    private Vector3 ClampPosition(Vector3 posicaoAlvo)
    {
        Vector2 tamanhoPai = paiTransform.rect.size;

        float larguraFilhoEscalado = conteudoTransform.rect.width * conteudoTransform.localScale.x;
        float alturaFilhoEscalado = conteudoTransform.rect.height * conteudoTransform.localScale.y;

        // Limites horizontais para Pivot X = 1 no Pai
        float minX = -larguraFilhoEscalado / 2f;
        float maxX = -tamanhoPai.x + (larguraFilhoEscalado / 2f);

        // Limites verticais para Pivot Y = 0.5 no Pai
        float minY = (tamanhoPai.y - alturaFilhoEscalado) / 2f;
        float maxY = (alturaFilhoEscalado - tamanhoPai.y) / 2f;

        float clampX = Mathf.Clamp(posicaoAlvo.x, minX, maxX);
        float clampY = Mathf.Clamp(posicaoAlvo.y, minY, maxY);

        return new Vector3(clampX, clampY, posicaoAlvo.z);
    }

    public void AdjustBlockInDrag(RectTransform block, Vector3 targetScale) {
        block.localScale = targetScale;
        Vector3 mouseWorldPos = Input.mousePosition;
        mouseWorldPos.z = 0f; 
        block.transform.position = mouseWorldPos;
    }
}