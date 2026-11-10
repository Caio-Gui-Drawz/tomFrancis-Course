using UnityEngine;

// Dá um impulso de "explosão" a um inimigo SEM tirar o controle do movimento dele:
//   velocidade final = velocidade do movimento normal (perseguir o player, etc.) + impulso que vai sumindo pela curva.
// Como o movimento nunca é pausado, o inimigo não para nem gira no lugar: a direção vai curvando sozinha da saída
// para o alvo conforme o impulso some, e a velocidade cai de forma contínua até a velocidade normal dele.
// O FaceVelocity vê a velocidade já com o impulso, então o visual já nasce virado para a direção de saída e gira
// junto, no mesmo ritmo suave de sempre.
// É adicionado sozinho pelo SpawnOnDeath nos filhos; não precisa colocar à mão.
[DefaultExecutionOrder(-100)] // o LateUpdate daqui roda antes do FaceVelocity
[RequireComponent(typeof(Rigidbody2D))]
public class ExplosionBurst : MonoBehaviour
{
    Rigidbody2D body;
    AnimationCurve curve;
    Vector2 direction;
    float speed;
    float duration;
    float elapsed;
    bool active;

    // Velocidade que o movimento normal quer (lida sempre que ele reescreve a do Rigidbody) e a última que NÓS escrevemos.
    Vector2 movementVelocity;
    Vector2 lastWritten;

    public static void Launch(GameObject target, Vector2 direction, float speed, float seconds, AnimationCurve curve)
    {
        if (target == null || direction.sqrMagnitude < 0.0001f || speed <= 0f || seconds <= 0f) return;
        if (target.GetComponent<Rigidbody2D>() == null) return;

        ExplosionBurst burst = target.GetComponent<ExplosionBurst>();
        if (burst == null) burst = target.AddComponent<ExplosionBurst>();
        burst.Begin(direction.normalized, speed, seconds, curve);
    }

    void Begin(Vector2 launchDirection, float launchSpeed, float seconds, AnimationCurve launchCurve)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();

        direction = launchDirection;
        speed = launchSpeed;
        duration = seconds;
        curve = launchCurve;
        elapsed = 0f;
        active = true;

        // O movimento (Straight, Axis...) pode já ter escrito a velocidade dele no OnEnable.
        movementVelocity = body.linearVelocity;
        Apply(0f);
    }

    float Evaluate(float t)
    {
        return curve != null && curve.length > 0 ? Mathf.Max(0f, curve.Evaluate(t)) : 1f - t;
    }

    // Se a velocidade do Rigidbody não é a que escrevemos, o movimento normal reescreveu: é a nova velocidade dele.
    void CaptureMovement()
    {
        Vector2 current = body.linearVelocity;
        if ((current - lastWritten).sqrMagnitude > 0.0001f) movementVelocity = current;
    }

    void Apply(float t)
    {
        lastWritten = movementVelocity + direction * speed * Evaluate(t);
        body.linearVelocity = lastWritten;
    }

    void FixedUpdate()
    {
        if (!active) return;

        CaptureMovement();
        elapsed += Time.fixedDeltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            // Fim: só a velocidade do próprio movimento.
            lastWritten = movementVelocity;
            body.linearVelocity = movementVelocity;
            active = false;
            return;
        }

        Apply(t);
    }

    // O movimento (ex.: Chase) reescreve a velocidade no Update. Antes do FaceVelocity olhar, recolocamos o impulso,
    // para o visual acompanhar a direção real do movimento.
    void LateUpdate()
    {
        if (!active) return;

        CaptureMovement();
        Apply(Mathf.Clamp01(elapsed / duration));
    }

    void OnDisable()
    {
        active = false;
    }
}
