using UnityEngine;


public class ParabolicMovement : MonoBehaviour
{
    public float initialSpeedX = 4f; 
    public float initialSpeedY = 6f; 
    public float gravity = -9.8f;    

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

      
        float x = origin.x - initialSpeedX * elapsedTime;

        
        float deltaY = initialSpeedY * elapsedTime + 0.5f * gravity * elapsedTime * elapsedTime;
        float y = origin.y + deltaY;

        transform.position = new Vector3(x, y, origin.z);
    }
}
