using UnityEngine;

public class bullet2D : MonoBehaviour
{
    public float bulletSpeed;
    public float damage;
    public float secondsUntilDestroy;

    void Start()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * bulletSpeed;
    }

    void Update()
    {
        secondsUntilDestroy -= Time.deltaTime;

        if (secondsUntilDestroy < 1)
        {
            transform.localScale *= secondsUntilDestroy;
        }

        if (secondsUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }
    }

    // Cobre os dois casos: collider do alvo configurado como Trigger, ou como colisor solido normal.
    void OnTriggerEnter2D(Collider2D other) => HandleHit(other.gameObject);
    void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.gameObject);

    void HandleHit(GameObject other)
    {
        healthSystem theirHealthSystem = other.GetComponent<healthSystem>();
        if (theirHealthSystem != null)
        {
            theirHealthSystem.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}