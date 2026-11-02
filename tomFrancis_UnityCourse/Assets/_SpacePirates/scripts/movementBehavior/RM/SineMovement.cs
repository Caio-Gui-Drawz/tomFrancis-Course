using UnityEngine;

public class SineMovement : MonoBehaviour, IMovementBehavior
{
    public float forwardSpeed = 3f; 
    public float amplitude = 2f;   
    public float frequency = 2f;    

    // 1 = normal. A fase de loot reduz isso para os itens andarem mais devagar (sem parar).
    [HideInInspector] public float timeScale = 1f;

    Vector3 origin;
    float elapsedTime;

    void OnEnable()
    {
        origin = transform.position;
        elapsedTime = 0f;
        timeScale = 1f;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime * timeScale;

       
        float x = origin.x - forwardSpeed * elapsedTime;

        
        float y = origin.y + amplitude * Mathf.Sin(frequency * elapsedTime);

        transform.position = new Vector3(x, y, origin.z);
    }
}
