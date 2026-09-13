using UnityEngine;

public class enemy2D : MonoBehaviour
{
    public float damage;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Player2D>() != null)
        {
            healthSystem theirHealthSystem = other.GetComponent<healthSystem>();
            if (theirHealthSystem != null)
            {
                theirHealthSystem.TakeDamage(damage);
            }
        }
    }
    
}