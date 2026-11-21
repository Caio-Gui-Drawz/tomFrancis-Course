using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;

// Colocar no mesmo objeto que tem o healthSystem do player.
// Ao morrer: desliga o combate e o movimento, toca a animação de morte (via PlayerBodyAnimations, no filho do
// Spine), fica parado na pose final por um instante, CAI para fora da tela e só então manda o jogo pro Game Over.
public class PlayerDeath : MonoBehaviour
{
    [Tooltip("Tempo (segundos) parado na pose final da morte antes do Game Over. Só vale com 'Fall Off Screen' DESLIGADO (com a queda ligada quem manda é o 'Pause After Fall').")]
    public float holdAfterDeathAnimation = 0.6f;

    [Header("Queda para fora da tela")]
    [Tooltip("Desligado = comportamento antigo: o boneco fica na pose final, sem cair.")]
    public bool fallOffScreen = true;
    [Tooltip("Segundos depois do início da animação de morte em que ele COMEÇA a cair. 0 = cai assim que a animação dá play (ela continua tocando durante a queda).")]
    [Min(0f)] public float fallStartDelay = 0f;
    [Tooltip("Velocidade inicial da queda (unidades/s).")]
    [Min(0f)] public float initialFallSpeed = 2f;
    [Tooltip("Aceleração da queda (unidades/s²). Maior = cai mais 'pesado'.")]
    [Min(0f)] public float fallAcceleration = 35f;
    [Tooltip("Velocidade máxima da queda (unidades/s).")]
    [Min(0.1f)] public float maxFallSpeed = 40f;
    [Tooltip("Giro durante a queda (graus/s). 0 = cai sem girar; valores negativos giram pro outro lado.")]
    public float spinDegreesPerSecond = 0f;
    [Tooltip("Folga (unidades) abaixo da borda inferior da tela: o boneco só conta como 'fora' depois que TODO ele passar dessa linha.")]
    [Min(0f)] public float offscreenMargin = 1f;
    [Tooltip("Segurança: se ainda não saiu da tela depois deste tempo (s), segue para o Game Over mesmo assim.")]
    [Min(0.5f)] public float maxFallSeconds = 4f;
    [Tooltip("Pausa (s) depois que ele saiu da tela, antes do Game Over.")]
    [Min(0f)] public float pauseAfterFall = 0.4f;

    [Header("Câmera e colisão durante a queda")]
    [Tooltip("Ligado = a câmera para de seguir o player na morte, senão ela desce junto e ele demora a sair da tela.")]
    public bool freezeCameraOnDeath = true;
    [Tooltip("Ligado = desliga os colisores do player na queda, pra inimigos e caixas não esbarrarem no corpo.")]
    public bool disableCollidersOnFall = true;

    healthSystem health;
    Player2D player;
    PlayerBodyAnimations bodyAnimations;

    void Start()
    {
        health = GetComponent<healthSystem>();
        player = GetComponent<Player2D>();
        bodyAnimations = GetComponentInChildren<PlayerBodyAnimations>();

        // O player não pode ser removido/destruído ao morrer, senão a animação nunca aparece.
        health.removeOnDeath = false;
        health.soundProfile = SoundProfile.Player; // sons de dano/morte do player (via AudioEvents)
        health.OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        if (player != null)
        {
            player.SetCombatStance(false); // solta a mira/braço, o corpo todo vira a animação de morte
            player.enabled = false;        // para de atirar durante a morte
        }

        // Para de voar: sem isso o corpo continuaria obedecendo as teclas durante a morte.
        PlayerMovement2D movement = GetComponent<PlayerMovement2D>();
        if (movement != null) movement.enabled = false;

        if (freezeCameraOnDeath) FreezeCamera();

        float animDuration = bodyAnimations != null ? bodyAnimations.PlayDeath() : 0f;
        float startTime = Time.unscaledTime;

        if (fallOffScreen)
        {
            // A queda roda JUNTO com a animação: começa 'fallStartDelay' segundos depois dela dar play.
            if (fallStartDelay > 0f) yield return new WaitForSecondsRealtime(fallStartDelay);
            yield return FallOffScreen();

            // Se ele saiu da tela antes da animação acabar, espera ela terminar.
            float remaining = animDuration - (Time.unscaledTime - startTime);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            if (pauseAfterFall > 0f) yield return new WaitForSecondsRealtime(pauseAfterFall);
        }
        else
        {
            yield return new WaitForSecondsRealtime(animDuration + holdAfterDeathAnimation);
        }

        GameManager.Instance.SetState(GameState.GameOver);
    }

    IEnumerator FallOffScreen()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false; // a queda é movida à mão; a física não deve brigar com ela
        }

        if (disableCollidersOnFall)
        {
            foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float speed = initialFallSpeed;
        float elapsed = 0f;

        while (elapsed < maxFallSeconds && !IsBelowScreen(renderers))
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            speed = Mathf.Min(maxFallSpeed, speed + fallAcceleration * dt);

            transform.position += Vector3.down * (speed * dt);
            if (spinDegreesPerSecond != 0f) transform.Rotate(0f, 0f, spinDegreesPerSecond * dt);
            yield return null;
        }
    }

    // true quando TODO o corpo (todos os renderers) já passou da borda inferior da câmera + folga.
    bool IsBelowScreen(Renderer[] renderers)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        float bottom = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, Mathf.Abs(cam.transform.position.z))).y - offscreenMargin;

        bool any = false;
        float top = float.NegativeInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled) continue;
            top = Mathf.Max(top, renderers[i].bounds.max.y);
            any = true;
        }
        if (!any) top = transform.position.y;

        return top < bottom;
    }

    // Faz a câmera ficar onde está. O alvo da câmera costuma ter um PositionConstraint seguindo o player;
    // se não tiver, solta o "Follow" da CinemachineCamera.
    void FreezeCamera()
    {
        CinemachineCamera cm = FindFirstObjectByType<CinemachineCamera>();
        if (cm == null || cm.Follow == null) return;

        PositionConstraint constraint = cm.Follow.GetComponent<PositionConstraint>();
        if (constraint != null && cm.Follow != transform)
        {
            constraint.constraintActive = false;
        }
        else
        {
            cm.Follow = null;
        }
    }
}
