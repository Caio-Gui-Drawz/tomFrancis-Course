using UnityEngine;

// Um Spawner por prefab. Só sabe: "meu prefab nasce em um destes pontos".
public class Spawner : MonoBehaviour
{
    public GameObject prefab;
    public Transform[] allowedSpawnPoints;

    public void SpawnOne()
    {
        if (allowedSpawnPoints == null || allowedSpawnPoints.Length == 0) return;

        Transform spawnPoint = allowedSpawnPoints[Random.Range(0, allowedSpawnPoints.Length)];
        PoolManager.Instance.Get(prefab, spawnPoint.position, spawnPoint.rotation);
    }
}
