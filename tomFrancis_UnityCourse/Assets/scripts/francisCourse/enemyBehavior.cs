using UnityEngine;

public class enemyBehavior : MonoBehaviour

{
    
    public float speed;
    public float damage;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        if (references.thePlayer != null)
        {       
        
        Rigidbody ourRigidbody = GetComponent<Rigidbody>();
        Vector3 vectorToPlayer = references.thePlayer.transform.position - transform.position;
        ourRigidbody.linearVelocity = vectorToPlayer.normalized * speed; //normalizar faz o vetor ter a mesma direção mas com comprimento (magnitude) igual a 1})
 
        }
    }

     private void OnCollisionEnter(Collision thisCollision)
    {
        GameObject theirGameObject = thisCollision.gameObject; //objeto que bateu no collider da bala.
        if (theirGameObject.GetComponent<playerBehavior>() != null)
        
        {
        
        healthSystem theirHealthSystem = theirGameObject.GetComponent<healthSystem>();

        if (theirHealthSystem != null)

        {

        theirHealthSystem.TakeDamage(damage); //diminui a vida do inimigo em 1
       
    
        }
        

        }
    }
}
