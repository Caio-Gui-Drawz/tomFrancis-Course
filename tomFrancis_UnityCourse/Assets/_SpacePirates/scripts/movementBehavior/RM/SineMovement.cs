using UnityEngine;

public class SineMovement : MonoBehaviour, IMovementBehavior
{
    public float forwardSpeed = 3f; 
    public float amplitude = 2f;   
    public float frequency = 2f;    

    [Header("Variedade no spawn")]
    [Tooltip("Ligado = cada instância sorteia se começa a onda indo PARA CIMA ou PARA BAIXO (50/50). Desligado = sempre para cima, como antes.")]
    public bool randomizeDirection = false;

    [Tooltip("Ligado = cada instância começa em um ponto aleatório da onda (a posição de spawn continua a mesma, só muda a fase: pode sair subindo, descendo, ou perto de um topo). Se também estiver ligado 'Randomize Direction', ele só espelha a onda.")]
    public bool randomizePhase = false;

    // 1 = normal. A fase de loot reduz isso para os itens andarem mais devagar (sem parar).
    [HideInInspector] public float timeScale = 1f;

    Vector3 origin;
    float elapsedTime;
    float direction = 1f;
    float phase;
    float phaseOffsetY;

    void OnEnable()
    {
        origin = transform.position;
        elapsedTime = 0f;
        timeScale = 1f;

        direction = randomizeDirection && Random.value < 0.5f ? -1f : 1f;
        phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        phaseOffsetY = Mathf.Sin(phase); // desconta a altura inicial para a instância nascer exatamente no ponto de spawn
    }

    void Update()
    {
        elapsedTime += Time.deltaTime * timeScale;

       
        float x = origin.x - forwardSpeed * elapsedTime;

        
        float y = origin.y + direction * amplitude * (Mathf.Sin(frequency * elapsedTime + phase) - phaseOffsetY);

        transform.position = new Vector3(x, y, origin.z);
    }
}
