using UnityEngine;

// Movimento reto na direção em que o objeto foi ativado (mesma lógica que o enemy2D usava antes).
// Reutilizável em qualquer spawnável: inimigo, tesouro ou upgrade.
public class StraightMovement : MonoBehaviour
{
    public float speed;

    private Vector2 offset;
    public Vector2 offsetRange;
    public Vector2 direction;

    void OnEnable()
    {

        offset = new Vector2(Random.Range(offsetRange.x, offsetRange.y), Random.Range(offsetRange.x, offsetRange.y));
        Vector2 randomDirection = new Vector3(direction.x + offset.x, direction.y + offset.y, 0);
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = randomDirection.normalized * speed;
    }
}
