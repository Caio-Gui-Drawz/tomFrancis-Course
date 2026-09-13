using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class SpawnerRange
    {
        public Spawner spawner;
        public int startWave = 1;
        public int endWave = int.MaxValue; // deixe bem alto (ou não mexa) para "nunca remove"
    }

    public SpawnerRange[] spawnerRanges;
    public int startingBudget = 30;
    public int budgetIncrementPerWave = 10;
    public float secondsBetweenSpawnsInWave = 0.5f;
    public float secondsBetweenWaves = 3f;

    int currentWave = 1;
    int currentBudget;
    int spawnedThisWave;
    float secondsSinceLastSpawn;
    bool waveActive;
    bool canRun;

    List<Spawner> activeSpawners = new List<Spawner>();

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
        canRun = (newState == GameState.Playing);
    }

    void Start()
    {
        StartWave();
    }

    void StartWave()
    {
        currentBudget = startingBudget + (currentWave - 1) * budgetIncrementPerWave;
        spawnedThisWave = 0;
        waveActive = true;
        RefreshActiveSpawners();
    }

    void RefreshActiveSpawners()
    {
        activeSpawners.Clear();
        foreach (var range in spawnerRanges)
        {
            if (currentWave >= range.startWave && currentWave <= range.endWave)
            {
                activeSpawners.Add(range.spawner);
            }
        }
    }

    void FixedUpdate()
    {
        if (!canRun || !waveActive) return;

        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn < secondsBetweenSpawnsInWave) return;
        secondsSinceLastSpawn = 0;

        if (activeSpawners.Count > 0)
        {
            Spawner chosen = activeSpawners[Random.Range(0, activeSpawners.Count)];
            chosen.SpawnOne();
        }

        spawnedThisWave++;
        if (spawnedThisWave >= currentBudget)
        {
            waveActive = false;
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    IEnumerator WaitAndStartNextWave()
    {
        yield return new WaitForSeconds(secondsBetweenWaves);
        currentWave++;
        StartWave();
    }
}
