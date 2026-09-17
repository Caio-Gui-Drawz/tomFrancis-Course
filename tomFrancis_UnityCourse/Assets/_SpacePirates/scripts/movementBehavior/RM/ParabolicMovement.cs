using UnityEngine;

// Movimento parabólico (MRUV — Movimento Retilíneo Uniformemente Variado):
// velocidade constante no eixo X (MRU) e aceleração constante da gravidade no eixo Y.
// Usa a equação clássica da cinemática: Δy = v0*t + (1/2)*a*t²
public class ParabolicMovement : MonoBehaviour
{
    public float initialSpeedX = 4f; // velocidade horizontal (constante)
    public float initialSpeedY = 6f; // impulso vertical inicial (o "arremesso")
    public float gravity = -9.8f;    // aceleração constante (negativa = puxa para baixo)

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

        // Eixo X: MRU — distância = velocidade * tempo.
        float x = origin.x - initialSpeedX * elapsedTime;

        // Eixo Y: MRUV — equação da cinemática com aceleração constante.
        float deltaY = initialSpeedY * elapsedTime + 0.5f * gravity * elapsedTime * elapsedTime;
        float y = origin.y + deltaY;

        transform.position = new Vector3(x, y, origin.z);
    }
}
