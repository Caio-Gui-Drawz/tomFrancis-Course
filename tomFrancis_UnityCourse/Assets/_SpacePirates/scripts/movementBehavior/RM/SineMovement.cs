using UnityEngine;

public class SineMovement : MonoBehaviour
{
    public float forwardSpeed = 3f; 
    public float amplitude = 2f;   
    public float frequency = 2f;    

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

       
        float x = origin.x - forwardSpeed * elapsedTime;

        
        float y = origin.y + amplitude * Mathf.Sin(frequency * elapsedTime);

        transform.position = new Vector3(x, y, origin.z);
    }
}
