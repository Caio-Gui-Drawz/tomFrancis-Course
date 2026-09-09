using UnityEngine;

public class enemy2D : MonoBehaviour
{

    public float damage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

     private void OnCollisionEnter2D(Collision2D thisCollision)
    {
         Debug.Log("collision detected");
        GameObject theirGameObject = thisCollision.gameObject; //objeto que bateu no collider da bala.
        if (theirGameObject.GetComponent<Player2D>() != null)
        
        {
        Debug.Log("player hit");
        
        healthSystem theirHealthSystem = theirGameObject.GetComponent<healthSystem>();

        if (theirHealthSystem != null)

        {
            Debug.Log("player health system found");
        theirHealthSystem.TakeDamage(damage); 
        }
        

        }
    }
}
