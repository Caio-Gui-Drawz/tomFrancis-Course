using UnityEngine;

// Movimento senoidal: avança em linha reta (MRU) enquanto oscila perpendicularmente
// seguindo a função y = amplitude * sen(frequência * tempo).
// Calculado manualmente (sem física, sem tween) para poder explicar a fórmula na defesa.
public class SineMovement : MonoBehaviour
{
    public float forwardSpeed = 3f; // velocidade constante no eixo de avanço
    public float amplitude = 2f;    // "altura" máxima da onda
    public float frequency = 2f;    // quão rápido oscila (maior = onda mais apertada)

    Vector3 origin;
    float elapsedTime;

    void OnEnable()
    {
        origin = transform.position;
        elapsedTime = 0f;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        // Eixo X: avanço linear comum (MRU).
        float x = origin.x - forwardSpeed * elapsedTime;

        // Eixo Y: deslocamento pela função seno — o coração do movimento senoidal.
        float y = origin.y + amplitude * Mathf.Sin(frequency * elapsedTime);

        transform.position = new Vector3(x, y, origin.z);
    }
}
