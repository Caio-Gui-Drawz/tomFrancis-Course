using UnityEngine;

// Mantém o objeto DENTRO da tela no eixo vertical: se encostar na borda de cima ou de baixo da câmera,
// a velocidade vertical é invertida (quica) e ele volta para dentro. Esquerda/direita ficam livres, então a
// caixa ainda pode passar reto e sair por trás. Feito para as caixas de arma (lob + spawnpoint com Sine).
//
// Coloque no prefab (precisa de Rigidbody2D). Funciona com o arco pela gravidade do LobAtPlayerMovement.
// Com o LobAtPlayerMovement em modo "Use Speed Curve", é o próprio Lob que usa este componente: ele dobra
// (espelha) a altura do arco nas bordas, via FoldY, e o quique por velocidade abaixo fica de fora.
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(100)]
public class ScreenEdgeBounce : MonoBehaviour
{
    [Tooltip("Distância (unidades) que o CENTRO do objeto mantém da borda, além da metade da altura dele (calculada sozinha pelo collider/sprite).")]
    public float edgePadding = 0.2f;

    [Tooltip("Quanto da velocidade vertical sobra depois do quique (1 = quica igual, 0.5 = metade).")]
    [Range(0f, 1.5f)] public float bounciness = 0.9f;

    [Tooltip("Velocidade vertical mínima (unidades/s) logo após o quique, para a caixa não 'grudar' na borda.")]
    [Min(0f)] public float minBounceSpeed = 1.5f;

    [Tooltip("Ligado = só começa a valer depois que o objeto já esteve dentro da faixa vertical da tela (não puxa quem nasce fora). Desligado = vale desde o nascimento.")]
    public bool onlyAfterInside = true;

    [Tooltip("Suaviza o movimento da câmera usado para achar as bordas (s). Evita que o tremor da câmera (impulse/shake) e o seguir do player façam a borda — e a caixa junto — tremer. 0 = segue a câmera crua.")]
    [Min(0f)] public float rangeSmoothTime = 0.3f;

    Rigidbody2D body;
    Collider2D col;
    Renderer[] renderers;
    bool wasInside;
    LobAtPlayerMovement lob;

    // Faixa vertical estável: centro suavizado da câmera + altura do objeto medida UMA vez.
    float smoothCenterY;
    float centerVelocity;
    float lastRangeStep = -1f;
    bool rangeReady;
    float cachedHalfHeight = -1f;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        col = GetComponentInChildren<Collider2D>();
        lob = GetComponent<LobAtPlayerMovement>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnEnable()
    {
        wasInside = false;
        rangeReady = false;
        cachedHalfHeight = -1f; // objeto do pool: mede de novo no próximo uso
    }

    void FixedUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float top, bottom;
        if (!TryGetRange(cam, out bottom, out top)) return;

        Vector2 pos = body.position;
        bool inside = pos.y <= top && pos.y >= bottom;
        if (inside) wasInside = true;
        if (onlyAfterInside && !wasInside) return;

        // O Lob com Speed Curve conduz a posição sozinho (e já dobra o arco via FoldY): não mexe na velocidade.
        if (lob != null && lob.IsControlling) return;

        Vector2 vel = body.linearVelocity;

        if (pos.y > top)
        {
            pos.y = top;
            vel.y = -Mathf.Max(Mathf.Abs(vel.y) * bounciness, minBounceSpeed);
            Apply(pos, vel);
        }
        else if (pos.y < bottom)
        {
            pos.y = bottom;
            vel.y = Mathf.Max(Mathf.Abs(vel.y) * bounciness, minBounceSpeed);
            Apply(pos, vel);
        }
    }

    void Apply(Vector2 pos, Vector2 vel)
    {
        body.position = pos;
        body.linearVelocity = vel;
    }

    bool TryGetRange(Camera cam, out float bottom, out float top)
    {
        // A altura do objeto é medida uma vez: o collider gira junto com a caixa e o AABB dele muda a cada frame,
        // o que faria as bordas "respirarem" e a caixa tremer perto delas.
        if (cachedHalfHeight < 0f)
        {
            float measured = HalfHeight();
            if (measured > 0.001f) cachedHalfHeight = measured;
        }

        float camY = cam.transform.position.y;
        if (!rangeReady || rangeSmoothTime <= 0f)
        {
            smoothCenterY = camY;
            centerVelocity = 0f;
            rangeReady = true;
            lastRangeStep = Time.fixedTime;
        }
        else if (!Mathf.Approximately(lastRangeStep, Time.fixedTime))
        {
            // Um passo por tick de física (o Lob e este componente consultam no mesmo tick).
            smoothCenterY = Mathf.SmoothDamp(smoothCenterY, camY, ref centerVelocity, rangeSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
            lastRangeStep = Time.fixedTime;
        }

        float half = Mathf.Max(0f, cachedHalfHeight) + edgePadding;
        top = smoothCenterY + cam.orthographicSize - half;
        bottom = smoothCenterY - cam.orthographicSize + half;
        return top > bottom;
    }

    // Dobra uma altura (mundo) para dentro da faixa vertical da tela, como um espelho nas duas bordas.
    // Dentro da faixa devolve a própria altura. Usado pelo LobAtPlayerMovement.
    public float FoldY(float y)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return y;

        float bottom, top;
        if (!TryGetRange(cam, out bottom, out top)) return y;
        if (onlyAfterInside && !wasInside) return y;

        float length = top - bottom;
        float t = Mathf.Repeat(y - bottom, 2f * length);
        if (t > length) t = 2f * length - t;
        return bottom + t;
    }

    float HalfHeight()
    {
        if (col != null) return col.bounds.extents.y;

        float h = 0f;
        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) h = b.extents.y;
        return h;
    }
}
