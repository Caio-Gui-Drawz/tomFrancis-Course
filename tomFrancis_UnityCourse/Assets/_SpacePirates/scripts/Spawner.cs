using UnityEngine;

// Um Spawner por prefab. Só sabe: "meu prefab nasce em um destes pontos".
public class Spawner : MonoBehaviour
{
    public GameObject prefab;
    public Transform[] allowedSpawnPoints;

    private Vector3 offset;
    public Vector2 offsetRange;


    public void OnEnable()
    {
        offset = new Vector3(Random.Range(offsetRange.x, offsetRange.y), 0, Random.Range(offsetRange.x, offsetRange.y));
    }
    public void SpawnOne()
    {
        if (allowedSpawnPoints == null || allowedSpawnPoints.Length == 0) return;
        //Escolhe um angulo aleatorio.


        Transform spawnPoint = allowedSpawnPoints[Random.Range(0, allowedSpawnPoints.Length)];
        PoolManager.Instance.Get(prefab, spawnPoint.position + offset, Quaternion.identity);
    }
}
