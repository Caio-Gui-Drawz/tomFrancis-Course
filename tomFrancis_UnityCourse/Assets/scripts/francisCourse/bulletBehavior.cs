using UnityEngine;

public class bulletBehavior : MonoBehaviour
{
    public float bulletSpeed;
    public float secondsUntilDestroy;
    public float damage;
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
        secondsUntilDestroy -= Time.deltaTime; //diminui o tempo 


        if (secondsUntilDestroy <1)
        {
            transform.localScale *= secondsUntilDestroy; //como o tempo está diminuindo, se multiplicar esse valor pela escala do objeto, ele vai diminuir
        }


        if (secondsUntilDestroy <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision thisCollision)
    {
        GameObject theirGameObject = thisCollision.gameObject; //objeto que bateu no collider da bala.
        if (theirGameObject.GetComponent<enemyBehavior>() != null)
        
        {
        
        healthSystem theirHealthSystem = theirGameObject.GetComponent<healthSystem>();

        if (theirHealthSystem != null)

        {

        theirHealthSystem.TakeDamage(damage); //diminui a vida do inimigo em 1
       
    
        }
        
        Destroy(gameObject);   

        }
    }
}
