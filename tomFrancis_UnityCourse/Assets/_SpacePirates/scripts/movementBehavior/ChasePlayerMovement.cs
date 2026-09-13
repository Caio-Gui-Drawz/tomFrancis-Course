using UnityEngine;

// Persegue o player continuamente, recalculando a direção a cada frame.
// Reutilizável em qualquer spawnável que deva perseguir o player (não só inimigo).
// Depende de references.thePlayer estar preenchido (ver Player2D.Start()).
public class ChasePlayerMovement : MonoBehaviour
{
    public float speed;
    private Vector3 offset;
    public Vector2 offsetRange;

    void OnEnable()
    {
        offset = new Vector3(Random.Range(offsetRange.x, offsetRange.y), 0, Random.Range(offsetRange.x, offsetRange.y));
    }
    void Update()
    {
        if (references.thePlayer == null) return;

        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        Vector2 vectorToPlayer = references.thePlayer.transform.position + offset - transform.position;
        ourRigidbody.linearVelocity = vectorToPlayer.normalized * speed;
    }
}
