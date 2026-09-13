using UnityEngine;

public class bullet2D : MonoBehaviour
{
    public float bulletSpeed;
    public float damage;
    public float secondsUntilDestroy; // tempo de vida configurado no Inspector

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
            theirHealthSystem.TakeDamage(damage);
            PoolManager.Instance.Return(gameObject);
        }
    }
}
