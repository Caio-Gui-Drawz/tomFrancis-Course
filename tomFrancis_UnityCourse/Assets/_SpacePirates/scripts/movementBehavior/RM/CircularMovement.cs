using UnityEngine;

// Movimento circular composto: o CENTRO da órbita se desloca em linha reta (MRU) enquanto
// o objeto gira ao redor desse centro móvel — a posição final é a SOMA VETORIAL de dois
// movimentos: translação linear do centro + rotação de raio constante ao redor dele.
//
// A velocidade angular NÃO é constante: ela é modulada por uma AnimationCurve, reiniciada
// a cada volta completa (360°) — permitindo ease-in/ease-out por ciclo, editável no Inspector.
public class CircularMovement : MonoBehaviour
{
    public float radius = 2f;
    public float forwardSpeed = 2f;    // velocidade de translação do centro da órbita
    public float lifetimeSeconds = 6f; // tempo até devolver ao pool

    [Header("Curva de aceleração por volta (360°)")]
    public float degreesPerCycle = 360f; // uma volta completa
    public float cycleDuration = 2f;     // tempo para completar UMA volta, na velocidade "cheia"
    // Curva padrão: EaseInOut clássica — começa devagar, acelera no meio, desacelera no fim.
    public AnimationCurve angularSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Vector3 origin;
    float currentAngleDegrees;
    float elapsedTime;
    float secondsRemaining;
    float cycleElapsed;

    void OnEnable()
    {
        origin = transform.position;
        currentAngleDegrees = 0f;
        elapsedTime = 0f;
        secondsRemaining = lifetimeSeconds;
        cycleElapsed = 0f;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        // Centro da órbita avançando em MRU.
        float centerX = origin.x - forwardSpeed * elapsedTime;
        float centerY = origin.y;

        // Progresso normalizado (0 a 1) dentro do ciclo atual de 360°.
        cycleElapsed += Time.deltaTime;
        float cycleProgress = Mathf.Clamp01(cycleElapsed / cycleDuration);

        // A curva modula a velocidade angular INSTANTÂNEA deste frame.
        float baseAngularSpeed = degreesPerCycle / cycleDuration;
        float speedMultiplier = angularSpeedCurve.Evaluate(cycleProgress);
        float instantAngularSpeed = baseAngularSpeed * speedMultiplier;

        currentAngleDegrees += instantAngularSpeed * Time.deltaTime;

        if (cycleElapsed >= cycleDuration)
        {
            cycleElapsed = 0f; // reinicia o ease-in/ease-out para a próxima volta
        }

        // Vetor circular ao redor do centro móvel.
        float angleRadians = currentAngleDegrees * Mathf.Deg2Rad;
        float offsetX = radius * Mathf.Cos(angleRadians);
        float offsetY = radius * Mathf.Sin(angleRadians);
        Debug.Log($"angleRadians: {angleRadians}, offsetX: {offsetX}, offsetY: {offsetY}");

        transform.position = new Vector3(centerX + offsetX, centerY + offsetY, origin.z);

        secondsRemaining -= Time.deltaTime;
        if (secondsRemaining <= 0f)
        {
            PoolManager.Instance.Return(gameObject);
        }
    }
}