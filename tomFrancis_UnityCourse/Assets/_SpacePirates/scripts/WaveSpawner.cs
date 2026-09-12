using UnityEngine;

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public float weight; // chance relativa de aparecer (não precisa somar 100)
}

public class WaveSpawner : MonoBehaviour
{
    public SpawnEntry[] spawnables;
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

        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn < secondsBetweenSpawns) return;

        GameObject chosen = PickWeighted();
        if (chosen == null) return;

        float randomAngle = Random.Range(minAngle, maxAngle);
        Quaternion finalRotation = transform.rotation * Quaternion.Euler(0, 0, randomAngle);
        Instantiate(chosen, transform.position, finalRotation);

        secondsSinceLastSpawn = 0;
    }

    GameObject PickWeighted()
    {
        if (spawnables == null || spawnables.Length == 0) return null;

        float totalWeight = 0;
        foreach (var entry in spawnables) totalWeight += entry.weight;

        float roll = Random.Range(0, totalWeight);
        float cumulative = 0;
        foreach (var entry in spawnables)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.prefab;
        }
        return spawnables[spawnables.Length - 1].prefab; // fallback de segurança
    }
}
