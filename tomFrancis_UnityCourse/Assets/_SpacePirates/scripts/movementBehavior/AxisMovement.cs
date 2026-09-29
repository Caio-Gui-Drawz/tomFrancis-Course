using UnityEngine;


public class AxisMovement : MonoBehaviour
{
    public enum Direction { Right, Left, Up, Down }

    public Direction direction;
    public float speed;

    void OnEnable()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = GetDirectionVector() * speed;
    }

    Vector2 GetDirectionVector()
    {
        switch (direction)
        {
            case Direction.Right: return Vector2.right;
            case Direction.Left: return Vector2.left;
            case Direction.Up: return Vector2.up;
            case Direction.Down: return Vector2.down;
        }
        return Vector2.zero;
    }
}