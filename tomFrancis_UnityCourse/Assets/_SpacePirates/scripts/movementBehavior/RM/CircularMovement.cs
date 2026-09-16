using UnityEngine;

// Movimento circular (órbita): a posição descreve um círculo ao redor de um centro fixo,
// usando as equações paramétricas do círculo:
// x = centro.x + raio * cos(ângulo)
// y = centro.y + raio * sen(ângulo)
public class CircularMovement : MonoBehaviour
{
    public float radius = 2f;
    public float angularSpeedDegrees = 90f; // graus por segundo

    Vector3 center;
    float currentAngleDegrees;

    void OnEnable()
    {
        center = transform.position; // o ponto de spawn vira o centro da órbita
        currentAngleDegrees = 0f;
    }

    void Update()
    {
        currentAngleDegrees += angularSpeedDegrees * Time.deltaTime;
        float angleRadians = currentAngleDegrees * Mathf.Deg2Rad;

        float x = center.x + radius * Mathf.Cos(angleRadians);
        float y = center.y + radius * Mathf.Sin(angleRadians);

        transform.position = new Vector3(x, y, center.z);
    }
}
