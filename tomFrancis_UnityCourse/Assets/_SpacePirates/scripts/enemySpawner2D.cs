using UnityEngine;

public class enemySpawner2D : MonoBehaviour
{
public GameObject enemyPrefab;

public float secondsBetweenSpawns;
float secondsSinceLastSpawn;

public float minAngle;
public float maxAngle;


    
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
            //Escolhe um angulo aleatorio.
            float randomAngle = Random.Range(minAngle, maxAngle);

            //Cria uma rotação a partir do angulo aleatorio
            Quaternion randomRotation = Quaternion.Euler(0, 0, randomAngle);

            //Combina a rotação do spawner com a rotação aleatoria.
            Quaternion finalRotation = transform.rotation * randomRotation;

            //cria o inimigo usando essa rotação
            Instantiate(enemyPrefab, transform.position, finalRotation);
            
            secondsSinceLastSpawn = 0;
        }
    }

}
