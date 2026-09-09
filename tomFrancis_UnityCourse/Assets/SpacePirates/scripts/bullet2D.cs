using UnityEngine;

public class bullet2D : MonoBehaviour
{

    public float bulletSpeed;
    public float damage;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * bulletSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

     private void OnCollisionEnter2D(Collision2D thisCollision)
    {
        Debug.Log("collision detected");
        GameObject theirGameObject = thisCollision.gameObject; //objeto que bateu no collider da bala.
        if (theirGameObject.GetComponent<enemy2D>() != null)
        
        {
        Debug.Log("enemy hit");
        healthSystem theirHealthSystem = theirGameObject.GetComponent<healthSystem>();

        if (theirHealthSystem != null)

        {
        Debug.Log("enemy health system found");
        theirHealthSystem.TakeDamage(damage); //diminui a vida do inimigo em 1
       
    
        }
        
        Destroy(gameObject);   

        }
    }
}
