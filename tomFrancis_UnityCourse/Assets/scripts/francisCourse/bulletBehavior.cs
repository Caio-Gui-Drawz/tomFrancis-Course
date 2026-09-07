using UnityEngine;

public class bulletBehavior : MonoBehaviour
{
    public float bulletSpeed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //declara uma informação que eu quero guardar, nesse caso ao invez de uma variavel, estou guardando uma componente do tipo Rigidbody.  
        Rigidbody ourRigidbody = GetComponent<Rigidbody>();
        ourRigidbody.linearVelocity = transform.forward * bulletSpeed;


    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnCollisionEnter(Collision thisCollision)
    {
        GameObject theirGameObject = thisCollision.gameObject; //objeto que bateu no collider da bala.
        if (theirGameObject.GetComponent<enemyBehavior>() != null)
        {

        Destroy(theirGameObject); 
        Destroy(gameObject);   
        }
        
   
    }
    
}
