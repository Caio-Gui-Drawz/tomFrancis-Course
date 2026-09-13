using UnityEngine;

// Movimento reto na direção em que o objeto foi ativado (mesma lógica que o enemy2D usava antes).
// Reutilizável em qualquer spawnável: inimigo, tesouro ou upgrade.
public class StraightMovement : MonoBehaviour
{
    public float speed;

    void OnEnable()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * speed;
    }
}
