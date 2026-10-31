using UnityEngine;

// Arremessa o objeto em um arco que CHEGA no player (ou perto dele). A velocidade inicial é calculada
// com a gravidade do Rigidbody2D, então o arco sempre termina onde mira.
//
// Requisitos no prefab: Rigidbody2D com Gravity Scale > 0 (é ele que faz o arco; com 0 vira reta)
// e Linear Damping = 0 (atrito no ar desmonta a conta).
[RequireComponent(typeof(Rigidbody2D))]
public class LobAtPlayerMovement : MonoBehaviour, IMovementBehavior
{
    public enum ArcMode
    {
        FlightTime,          // você define quanto o voo demora; a altura do arco é consequência
        ApexHeight,          // você define quanto o arco passa ACIMA do ponto mais alto (origem ou alvo)
        ApexAboveScreenTop   // o topo do arco fica "Apex Height" unidades ACIMA da borda de cima da tela
    }

    [Header("Forma do arco")]
    public ArcMode arcMode = ArcMode.ApexAboveScreenTop;

    [Tooltip("Modo FlightTime: segundos até chegar no alvo.")]
    public float flightTime = 1.5f;

    [Tooltip("Modos Apex: ApexHeight = altura acima do ponto mais alto. ApexAboveScreenTop = margem acima da borda de cima da câmera (maior = sobe mais fora da tela).")]
    public float apexHeight = 3f;

    [Header("Mira")]
    [Tooltip("Raio ao redor do player onde o arremesso pode cair. 0 = mira exata na posição do player no momento do arremesso.")]
    public float aimRadius = 1f;

    [Tooltip("Se o player não existir (ex.: morreu), vai pra esse lado em vez de ficar parado.")]
    public Vector2 fallbackDirection = Vector2.left;

    [Header("Aceleração ao longo do caminho")]
    [Tooltip("Desligado = o arco é puramente a gravidade do Rigidbody (como antes). Ligado = a curva abaixo controla a velocidade.")]
    public bool useSpeedCurve = false;

    [Tooltip("Multiplicador de velocidade ao longo do arco. Eixo X: 0 = saída, 1 = chegada no alvo. Eixo Y: 1 = velocidade normal do arco, 2 = o dobro, 0.5 = metade. O formato do caminho não muda, só a velocidade nele.")]
    public AnimationCurve speedCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("Piso do multiplicador. Evita o objeto parar de vez se a curva chegar em 0.")]
    public float minSpeedMultiplier = 0.05f;

    Rigidbody2D body;
    float originalGravityScale;

    // Estado do arco calculado no arremesso.
    Vector2 origin;
    Vector2 launchVelocity;
    Vector2 gravityVector;
    float arcDuration;

    // Estado do controle por curva.
    bool controlling;
    float arcTime; // tempo "do arco" já percorrido (avança mais rápido/devagar conforme a curva)

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        originalGravityScale = body.gravityScale; // guarda o valor do prefab (o controle por curva zera a gravidade)
    }

    void OnEnable()
    {
        origin = transform.position;
        Vector2 target;
        if (references.thePlayer != null)
        {
            target = (Vector2)references.thePlayer.transform.position + Random.insideUnitCircle * aimRadius;
        }
        else
        {
            target = origin + fallbackDirection.normalized * 10f;
        }

        gravityVector = Physics2D.gravity * originalGravityScale;
        ComputeArc(target);

        body.linearVelocity = launchVelocity;
        arcTime = 0f;

        if (useSpeedCurve)
        {
            // Nós mesmos conduzimos o objeto pelo arco, então a gravidade do Rigidbody sai de cena.
            body.gravityScale = 0f;
            controlling = true;
        }
        else
        {
            body.gravityScale = originalGravityScale;
            controlling = false;
        }
    }

    void OnDisable()
    {
        controlling = false;
        if (body != null) body.gravityScale = originalGravityScale;
    }

    void FixedUpdate()
    {
        if (!controlling) return;

        float dt = Time.fixedDeltaTime;
        float progress = arcDuration > 0f ? Mathf.Clamp01(arcTime / arcDuration) : 1f;
        float multiplier = Mathf.Max(minSpeedMultiplier, speedCurve.Evaluate(progress));
        arcTime += dt * multiplier;

        // Chegou no fim do arco: devolve a gravidade e deixa seguir a velocidade natural dali (cai pra fora da tela).
        if (arcTime >= arcDuration)
        {
            controlling = false;
            body.gravityScale = originalGravityScale;
            body.linearVelocity = launchVelocity + gravityVector * arcDuration;
            return;
        }

        // Posição exata do arco naquele instante; a velocidade é escolhida pra chegar lá no próximo passo da física.
        Vector2 nextPosition = origin + launchVelocity * arcTime + 0.5f * gravityVector * arcTime * arcTime;
        body.linearVelocity = (nextPosition - body.position) / dt;
    }

    // Calcula launchVelocity e arcDuration pra sair de "origin" e passar por "target".
    void ComputeArc(Vector2 target)
    {
        // Gravidade efetiva (positiva = puxando pra baixo).
        float g = -gravityVector.y;

        // Sem gravidade não existe arco: cai no modo de tempo de voo (vira uma reta).
        if (arcMode == ArcMode.FlightTime || g <= 0.01f)
        {
            arcDuration = Mathf.Max(0.05f, flightTime);

            // posição(t) = origem + v*t + 0,5*g*t²  ->  isolando v pra posição(tempo) = alvo:
            launchVelocity = (target - origin - 0.5f * gravityVector * arcDuration * arcDuration) / arcDuration;
            return;
        }

        // Altura do topo do arco (apex) em coordenadas de mundo.
        float highestPoint = Mathf.Max(origin.y, target.y);
        float apexY = highestPoint + apexHeight;

        Camera cam = Camera.main;
        if (arcMode == ArcMode.ApexAboveScreenTop && cam != null && cam.orthographic)
        {
            float screenTop = cam.transform.position.y + cam.orthographicSize;
            apexY = screenTop + apexHeight;
        }

        // O topo precisa ficar acima da origem e do alvo, senão a conta não fecha.
        apexY = Mathf.Max(apexY, highestPoint + 0.1f);

        float rise = apexY - origin.y;  // subida até o topo
        float fall = apexY - target.y;  // descida do topo até o alvo

        float vy = Mathf.Sqrt(2f * g * rise);
        float timeUp = vy / g;
        float timeDown = Mathf.Sqrt(2f * fall / g);
        arcDuration = timeUp + timeDown;

        float vx = (target.x - origin.x) / arcDuration;
        launchVelocity = new Vector2(vx, vy);
    }
}
