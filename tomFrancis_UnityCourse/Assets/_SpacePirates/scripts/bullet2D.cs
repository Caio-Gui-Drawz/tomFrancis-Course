using UnityEngine;

public class bullet2D : MonoBehaviour
{

    public float bulletSpeed;
    public float damage;
    public float secondsUntilDestroy;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * bulletSpeed;
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
