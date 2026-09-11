using UnityEngine;

public class weaponBehavior : MonoBehaviour
{

     public GameObject bulletPrefab;
     public float accuracy;

      public float fireRate; //how many seconds between shots 
      public float numberOfProjectiles;
      public GameObject bulletSpawnPoint;

      float secondsSinceLastShot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
          secondsSinceLastShot = fireRate; //inicia podendo atirar
    }

    // Update is called once per frame
    void Update()
    {
         //Firing
    secondsSinceLastShot += Time.deltaTime; //vai aumentar esse valor todo segundo ate chegar no fireRate, ai o player vai poder atirar de novo




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
