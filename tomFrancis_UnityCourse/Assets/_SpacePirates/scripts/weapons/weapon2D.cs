using UnityEngine;

public class weapon2D : MonoBehaviour
{

     public GameObject bulletPrefab;
     public float accuracy;

      public float fireRate; //how many seconds between shots 
      public float numberOfProjectiles;
      public GameObject bulletSpawnPoint;

      float secondsSinceLastShot;
     

    
    void Start()
    {
         secondsSinceLastShot = fireRate; //inicia podendo atirar
    }


    void Update()
    {
         

        // Atirar
        secondsSinceLastShot += Time.deltaTime;
        if (secondsSinceLastShot >= fireRate && Input.GetButton("Fire1"))
        {
            Instantiate(bulletPrefab, transform.position + transform.right, transform.rotation);
            secondsSinceLastShot = 0;
        }

        // Virar de frente pro mouse
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = mousePosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void Fire(Vector3 targetPosition)
    {
          if (secondsSinceLastShot >= fireRate)
       {
          //ready to fire
        for (
        int iterationCount = 0;  //Declare a variable to keep track of how many iterations we've done
        iterationCount < numberOfProjectiles; // set a limit for how high this variable can go
        iterationCount++ //run this after each time we iterate - increase the iteration count.
        )
            {

          
        GameObject newBullet = Instantiate(bulletPrefab, bulletSpawnPoint.transform.position, bulletSpawnPoint.transform.rotation);
        //Offset that target position by a random amount, according to our inaccuracy.
        float inaccuracy = Vector3.Distance(transform.position, targetPosition) / accuracy; 
        Vector3 inaccuratePosition = targetPosition;
        inaccuratePosition.x += Random.Range(-inaccuracy, inaccuracy);
        inaccuratePosition.z += Random.Range(-inaccuracy, inaccuracy);
        newBullet.transform.LookAt(inaccuratePosition);
        
      
        secondsSinceLastShot = 0; //reseta o contador de tempo para o próximo tiro
        newBullet.name = iterationCount.ToString();

            }
        }
    
    }

}
