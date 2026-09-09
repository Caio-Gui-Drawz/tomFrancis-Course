using UnityEngine;

public class enemySpawner : MonoBehaviour
{
public GameObject enemyPrefab;
public GameObject spawnPoint;
public float secondsBetweenSpawns;
float secondsSinceLastSpawn;






    
    void Start()
    {
        secondsSinceLastSpawn = 0;

    }

    //fixed update happens the same number of times for all players, so it' a good place for gameplay critical things

    private void FixedUpdate()
    {
        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn >= secondsBetweenSpawns)
        {
        

            //cria o inimigo usando essa rotação
            Instantiate(enemyPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
            secondsSinceLastSpawn = 0;
        }
    }

}
