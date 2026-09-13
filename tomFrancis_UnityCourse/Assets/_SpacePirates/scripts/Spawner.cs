using UnityEngine;

// Um Spawner por prefab. Só sabe: "meu prefab nasce em um destes pontos".
public class Spawner : MonoBehaviour
{
    public GameObject prefab;
    public Transform[] allowedSpawnPoints;

    public float minAngle, maxAngle;

    public void SpawnOne()
    {
        if (allowedSpawnPoints == null || allowedSpawnPoints.Length == 0) return;
        //Escolhe um angulo aleatorio.
        float randomAngle = Random.Range(minAngle, maxAngle);

        //Cria uma rotação a partir do angulo aleatorio
        Quaternion randomRotation = Quaternion.Euler(0, 0, randomAngle);

        //Combina a rotação do spawner com a rotação aleatoria.
        Quaternion finalRotation = transform.rotation * randomRotation;

        Transform spawnPoint = allowedSpawnPoints[Random.Range(0, allowedSpawnPoints.Length)];
        PoolManager.Instance.Get(prefab, spawnPoint.position, finalRotation);
    }
}
