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
        public float weight = 1f; // maior = mais frequente, menor = mais raro
    }

    public SpawnerRange[] spawnerRanges;
    public int startingBudget = 30;
    public int budgetIncrementPerWave = 10;
    public float secondsBetweenSpawnsInWave = 0.5f;
    public float secondsBetweenWaves = 3f;

    [Header("Intro")]
    [Tooltip("Quanto tempo depois de começar a jogar até a wave 1 realmente começar (janela de só voar, sem combate).")]
    public float secondsBeforeFirstWave = 3f;

    // Rodada de bônus de tesouros, que acontece durante o intervalo entre uma wave e outra.
    public Spawner[] treasureBonusSpawners;
    public float secondsBetweenBonusSpawns = 0.3f;

    int currentWave = 1;
    int currentBudget;
    int spawnedThisWave;
    float secondsSinceLastSpawn;
    bool waveActive;
    bool canRun;
    bool hasStartedFirstWave;

    List<SpawnerRange> activeRanges = new List<SpawnerRange>();

    // Avisa sempre que uma nova wave começa, passando o número dela.
    public event System.Action<int> OnWaveStarted;

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

        // Só dispara a sequência da wave 1 na PRIMEIRA vez que o jogo entra em Playing
        // (evita reiniciar a introdução se o player só pausar e voltar).
        if (newState == GameState.Playing && !hasStartedFirstWave)
        {
            hasStartedFirstWave = true;
            StartCoroutine(WaitAndStartFirstWave());
        }
    }

    IEnumerator WaitAndStartFirstWave()
    {
        yield return new WaitForSeconds(secondsBeforeFirstWave);
        StartWave();
    }

    void StartWave()
    {
        currentBudget = startingBudget + (currentWave - 1) * budgetIncrementPerWave;
        spawnedThisWave = 0;
        waveActive = true;
        RefreshActiveSpawners();
        OnWaveStarted?.Invoke(currentWave);
    }

    void RefreshActiveSpawners()
    {
        activeRanges.Clear();
        foreach (var range in spawnerRanges)
        {
            if (currentWave >= range.startWave && currentWave <= range.endWave)
            {
                activeRanges.Add(range);
            }
        }
    }

    void FixedUpdate()
    {
        if (!canRun || !waveActive) return;

        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn < secondsBetweenSpawnsInWave) return;
        secondsSinceLastSpawn = 0;

        Spawner chosen = PickWeightedSpawner();
        if (chosen != null)
        {
            chosen.SpawnOne();
        }

        spawnedThisWave++;
        if (spawnedThisWave >= currentBudget)
        {
            waveActive = false;
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    Spawner PickWeightedSpawner()
    {
        if (activeRanges.Count == 0) return null;

        float totalWeight = 0;
        foreach (var range in activeRanges) totalWeight += range.weight;

        float roll = Random.Range(0, totalWeight);
        float cumulative = 0;
        foreach (var range in activeRanges)
        {
            cumulative += range.weight;
            if (roll <= cumulative) return range.spawner;
        }
        return activeRanges[activeRanges.Count - 1].spawner;
    }

    IEnumerator WaitAndStartNextWave()
    {
        float elapsed = 0f;
        while (elapsed < secondsBetweenWaves)
        {
            if (treasureBonusSpawners != null && treasureBonusSpawners.Length > 0)
            {
                Spawner chosen = treasureBonusSpawners[Random.Range(0, treasureBonusSpawners.Length)];
                chosen.SpawnOne();
            }

            yield return new WaitForSeconds(secondsBetweenBonusSpawns);
            elapsed += secondsBetweenBonusSpawns;
        }

        currentWave++;
        StartWave();
    }
}
