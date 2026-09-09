using UnityEngine;

public class enemySpawner2D : MonoBehaviour
{
public GameObject enemyPrefab;

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
            Instantiate(enemyPrefab, transform.position, transform.rotation);
            secondsSinceLastSpawn = 0;
        }
    }

}
