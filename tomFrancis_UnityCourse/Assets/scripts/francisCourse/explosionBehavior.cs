using UnityEngine;

public class explosionBehavior : MonoBehaviour
{

    public float secondsToExist;
    float secondsAlive;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        secondsAlive = 0;

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        
        secondsAlive += Time.fixedDeltaTime;

        float lifeFraction = secondsAlive / secondsToExist;
        Vector3 maxScale = Vector3.one * 5; //5 is the maximum size of the explosion
        transform.localScale = Vector3.Lerp(Vector3.zero, maxScale, lifeFraction);

        if (secondsAlive >= secondsToExist)
        {
            Destroy(gameObject);
        }
    }

   
}
