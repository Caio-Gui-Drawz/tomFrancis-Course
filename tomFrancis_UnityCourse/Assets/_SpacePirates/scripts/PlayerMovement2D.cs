using UnityEngine;

// Movimento do player: W/A/S/D ou setas. A mira com o mouse continua igual.
// Dois interruptores no Inspector (e teclas de teste):
//   Vertical Movement Enabled   = liga/desliga TODO o movimento (desligado = player parado como na versão original).
//   Horizontal Movement Enabled = liga/desliga só o lado a lado (desligado = só sobe e desce, como antes).
// Coloque este componente no mesmo objeto do Player2D (o que tem o Rigidbody2D).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [Tooltip("Ligado = o player se move (sobe e desce). Desligado = fica parado como na versão original.")]
    [SerializeField] bool verticalMovementEnabled = true;

    [Tooltip("Ligado = também anda para frente e para trás (A/D ou setas). Desligado = só movimento vertical, como antes. Só vale com o movimento ligado.")]
    [SerializeField] bool horizontalMovementEnabled = true;

    [Header("Movimento")]
    [Tooltip("Velocidade máxima vertical (unidades por segundo).")]
    public float maxSpeed = 14f;
    [Tooltip("Velocidade máxima horizontal (unidades por segundo).")]
    public float maxSpeedX = 10f;
    [Tooltip("Quão rápido chega à velocidade máxima (unidades/s²). Maior = resposta mais seca.")]
    public float acceleration = 90f;
    [Tooltip("Quão rápido para ao soltar a tecla (unidades/s²). Maior = para mais rápido.")]
    public float deceleration = 120f;

    [Header("Limites (distância da borda da câmera)")]
    [Tooltip("Folga entre o player e a borda de cima da tela, em unidades do mundo.")]
    public float marginTop = 3f;
    [Tooltip("Folga entre o player e a borda de baixo da tela, em unidades do mundo.")]
    public float marginBottom = 3f;
    [Tooltip("Folga entre o player e a borda ESQUERDA da tela, em unidades do mundo.")]
    public float marginLeft = 1.5f;
    [Tooltip("Folga entre o player e a borda DIREITA da tela, em unidades do mundo. Maior = o player não avança tanto para a direita.")]
    public float marginRight = 6f;

    [Header("Teste")]
    [Tooltip("Tecla que liga/desliga o movimento durante o Play (só no Editor e em builds de desenvolvimento).")]
    public KeyCode debugToggleKey = KeyCode.F3;
    [Tooltip("Tecla que liga/desliga só o movimento horizontal durante o Play (Editor e builds de desenvolvimento).")]
    public KeyCode debugToggleHorizontalKey = KeyCode.F4;

    public bool VerticalMovementEnabled
    {
        get { return verticalMovementEnabled; }
        set { verticalMovementEnabled = value; ApplyMode(); }
    }

    public bool HorizontalMovementEnabled
    {
        get { return horizontalMovementEnabled; }
        set { horizontalMovementEnabled = value; ApplyMode(); }
    }

    // Velocidade horizontal atual, de -1 (para trás na velocidade máxima) a +1 (para frente).
    public float NormalizedVelocityX
    {
        get { return (verticalMovementEnabled && horizontalMovementEnabled && maxSpeedX > 0f) ? Mathf.Clamp(currentVelocityX / maxSpeedX, -1f, 1f) : 0f; }
    }

    // Velocidade vertical atual, de -1 (descendo na velocidade máxima) a +1 (subindo). Usada pelas animações.
    public float NormalizedVelocityY
    {
        get { return (verticalMovementEnabled && maxSpeed > 0f) ? Mathf.Clamp(currentVelocityY / maxSpeed, -1f, 1f) : 0f; }
    }

    Rigidbody2D body;
    RigidbodyConstraints2D originalConstraints;
    float originalGravityScale;
    Vector3 startPosition;
    bool playing;
    float currentVelocityY;
    float currentVelocityX;

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
            currentVelocityX = 0f;
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        if (newState == GameState.Menu) ReturnToStart();
    }

    void ReturnToStart()
    {
        if (body == null) return;
        body.position = new Vector2(startPosition.x, startPosition.y);
        transform.position = new Vector3(startPosition.x, startPosition.y, transform.position.z);
    }

    // Liga: libera o eixo Y (e o X se o horizontal estiver ligado) e tira a gravidade. Desliga: volta ao original.
    void ApplyMode()
    {
        if (body == null) return;

        if (verticalMovementEnabled)
        {
            RigidbodyConstraints2D c = originalConstraints & ~RigidbodyConstraints2D.FreezePositionY;
            if (horizontalMovementEnabled)
            {
                c &= ~RigidbodyConstraints2D.FreezePositionX;
            }
            else
            {
                c |= RigidbodyConstraints2D.FreezePositionX;
                currentVelocityX = 0f;
                // Voltou ao modo só-vertical: volta para a posição horizontal original.
                body.position = new Vector2(startPosition.x, body.position.y);
                transform.position = new Vector3(startPosition.x, transform.position.y, transform.position.z);
            }
            body.constraints = c;
            body.gravityScale = 0f;
        }
        else
        {
            body.constraints = originalConstraints;
            body.gravityScale = originalGravityScale;
            currentVelocityY = 0f;
            currentVelocityX = 0f;
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
            Debug.Log("[PlayerMovement2D] Movimento: " + (verticalMovementEnabled ? "LIGADO" : "DESLIGADO"));
        }
        if (debugToggleHorizontalKey != KeyCode.None && Input.GetKeyDown(debugToggleHorizontalKey))
        {
            HorizontalMovementEnabled = !horizontalMovementEnabled;
            Debug.Log("[PlayerMovement2D] Movimento horizontal: " + (horizontalMovementEnabled ? "LIGADO" : "DESLIGADO"));
        }
#endif
    }

    void FixedUpdate()
    {
        if (!verticalMovementEnabled) return;

        float inputX = (playing && horizontalMovementEnabled) ? TouchInput.Horizontal : 0f; // A/D e setas
        float inputY = playing ? TouchInput.Vertical : 0f;                                  // W/S, setas e toque
        Vector2 input = Vector2.ClampMagnitude(new Vector2(inputX, inputY), 1f);             // diagonal não é mais rápida

        float targetY = input.y * maxSpeed;
        float rateY = Mathf.Abs(targetY) > Mathf.Abs(currentVelocityY) ? acceleration : deceleration;
        currentVelocityY = Mathf.MoveTowards(currentVelocityY, targetY, rateY * Time.fixedDeltaTime);

        float targetX = input.x * maxSpeedX;
        float rateX = Mathf.Abs(targetX) > Mathf.Abs(currentVelocityX) ? acceleration : deceleration;
        currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetX, rateX * Time.fixedDeltaTime);

        // Limites: bordas da câmera ortográfica menos as folgas.
        Camera cam = Camera.main;
        float minY = float.NegativeInfinity;
        float maxY = float.PositiveInfinity;
        float minX = float.NegativeInfinity;
        float maxX = float.PositiveInfinity;
        if (cam != null && cam.orthographic)
        {
            minY = cam.transform.position.y - cam.orthographicSize + marginBottom;
            maxY = cam.transform.position.y + cam.orthographicSize - marginTop;

            float halfWidth = cam.orthographicSize * cam.aspect;
            minX = cam.transform.position.x - halfWidth + marginLeft;
            maxX = cam.transform.position.x + halfWidth - marginRight;
        }

        Vector2 pos = body.position;
        bool clamped = false;

        if ((pos.y <= minY && currentVelocityY < 0f) || (pos.y >= maxY && currentVelocityY > 0f))
        {
            currentVelocityY = 0f;
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            clamped = true;
        }

        if (horizontalMovementEnabled && ((pos.x <= minX && currentVelocityX < 0f) || (pos.x >= maxX && currentVelocityX > 0f)))
        {
            currentVelocityX = 0f;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            clamped = true;
        }

        if (clamped) body.position = pos;

        body.linearVelocity = new Vector2(horizontalMovementEnabled ? currentVelocityX : 0f, currentVelocityY);
    }
}
