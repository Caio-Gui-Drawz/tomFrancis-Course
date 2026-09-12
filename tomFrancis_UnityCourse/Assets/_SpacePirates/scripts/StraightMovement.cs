using UnityEngine;

// Movimento reto na direção em que o objeto nasceu (mesma lógica que o enemy2D já usa).
// Reutilizável em qualquer spawnável: inimigo, tesouro ou upgrade.
public class StraightMovement : MonoBehaviour
{
    public float speed;

    void Start()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * speed;
    }
}
