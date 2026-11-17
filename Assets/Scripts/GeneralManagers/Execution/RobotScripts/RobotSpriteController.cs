using UnityEngine;

// Controle visual da direção do robô: troca o sprite do robô e da ferramenta
// equipada conforme a direção da caminhada (RobotMovement.direction/isMoving).
// Quando a caminhada termina — inclusive esperando o robô terminar de deslizar
// até o tile — volta ao idle de frente (virado pro jogador).
// O sprite da ferramenta vem do ToolData do id equipado no slot Tool do robô:
// frente/trás/lado (o lado é espelhado com flipX pra esquerda).
// Animação de caminhada: se os arrays de frames estiverem preenchidos (spritesheet
// fatiado), avança os frames enquanto anda; caso contrário, usa o sprite estático
// por direção (comportamento anterior, preservado como fallback).
public class RobotSpriteController : MonoBehaviour
{
    [Header("Renderers (vazio = procura nos filhos RobotSprite/ToolSprite)")]
    [SerializeField] private SpriteRenderer robotSpriteRenderer;
    [SerializeField] private SpriteRenderer toolSpriteRenderer;

    [Header("Sprites do robô por direção (idle / fallback estático)")]
    [Tooltip("Parado / andando pra baixo (idle).")]
    [SerializeField] private Sprite spriteFrente;
    [Tooltip("Andando pra cima (costas).")]
    [SerializeField] private Sprite spriteTras;
    [Tooltip("Andando pra direita.")]
    [SerializeField] private Sprite spriteDireita;
    [Tooltip("Andando pra esquerda.")]
    [SerializeField] private Sprite spriteEsquerda;

    [Header("Animação de caminhada (spritesheet fatiado)")]
    [Tooltip("Frames de caminhada pra frente/baixo. Vazio = usa spriteFrente estático.")]
    [SerializeField] private Sprite[] framesFrente;
    [Tooltip("Frames de caminhada pra cima. Vazio = usa spriteTras estático.")]
    [SerializeField] private Sprite[] framesTras;
    [Tooltip("Frames de caminhada pra direita. Vazio = usa spriteDireita estático.")]
    [SerializeField] private Sprite[] framesDireita;
    [Tooltip("Frames de caminhada pra esquerda. Vazio = usa spriteEsquerda estático.")]
    [SerializeField] private Sprite[] framesEsquerda;
    [Tooltip("Frames por segundo da animação de caminhada.")]
    [SerializeField] private float framesPerSecond = 8f;

    private enum Facing { Frente, Tras, Esquerda, Direita }

    private RobotMovement robotMovement;
    private RobotInventory robotInventory;

    private Facing currentFacing = Facing.Frente;
    private bool facingApplied;   // aplica o facing no primeiro Update mesmo sem mudança
    private bool wasWalking;

    // Temporização da animação de caminhada.
    private float frameTimer;
    private int currentFrameIndex;

    // -2 = ainda não resolvido: força a primeira atualização da ferramenta.
    private int currentToolId = -2;
    private ToolData currentTool; // ToolData do id exibido (null = sem ferramenta)

    // Posição do frame anterior: detecta quando o robô terminou de deslizar até o
    // tile (a caminhada lógica acaba antes do visual chegar no lugar).
    private Vector2 lastPosition;
    private bool hasLastPosition;

    // Configuração incompleta avisa uma vez só, sem spammar o Console.
    private bool warnedRobotSprites;
    private bool warnedToolSprites;
    private bool warnedExecutionManager;

    private void Awake() {
        robotMovement = GetComponent<RobotMovement>();
        robotInventory = GetComponent<RobotInventory>();

        // Renderers não ligados no Inspector: resolve pelos filhos do prefab.
        if (robotSpriteRenderer == null) robotSpriteRenderer = FindRenderer("RobotSprite");
        if (toolSpriteRenderer == null) toolSpriteRenderer = FindRenderer("ToolSprite");

        if (robotMovement == null) {
            Debug.LogWarning("[RobotSpriteController] Sem RobotMovement no mesmo objeto: componente desativado.", this);
            enabled = false;
            return;
        }

        if (robotSpriteRenderer == null && toolSpriteRenderer == null) {
            Debug.LogWarning("[RobotSpriteController] Sem SpriteRenderer pra controlar (filhos RobotSprite/ToolSprite): componente desativado.", this);
            enabled = false;
        }
    }

    private void OnEnable() {
        // A ferramenta muda por vários caminhos (equipar, quebrar, retirar da fábrica,
        // trocar de layout): todos notificam o OnInventoryChanged do inventário do robô.
        if (robotInventory != null && robotInventory.inventoryManager != null) {
            robotInventory.inventoryManager.OnInventoryChanged += OnRobotInventoryChanged;
        }
        RefreshTool();
    }

    private void OnDisable() {
        if (robotInventory != null && robotInventory.inventoryManager != null) {
            robotInventory.inventoryManager.OnInventoryChanged -= OnRobotInventoryChanged;
        }
    }

    private void Update() {
        if (robotMovement == null) return;

        // "Caminhando visualmente" = andando de fato (isMoving) ou ainda deslizando até
        // o tile depois de a caminhada terminar (a posição continua mudando nesse caso).
        // Nota: o tilePosition do RobotMovement não é sincronizado no spawn, então a
        // detecção usa a posição do frame anterior — cobre também o robô bloqueado que
        // nunca deu passo (posição não muda = já está parado).
        Vector2 position = transform.position;
        // No primeiro frame o robô ainda não se moveu: considera parado (idle),
        // evitando animar no spawn. Depois disso, detecta deslize pela mudança
        // de posição entre frames.
        bool settled;
        if (!hasLastPosition) {
            hasLastPosition = true;
            lastPosition = position;
            settled = true;
        } else {
            settled = (position == lastPosition);
            lastPosition = position;
        }

        bool walking = robotMovement.isMoving || !settled;
        Facing facing = walking ? ToFacing(robotMovement.direction) : Facing.Frente;

        // Detecta mudança de estado (idle <-> caminhada) ou de direção: reseta a
        // animação pra começar do primeiro frame.
        bool stateChanged = !facingApplied || facing != currentFacing || walking != wasWalking;
        if (stateChanged) {
            currentFacing = facing;
            facingApplied = true;
            wasWalking = walking;
            frameTimer = 0f;
            currentFrameIndex = 0;
        }

        // Avança a animação enquanto anda.
        if (walking) {
            frameTimer += Time.deltaTime;
            Sprite[] frames = FramesFor(currentFacing);
            if (frames != null && frames.Length > 0 && framesPerSecond > 0f) {
                currentFrameIndex = (int)(frameTimer * framesPerSecond) % frames.Length;
            }
        } else {
            currentFrameIndex = 0;
        }

        ApplyRobotSprite();
        ApplyToolSprite();
    }

    private SpriteRenderer FindRenderer(string childName) {
        foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true)) {
            if (spriteRenderer.gameObject.name == childName) return spriteRenderer;
        }
        return null;
    }

    private void OnRobotInventoryChanged(InventoryManager inventory) {
        RefreshTool();
    }

    // Direção do movimento -> facing visual. Baixo = frente (virado pro jogador),
    // cima = costas; os lados têm sprite próprio (o robô não é espelhado).
    private static Facing ToFacing(Vector2Int direction) {
        if (direction == Vector2Int.up) return Facing.Tras;
        if (direction == Vector2Int.down) return Facing.Frente;
        if (direction == Vector2Int.left) return Facing.Esquerda;
        if (direction == Vector2Int.right) return Facing.Direita;
        return Facing.Frente; // direção zero/inválida: fica de frente
    }

    private Sprite[] FramesFor(Facing facing) {
        if (facing == Facing.Frente) return framesFrente;
        if (facing == Facing.Tras) return framesTras;
        if (facing == Facing.Esquerda) return framesEsquerda;
        return framesDireita;
    }

    private void ApplyRobotSprite() {
        if (robotSpriteRenderer == null) return;

        Sprite sprite = null;
        Sprite[] frames = FramesFor(currentFacing);
        if (frames != null && frames.Length > 0) {
            int idx = Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);
            sprite = frames[idx];
        } else {
            if (currentFacing == Facing.Frente) sprite = spriteFrente;
            else if (currentFacing == Facing.Tras) sprite = spriteTras;
            else if (currentFacing == Facing.Esquerda) sprite = spriteEsquerda;
            else sprite = spriteDireita;
        }

        if (sprite == null) {
            // Sprites ainda não ligados no Inspector: mantém o sprite atual e avisa uma vez.
            if (!warnedRobotSprites) {
                warnedRobotSprites = true;
                Debug.LogWarning("[RobotSpriteController] Sprites do robô não configurados: mantém o sprite atual.", this);
            }
            return;
        }

        robotSpriteRenderer.sprite = sprite;
        robotSpriteRenderer.flipX = false; // o robô tem sprite próprio pra cada lado
    }

    private void ApplyToolSprite() {
        if (toolSpriteRenderer == null) return;

        // Sem ferramenta equipada (ou ToolData não resolvido): esconde o sprite.
        if (currentToolId < 0 || currentTool == null) {
            toolSpriteRenderer.sprite = null;
            return;
        }

        Sprite sprite = null;
        if (currentFacing == Facing.Frente) sprite = currentTool.spriteFrente;
        else if (currentFacing == Facing.Tras) sprite = currentTool.spriteTras;
        else sprite = currentTool.spriteLado; // lados (esquerda espelha abaixo)

        // Sprites direcionais ainda não feitos: usa o ícone como fallback.
        if (sprite == null) sprite = currentTool.icon;

        if (sprite == null) {
            if (!warnedToolSprites) {
                warnedToolSprites = true;
                Debug.LogWarning("[RobotSpriteController] ToolData '" + currentTool.displayName + "' sem sprites visuais: ferramenta escondida.", this);
            }
            toolSpriteRenderer.sprite = null;
            return;
        }

        toolSpriteRenderer.sprite = sprite;
        toolSpriteRenderer.flipX = currentFacing == Facing.Esquerda; // lado espelhado
    }

    // Lê o id da ferramenta no slot Tool e resolve o ToolData (só quando muda).
    private void RefreshTool() {
        int toolId = robotInventory != null ? robotInventory.GetToolId() : -1;
        if (toolId == currentToolId) return;

        currentToolId = toolId;
        currentTool = null;

        if (toolId >= 0) {
            var executionManager = ExecutionManager.instancia;
            if (executionManager == null) {
                if (!warnedExecutionManager) {
                    warnedExecutionManager = true;
                    Debug.LogWarning("[RobotSpriteController] ExecutionManager não encontrado: ferramenta " + toolId + " fica sem sprite.", this);
                }
            } else {
                currentTool = executionManager.GetTool(toolId);
            }
        }

        ApplyToolSprite();
    }
}