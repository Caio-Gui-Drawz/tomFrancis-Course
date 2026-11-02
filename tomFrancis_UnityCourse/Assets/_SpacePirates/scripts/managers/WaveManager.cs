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

    [Header("Configuração (asset compartilhado entre cenas)")]
    [Tooltip("Se preenchido, as waves usam os valores deste asset e os campos LEGADOS abaixo são ignorados. Para criar a partir dos valores atuais: menu de contexto do componente > 'Criar WaveConfig a partir dos valores atuais'.")]
    public WaveConfig config;

    [Header("LEGADO — usado só se 'Config' estiver vazio")]
    public SpawnerRange[] spawnerRanges;
    public int startingBudget = 30;
    public int budgetIncrementPerWave = 10;
    public float secondsBetweenSpawnsInWave = 0.5f;
    public float secondsBetweenWaves = 3f;

    [Header("Intro (legado)")]
    [Tooltip("Quanto tempo depois de começar a jogar até a wave 1 realmente começar (janela de só voar, sem combate).")]
    public float secondsBeforeFirstWave = 3f;

    // Rodada de bônus de tesouros, que acontece durante o intervalo entre uma wave e outra (legado).
    public Spawner[] treasureBonusSpawners;
    public float secondsBetweenBonusSpawns = 0.3f;

    // Valores em uso: do asset (Config) se existir, senão dos campos legados.
    int StartingBudgetValue { get { return config != null ? config.startingBudget : startingBudget; } }
    int BudgetIncrementValue { get { return config != null ? config.budgetIncrementPerWave : budgetIncrementPerWave; } }
    float SpawnIntervalValue { get { return config != null ? config.secondsBetweenSpawnsInWave : secondsBetweenSpawnsInWave; } }
    float BetweenWavesValue { get { return config != null ? config.secondsBetweenWaves : secondsBetweenWaves; } }
    float BeforeFirstWaveValue { get { return config != null ? config.secondsBeforeFirstWave : secondsBeforeFirstWave; } }
    float BonusIntervalValue { get { return config != null ? config.secondsBetweenBonusSpawns : secondsBetweenBonusSpawns; } }

    class ActiveEntry
    {
        public Spawner spawner;
        public float weight;
    }

    int currentWave = 1;
    int currentBudget;
    int spawnedThisWave;
    float secondsSinceLastSpawn;
    bool waveActive;
    bool canRun;
    bool hasStartedFirstWave;

    readonly List<ActiveEntry> activeRanges = new List<ActiveEntry>();
    readonly List<Spawner> bonusSpawners = new List<Spawner>();
    readonly Dictionary<GameObject, Spawner> spawnerByPrefab = new Dictionary<GameObject, Spawner>();
    readonly HashSet<GameObject> warnedPrefabs = new HashSet<GameObject>();

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
        yield return new WaitForSeconds(BeforeFirstWaveValue);
        StartWave();
    }

    void StartWave()
    {
        currentBudget = StartingBudgetValue + (currentWave - 1) * BudgetIncrementValue;
        spawnedThisWave = 0;
        waveActive = true;
        RefreshActiveSpawners();
        OnWaveStarted?.Invoke(currentWave);
        AudioEvents.PlayWaveStarted();
    }

    // Monta a lista de spawners ativos nesta wave. Com Config, cada faixa aponta para um PREFAB e o
    // Spawner é procurado na cena (um Spawner por prefab); no modo legado a faixa aponta direto para o Spawner.
    void RefreshActiveSpawners()
    {
        BuildSpawnerLookup();

        activeRanges.Clear();
        bonusSpawners.Clear();

        if (config != null)
        {
            if (config.ranges != null)
            {
                foreach (WaveRangeData range in config.ranges)
                {
                    if (range == null || range.prefab == null) continue;
                    if (currentWave < range.startWave || currentWave > range.endWave) continue;

                    Spawner spawner = FindSpawner(range.prefab);
                    if (spawner != null) AddActive(spawner, range.weight);
                }
            }

            if (config.treasureBonusPrefabs != null)
            {
                foreach (GameObject prefab in config.treasureBonusPrefabs)
                {
                    Spawner spawner = FindSpawner(prefab);
                    if (spawner != null) bonusSpawners.Add(spawner);
                }
            }
        }
        else
        {
            if (spawnerRanges != null)
            {
                foreach (SpawnerRange range in spawnerRanges)
                {
                    if (range == null || range.spawner == null) continue;
                    if (currentWave >= range.startWave && currentWave <= range.endWave) AddActive(range.spawner, range.weight);
                }
            }

            if (treasureBonusSpawners != null)
            {
                foreach (Spawner spawner in treasureBonusSpawners)
                {
                    if (spawner != null) bonusSpawners.Add(spawner);
                }
            }
        }
    }

    void AddActive(Spawner spawner, float weight)
    {
        ActiveEntry entry = new ActiveEntry();
        entry.spawner = spawner;
        entry.weight = weight;
        activeRanges.Add(entry);
    }

    void BuildSpawnerLookup()
    {
        spawnerByPrefab.Clear();

        // Os spawners ligados direto (legado) têm prioridade.
        if (spawnerRanges != null)
        {
            foreach (SpawnerRange range in spawnerRanges)
            {
                if (range != null && range.spawner != null && range.spawner.prefab != null) spawnerByPrefab[range.spawner.prefab] = range.spawner;
            }
        }

        foreach (Spawner spawner in FindObjectsByType<Spawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spawner.prefab != null && !spawnerByPrefab.ContainsKey(spawner.prefab)) spawnerByPrefab[spawner.prefab] = spawner;
        }
    }

    Spawner FindSpawner(GameObject prefab)
    {
        if (prefab == null) return null;

        Spawner spawner;
        if (spawnerByPrefab.TryGetValue(prefab, out spawner)) return spawner;

        if (warnedPrefabs.Add(prefab))
        {
            Debug.LogWarning("WaveManager: não há Spawner nesta cena para o prefab '" + prefab.name + "'. Essa faixa de wave será ignorada.", this);
        }
        return null;
    }

    void FixedUpdate()
    {
        if (!canRun || !waveActive) return;

        secondsSinceLastSpawn += Time.fixedDeltaTime;
        if (secondsSinceLastSpawn < SpawnIntervalValue) return;
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
        foreach (ActiveEntry entry in activeRanges) totalWeight += entry.weight;

        float roll = Random.Range(0, totalWeight);
        float cumulative = 0;
        foreach (ActiveEntry entry in activeRanges)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.spawner;
        }
        return activeRanges[activeRanges.Count - 1].spawner;
    }

    IEnumerator WaitAndStartNextWave()
    {
        float step = Mathf.Max(0.01f, BonusIntervalValue);
        float elapsed = 0f;
        while (elapsed < BetweenWavesValue)
        {
            if (bonusSpawners.Count > 0)
            {
                Spawner chosen = bonusSpawners[Random.Range(0, bonusSpawners.Count)];
                chosen.SpawnOne();
            }

            yield return new WaitForSeconds(step);
            elapsed += step;
        }

        currentWave++;
        StartWave();
    }

#if UNITY_EDITOR
    // Cria o asset WaveConfig com os valores que este WaveManager tem hoje e passa a usá-lo.
    [ContextMenu("Criar WaveConfig a partir dos valores atuais")]
    public void MigrateToConfigEditor()
    {
        if (config != null)
        {
            Debug.LogWarning("WaveManager: já usa o WaveConfig '" + config.name + "'.", this);
            return;
        }

        WaveConfig asset = ScriptableObject.CreateInstance<WaveConfig>();
        asset.startingBudget = startingBudget;
        asset.budgetIncrementPerWave = budgetIncrementPerWave;
        asset.secondsBetweenSpawnsInWave = secondsBetweenSpawnsInWave;
        asset.secondsBetweenWaves = secondsBetweenWaves;
        asset.secondsBeforeFirstWave = secondsBeforeFirstWave;
        asset.secondsBetweenBonusSpawns = secondsBetweenBonusSpawns;

        List<WaveRangeData> ranges = new List<WaveRangeData>();
        if (spawnerRanges != null)
        {
            foreach (SpawnerRange range in spawnerRanges)
            {
                if (range == null || range.spawner == null || range.spawner.prefab == null) continue;
                WaveRangeData data = new WaveRangeData();
                data.prefab = range.spawner.prefab;
                data.startWave = range.startWave;
                data.endWave = range.endWave;
                data.weight = range.weight;
                ranges.Add(data);
            }
        }
        asset.ranges = ranges.ToArray();

        List<GameObject> bonus = new List<GameObject>();
        if (treasureBonusSpawners != null)
        {
            foreach (Spawner spawner in treasureBonusSpawners)
            {
                if (spawner != null && spawner.prefab != null && !bonus.Contains(spawner.prefab)) bonus.Add(spawner.prefab);
            }
        }
        asset.treasureBonusPrefabs = bonus.ToArray();

        string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/_SpacePirates/SOs/WaveConfig.asset");
        UnityEditor.AssetDatabase.CreateAsset(asset, path);
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.Undo.RecordObject(this, "Usar WaveConfig");
        config = asset;
        UnityEditor.EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log("WaveManager: criado " + path + " com os valores atuais e ligado neste WaveManager. Salve a cena (Ctrl+S).", asset);
    }
#endif
}
