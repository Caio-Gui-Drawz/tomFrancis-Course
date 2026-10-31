using UnityEngine;

public class bullet2D : MonoBehaviour
{
    public float bulletSpeed;
    public float damage;
    public float secondsUntilDestroy; // tempo de vida configurado no Inspector

    [Header("Knockback (0 = esta bala não empurra)")]
    [Tooltip("Velocidade do empurrão no inimigo, na direção em que a bala vai. Só funciona em quem tem KnockbackReceiver.")]
    public float knockbackForce = 0f;
    [Tooltip("Quanto tempo o empurrão dura (a velocidade vai caindo até zero).")]
    public float knockbackDuration = 0.2f;

    float secondsRemaining;
    Vector3 initialScale;

    void Awake()
    {
        initialScale = transform.localScale;
    }

    void OnEnable()
    {
        secondsRemaining = secondsUntilDestroy;
        transform.localScale = initialScale;

        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * bulletSpeed;
    }

    void Update()
    {
        secondsRemaining -= Time.deltaTime;

        if (secondsRemaining < 1)
        {
            transform.localScale = initialScale * Mathf.Max(secondsRemaining, 0);
        }

        if (secondsRemaining <= 0)
        {
            PoolManager.Instance.Return(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other) => HandleHit(other.gameObject);
    void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.gameObject);

    void HandleHit(GameObject other)
    {
        healthSystem theirHealthSystem = other.GetComponent<healthSystem>();
        if (theirHealthSystem != null)
        {
            // Direção do tiro (lida antes de devolver a bala ao pool).
            Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
            Vector2 hitDirection = ourRigidbody != null && ourRigidbody.linearVelocity.sqrMagnitude > 0.0001f
                ? ourRigidbody.linearVelocity.normalized
                : (Vector2)transform.right;

            theirHealthSystem.TakeDamage(damage);

            if (knockbackForce > 0f && theirHealthSystem.currentHealth > 0f)
            {
                KnockbackReceiver knockback = other.GetComponent<KnockbackReceiver>();
                if (knockback != null) knockback.Apply(hitDirection, knockbackForce, knockbackDuration);
            }

            PoolManager.Instance.Return(gameObject);
        }
    }
}
