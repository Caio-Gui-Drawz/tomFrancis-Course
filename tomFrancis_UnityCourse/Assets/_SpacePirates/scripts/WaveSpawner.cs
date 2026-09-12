using UnityEngine;

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public float weight; // chance relativa de aparecer (não precisa somar 100)
}

[System.Serializable]
public class SpawnPointConfig
{
    public Transform point;
    public SpawnEntry[] spawnables; // o que PODE nascer especificamente neste ponto
}

public class WaveSpawner : MonoBehaviour
{
    public SpawnPointConfig[] spawnPointConfigs;
    public float secondsBetweenSpawns;
    public float minAngle, maxAngle;
    float secondsSinceLastSpawn;

    bool canSpawn;

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(GameState newState)
    {
        canSpawn = (newState == GameState.Playing);
    }

    private void FixedUpdate()
    {
        if (!canSpawn) return;
        if (spawnPointConfigs == null || spawnPointConfigs.Length == 0) return;

        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn < secondsBetweenSpawns) return;

        SpawnPointConfig chosenConfig = spawnPointConfigs[Random.Range(0, spawnPointConfigs.Length)];
        GameObject chosen = PickWeighted(chosenConfig.spawnables);
        if (chosen == null) return;

        Transform spawnPoint = chosenConfig.point != null ? chosenConfig.point : transform;
        float randomAngle = Random.Range(minAngle, maxAngle);
        Quaternion finalRotation = spawnPoint.rotation * Quaternion.Euler(0, 0, randomAngle);
        Instantiate(chosen, spawnPoint.position, finalRotation);

        secondsSinceLastSpawn = 0;
    }

    GameObject PickWeighted(SpawnEntry[] entries)
    {
        if (entries == null || entries.Length == 0) return null;

        float totalWeight = 0;
        foreach (var entry in entries) totalWeight += entry.weight;

        float roll = Random.Range(0, totalWeight);
        float cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.prefab;
        }
        return entries[entries.Length - 1].prefab;
    }
}