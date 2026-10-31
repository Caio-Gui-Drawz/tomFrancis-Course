using System.Collections.Generic;
using UnityEngine;

// Explosão 2D que causa dano em área. Coloque no prefab da explosão (o mesmo que vai no
// "Death Effect Prefab" do healthSystem do inimigo/mina). Fere inimigos E o player.
//
// Como funciona: no primeiro frame depois de nascer, faz UM Physics2D.OverlapCircleAll no raio configurado
// e chama TakeDamage em cada healthSystem encontrado (cada um só leva dano uma vez por explosão).
// Quem já morreu é ignorado pelo healthSystem, então explosões em cadeia não entram em loop.
//
// O visual é opcional: se "Visual" estiver preenchido, ele cresce de 0 até o tamanho do raio durante
// a duração e a explosão se destrói no final (funciona com sprite, partícula ou qualquer objeto).
public class ExplosionDamage2D : MonoBehaviour
{
    [Header("Dano")]
    public float damage = 3f;

    [Tooltip("Raio da explosão em unidades de mundo.")]
    public float radius = 2.5f;

    [Tooltip("Quais layers sofrem dano. Deixe Everything para ferir inimigos e player (quem não tem healthSystem é ignorado).")]
    public LayerMask affectedLayers = ~0;

    [Tooltip("Ligado = quanto mais longe do centro, menos dano.")]
    public bool distanceFalloff = false;

    [Tooltip("Com falloff ligado: fração do dano na borda do raio (0 = nada, 1 = dano total).")]
    [Range(0f, 1f)] public float edgeDamageFraction = 0.3f;

    [Tooltip("Atraso antes de causar o dano, em segundos (0 = no primeiro frame). Útil para escalonar reações em cadeia.")]
    public float damageDelay = 0f;

    [Header("Knockback (0 = não empurra)")]
    [Tooltip("Velocidade do empurrão, para FORA do centro da explosão (então empurra para os lados também). Só afeta quem tem KnockbackReceiver.")]
    public float knockbackForce = 0f;

    [Tooltip("Quanto tempo o empurrão dura.")]
    public float knockbackDuration = 0.35f;

    [Tooltip("Ligado = quem está longe do centro é empurrado com menos força.")]
    public bool knockbackFalloff = true;

    [Tooltip("Com falloff ligado: fração da força na borda do raio.")]
    [Range(0f, 1f)] public float edgeKnockbackFraction = 0.3f;

    [Header("Visual (opcional)")]
    [Tooltip("Objeto que cresce de 0 até o tamanho da explosão. Pode ser este próprio objeto ou um filho.")]
    public Transform visual;

    [Tooltip("Tamanho final do Visual com escala 1 = diâmetro de 1 unidade (sprite de 1x1). Ajuste se o seu sprite for maior.")]
    public float visualBaseDiameter = 1f;

    [Tooltip("Tempo de vida da explosão em segundos.")]
    public float duration = 0.35f;

    [Header("Eventos")]
    [Tooltip("Chamado quando o dano é aplicado (som, screenshake, etc.).")]
    public UnityEngine.Events.UnityEvent onExploded;

    float age;
    bool damageDone;

    void Start()
    {
        if (damageDelay <= 0f) DoDamage();
        if (visual != null) visual.localScale = Vector3.zero;
    }

    void Update()
    {
        age += Time.deltaTime;

        if (!damageDone && age >= damageDelay) DoDamage();

        if (visual != null && duration > 0f)
        {
            float t = Mathf.Clamp01(age / duration);
            float diameter = radius * 2f / Mathf.Max(0.0001f, visualBaseDiameter);
            visual.localScale = Vector3.one * (diameter * t);
        }

        if (age >= Mathf.Max(duration, damageDelay)) Destroy(gameObject);
    }

    void DoDamage()
    {
        damageDone = true;

        Vector2 center = transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, affectedLayers);
        HashSet<healthSystem> alreadyHit = new HashSet<healthSystem>();

        foreach (Collider2D hit in hits)
        {
            healthSystem target = hit.GetComponentInParent<healthSystem>();
            if (target == null || !alreadyHit.Add(target)) continue;

            float amount = damage;
            if (distanceFalloff && radius > 0f)
            {
                float distance01 = Mathf.Clamp01(Vector2.Distance(center, hit.ClosestPoint(center)) / radius);
                amount = damage * Mathf.Lerp(1f, edgeDamageFraction, distance01);
            }

            target.TakeDamage(amount);

            if (knockbackForce > 0f && target.currentHealth > 0f)
            {
                KnockbackReceiver knockback = target.GetComponent<KnockbackReceiver>();
                if (knockback != null)
                {
                    Vector2 away = (Vector2)target.transform.position - center;
                    if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
                    if (away.sqrMagnitude < 0.0001f) away = Vector2.right;

                    float force = knockbackForce;
                    if (knockbackFalloff && radius > 0f)
                    {
                        float d01 = Mathf.Clamp01(away.magnitude / radius);
                        force *= Mathf.Lerp(1f, edgeKnockbackFraction, d01);
                    }

                    knockback.Apply(away.normalized, force, knockbackDuration);
                }
            }
        }

        onExploded?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
