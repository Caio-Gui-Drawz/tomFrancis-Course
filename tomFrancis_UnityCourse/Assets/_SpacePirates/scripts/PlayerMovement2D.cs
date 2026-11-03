using UnityEngine;

// Movimento vertical do player (W/S ou setas cima/baixo). A mira com o mouse continua igual.
// O botão "Versão com movimento" no Inspector (ou a caixa abaixo) liga/desliga tudo em 1 clique:
//   desligado = player travado como antes; ligado = sobe e desce dentro dos limites da tela.
// Coloque este componente no mesmo objeto do Player2D (o que tem o Rigidbody2D).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Tooltip("Ligado = o player sobe e desce. Desligado = fica parado como na versão original.")]
    [SerializeField] bool verticalMovementEnabled = true;

    [Header("Movimento")]
    [Tooltip("Velocidade máxima vertical (unidades por segundo).")]
    public float maxSpeed = 14f;
    [Tooltip("Quão rápido chega à velocidade máxima (unidades/s²). Maior = resposta mais seca.")]
    public float acceleration = 90f;
    [Tooltip("Quão rápido para ao soltar a tecla (unidades/s²). Maior = para mais rápido.")]
    public float deceleration = 120f;

    [Header("Limites (distância da borda da câmera)")]
    [Tooltip("Folga entre o player e a borda de cima da tela, em unidades do mundo.")]
    public float marginTop = 3f;
    [Tooltip("Folga entre o player e a borda de baixo da tela, em unidades do mundo.")]
    public float marginBottom = 3f;

    [Header("Teste")]
    [Tooltip("Tecla que liga/desliga o movimento durante o Play (só no Editor e em builds de desenvolvimento).")]
    public KeyCode debugToggleKey = KeyCode.F3;

    public bool VerticalMovementEnabled
    {
        get { return verticalMovementEnabled; }
        set { verticalMovementEnabled = value; ApplyMode(); }
    }

    Rigidbody2D body;
    RigidbodyConstraints2D originalConstraints;
    float originalGravityScale;
    Vector3 startPosition;
    bool playing;
    float currentVelocityY;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        originalConstraints = body.constraints;
        originalGravityScale = body.gravityScale;
        startPosition = transform.position;
    }

    void OnEnable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
        ApplyMode();
    }

    void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        if (body != null)
        {
            body.constraints = originalConstraints;
            body.gravityScale = originalGravityScale;
        }
    }

    void OnValidate()
    {
        // Mexeu na caixa no Inspector durante o Play: aplica na hora.
        if (Application.isPlaying && body != null) ApplyMode();
    }

    void HandleStateChanged(GameState newState)
    {
        playing = (newState == GameState.Playing);
        if (!playing)
        {
            currentVelocityY = 0f;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        if (newState == GameState.Menu) ReturnToStart();
    }

    void ReturnToStart()
    {
        if (body == null) return;
        body.position = new Vector2(body.position.x, startPosition.y);
        transform.position = new Vector3(transform.position.x, startPosition.y, transform.position.z);
    }

    // Liga: libera o eixo Y (X continua travado) e tira a gravidade. Desliga: volta ao original.
    void ApplyMode()
    {
        if (body == null) return;

        if (verticalMovementEnabled)
        {
            body.constraints = (originalConstraints & ~RigidbodyConstraints2D.FreezePositionY) | RigidbodyConstraints2D.FreezePositionX;
            body.gravityScale = 0f;
        }
        else
        {
            body.constraints = originalConstraints;
            body.gravityScale = originalGravityScale;
            currentVelocityY = 0f;
            body.linearVelocity = Vector2.zero;
            ReturnToStart(); // volta à altura original: a versão "parada" é sempre igual à de antes
        }
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugToggleKey != KeyCode.None && Input.GetKeyDown(debugToggleKey))
        {
            VerticalMovementEnabled = !verticalMovementEnabled;
            Debug.Log("[PlayerMovement2D] Movimento vertical: " + (verticalMovementEnabled ? "LIGADO" : "DESLIGADO"));
        }
#endif
    }

    void FixedUpdate()
    {
        if (!verticalMovementEnabled) return;

        float input = playing ? Input.GetAxisRaw("Vertical") : 0f; // W/S e setas
        float target = input * maxSpeed;
        float rate = Mathf.Abs(target) > Mathf.Abs(currentVelocityY) ? acceleration : deceleration;
        currentVelocityY = Mathf.MoveTowards(currentVelocityY, target, rate * Time.fixedDeltaTime);

        // Limites: bordas verticais da câmera ortográfica menos as folgas.
        Camera cam = Camera.main;
        float minY = float.NegativeInfinity;
        float maxY = float.PositiveInfinity;
        if (cam != null && cam.orthographic)
        {
            minY = cam.transform.position.y - cam.orthographicSize + marginBottom;
            maxY = cam.transform.position.y + cam.orthographicSize - marginTop;
        }

        float y = body.position.y;
        if ((y <= minY && currentVelocityY < 0f) || (y >= maxY && currentVelocityY > 0f))
        {
            currentVelocityY = 0f;
            y = Mathf.Clamp(y, minY, maxY);
            body.position = new Vector2(body.position.x, y);
        }

        body.linearVelocity = new Vector2(0f, currentVelocityY);
    }
}
