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
    // Ponto de spawn usado pela última chamada (a fase de loot usa isso para não repetir o mesmo ponto em seguida).
    public Transform LastSpawnPoint { get; private set; }

    public GameObject SpawnOne()
    {
        return SpawnOne(null);
    }

    // Igual a SpawnOne, mas evita 'avoid' quando houver outro ponto disponível.
    public GameObject SpawnOne(Transform avoid)
    {
        if (allowedSpawnPoints == null || allowedSpawnPoints.Length == 0) return null;
        //Escolhe um angulo aleatorio.


        Transform spawnPoint = allowedSpawnPoints[Random.Range(0, allowedSpawnPoints.Length)];
        if (avoid != null && spawnPoint == avoid && allowedSpawnPoints.Length > 1)
        {
            for (int i = 0; i < 8 && spawnPoint == avoid; i++)
            {
                spawnPoint = allowedSpawnPoints[Random.Range(0, allowedSpawnPoints.Length)];
            }
        }
        LastSpawnPoint = spawnPoint;
        return PoolManager.Instance.Get(prefab, spawnPoint.position + offset, Quaternion.identity);
    }
}
