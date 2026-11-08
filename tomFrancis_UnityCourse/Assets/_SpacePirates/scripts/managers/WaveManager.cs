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

    // Modo planilha: contagem exata por wave (Config > Use Matrix).
    bool MatrixMode { get { return config != null && config.useMatrix; } }

    // 0 = waves infinitas. A planilha implica waves limitadas.
    int TotalWavesValue
    {
        get { return config != null && (config.useMatrix || config.limitWaves) ? Mathf.Max(1, config.totalWaves) : 0; }
    }

    public int TotalWaves { get { return TotalWavesValue; } }
    public int CurrentWave { get { return currentWave; } }

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
    bool allWavesDone;

    // Fila de spawns da wave atual no modo planilha (já embaralhada).
    class QueuedSpawn
    {
        public Spawner spawner;
        public WeaponData weapon;
    }
    readonly List<QueuedSpawn> waveQueue = new List<QueuedSpawn>();

    // Um item da fase de loot: o prefab e (opcional) a arma forçada, para caixas de arma da planilha.
    class DropEntry
    {
        public GameObject prefab;
        public WeaponData weapon;
        public DropEntry(GameObject prefab, WeaponData weapon = null) { this.prefab = prefab; this.weapon = weapon; }
    }

    readonly List<ActiveEntry> activeRanges = new List<ActiveEntry>();
    readonly List<Spawner> bonusSpawners = new List<Spawner>();
    readonly Dictionary<GameObject, Spawner> spawnerByPrefab = new Dictionary<GameObject, Spawner>();
    readonly HashSet<GameObject> warnedPrefabs = new HashSet<GameObject>();

    // Avisa sempre que uma nova wave começa, passando o número dela.
    public event System.Action<int> OnWaveStarted;

    // Avisa quando a wave termina de spawnar (começa o intervalo / fase de loot). Passa o número da wave que acabou.
    public event System.Action<int> OnWaveCompleted;

    // Avisa quando a última wave (e o loot dela) terminou. Só dispara com waves limitadas (Config > Limit Waves ou Use Matrix).
    public event System.Action OnAllWavesCompleted;
    public bool AllWavesDone { get { return allWavesDone; } }

    // Fase de loot entre waves (só acontece com Config > Loot Phase > Enabled). O número é a wave que ACABOU DE TERMINAR.
    public event System.Action<int> OnLootPhaseStarted;
    public event System.Action<int> OnLootPhaseEnded;
    public bool InLootPhase { get; private set; }

    // Desempenho da wave: dano que o player sofreu desde que ela começou.
    healthSystem trackedPlayerHealth;
    float lastPlayerHealth;
    float damageTakenThisWave;

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
        if (trackedPlayerHealth != null) trackedPlayerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
        trackedPlayerHealth = null;
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
        spawnedThisWave = 0;
        waveActive = true;
        RefreshActiveSpawners();
        if (MatrixMode)
        {
            BuildWaveQueue();
            currentBudget = waveQueue.Count;
        }
        else
        {
            currentBudget = StartingBudgetValue + (currentWave - 1) * BudgetIncrementValue;
        }
        BeginPerformanceTracking();
        OnWaveStarted?.Invoke(currentWave);
        AudioEvents.PlayWaveStarted();

        // Wave sem nada para nascer (planilha vazia nessa linha): termina na hora.
        if (currentBudget <= 0)
        {
            waveActive = false;
            StartCoroutine(WaitAndStartNextWave());
        }
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

        if (MatrixMode)
        {
            if (spawnedThisWave < waveQueue.Count)
            {
                QueuedSpawn queued = waveQueue[spawnedThisWave];
                ApplyForcedWeapon(queued.spawner.SpawnOne(), queued.weapon);
            }
        }
        else
        {
            Spawner chosen = PickWeightedSpawner();
            if (chosen != null)
            {
                chosen.SpawnOne();
            }
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
        bool isLastWave = TotalWavesValue > 0 && currentWave >= TotalWavesValue;
        bool lootBranch = config != null && config.lootPhase != null && (MatrixMode || config.lootPhase.enabled);

        if (lootBranch)
        {
            // Primeiro espera os inimigos da wave sumirem; só então avisa "wave concluída" e começa o loot.
            LogLoot("wave " + currentWave + " terminou de spawnar");
            if (config.lootPhase.waitForEnemiesToClear)
            {
                float waited = 0f;
                float timeout = Mathf.Max(1f, config.lootPhase.clearEnemiesTimeoutSeconds);
                float nextReport = 2f;
                while (waited < timeout && AnyEnemyAlive())
                {
                    yield return new WaitForSeconds(0.25f);
                    waited += 0.25f;
                    if (waited >= nextReport)
                    {
                        nextReport += 2f;
                        LogLoot("esperando inimigos há " + waited.ToString("0.0") + "s: " + DescribeEnemiesAlive());
                    }
                }
                LogLoot("espera de inimigos acabou após " + waited.ToString("0.0") + "s" + (waited >= timeout ? " (ESTOUROU o limite)" : ""));
            }

            OnWaveCompleted?.Invoke(currentWave);

            // Planilha: o loot roda só nas waves que têm itens de loot. Waves limitadas (sem planilha): a última wave não tem loot.
            bool runLoot = MatrixMode ? HasMatrixLoot(currentWave) : !isLastWave;
            if (runLoot) yield return LootPhaseRoutine(config.lootPhase);
            else if (!isLastWave) yield return new WaitForSeconds(Mathf.Max(0f, BetweenWavesValue));

            AdvanceOrFinish();
            yield break;
        }

        OnWaveCompleted?.Invoke(currentWave);
        if (isLastWave)
        {
            FinishAllWaves();
            yield break;
        }

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

        AdvanceOrFinish();
    }

    // Passa para a próxima wave, ou termina tudo se a última acabou de acontecer (waves limitadas).
    void AdvanceOrFinish()
    {
        if (TotalWavesValue > 0 && currentWave >= TotalWavesValue)
        {
            FinishAllWaves();
            return;
        }

        currentWave++;
        StartWave();
    }

    void FinishAllWaves()
    {
        if (allWavesDone) return;
        allWavesDone = true;
        waveActive = false;
        OnAllWavesCompleted?.Invoke();
    }

    // ---------- Debug (usado pelo DebugCommands) ----------

    // Faz nascer o prefab pelo Spawner dele na cena. Devolve o objeto criado (ou null se não há Spawner).
    public GameObject DebugSpawn(GameObject prefab)
    {
        BuildSpawnerLookup();
        Spawner spawner = FindSpawner(prefab);
        return spawner != null ? spawner.SpawnOne() : null;
    }

    // Pula direto para uma wave: cancela a wave/loot em andamento, limpa os inimigos vivos e começa a wave pedida.
    public void DebugGoToWave(int wave)
    {
        wave = Mathf.Max(1, wave);
        if (TotalWavesValue > 0) wave = Mathf.Min(wave, TotalWavesValue);

        StopAllCoroutines();

        if (InLootPhase)
        {
            InLootPhase = false;
            if (ParallaxSpeedController.Instance != null) ParallaxSpeedController.Instance.ExitLootSlowdown();
            OnLootPhaseEnded?.Invoke(currentWave);
        }

        foreach (enemy2D enemy in FindObjectsByType<enemy2D>(FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.isActiveAndEnabled) PoolManager.Instance.Return(enemy.gameObject);
        }

        hasStartedFirstWave = true;
        allWavesDone = false;
        waveActive = false;
        secondsSinceLastSpawn = 0f;
        currentWave = wave;

        // Se pulou a introdução, o player ainda não entrou em modo de combate.
        if (references.thePlayer != null)
        {
            Player2D player = references.thePlayer.GetComponent<Player2D>();
            if (player != null) player.SetCombatStance(true);
        }

        StartWave();
    }

    // ---------- Planilha de waves ----------

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // Monta a fila (embaralhada) do que nasce durante a wave atual: inimigos e itens da linha "Wave N" da planilha.
    void BuildWaveQueue()
    {
        waveQueue.Clear();
        if (config == null || config.matrixCells == null) return;

        foreach (WaveMatrixCell cell in config.matrixCells)
        {
            if (!CellUsable(cell) || cell.phase != MatrixPhase.Wave) continue;
            if (cell.wave != currentWave || cell.count <= 0) continue;

            for (int i = 0; i < cell.count; i++)
            {
                // Em coluna aleatória cada unidade sorteia o prefab (e usa o Spawner dele).
                Spawner spawner = FindSpawner(ResolveCellPrefab(cell));
                if (spawner == null) continue;

                QueuedSpawn queued = new QueuedSpawn();
                queued.spawner = spawner;
                queued.weapon = cell.group > 0 ? null : cell.weapon;
                waveQueue.Add(queued);
            }
        }

        Shuffle(waveQueue);
        SpaceOutQueue(waveQueue, config.minSpawnsBetweenSameType);
    }

    // Reordena a fila (já embaralhada) para itens do mesmo tipo não nascerem colados: em cada posição pega o primeiro
    // item que não repete um tipo presente nos últimos 'gap' spawns. Tipos muito frequentes têm o espaço reduzido.
    static void SpaceOutQueue(List<QueuedSpawn> queue, int gap)
    {
        if (gap <= 0 || queue.Count < 3) return;

        int total = queue.Count;
        Dictionary<Spawner, int> counts = new Dictionary<Spawner, int>();
        foreach (QueuedSpawn q in queue)
        {
            int c;
            counts.TryGetValue(q.spawner, out c);
            counts[q.spawner] = c + 1;
        }

        List<QueuedSpawn> remaining = new List<QueuedSpawn>(queue);
        List<QueuedSpawn> result = new List<QueuedSpawn>(total);

        while (remaining.Count > 0)
        {
            int pick = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                Spawner candidate = remaining[i].spawner;
                int effectiveGap = Mathf.Min(gap, Mathf.Max(0, total / counts[candidate] - 1));

                bool tooClose = false;
                for (int k = 1; k <= effectiveGap && k <= result.Count; k++)
                {
                    if (result[result.Count - k].spawner == candidate) { tooClose = true; break; }
                }

                if (!tooClose) { pick = i; break; }
            }

            result.Add(remaining[pick]);
            remaining.RemoveAt(pick);
        }

        queue.Clear();
        queue.AddRange(result);
    }

    bool HasMatrixLoot(int wave)
    {
        if (config == null || config.matrixCells == null) return false;
        foreach (WaveMatrixCell cell in config.matrixCells)
        {
            if (CellUsable(cell) && cell.phase == MatrixPhase.Loot && cell.wave == wave && cell.count > 0) return true;
        }
        return false;
    }

    void AddMatrixLootDrops(List<DropEntry> drops, int wave)
    {
        if (config == null || config.matrixCells == null) return;
        foreach (WaveMatrixCell cell in config.matrixCells)
        {
            if (!CellUsable(cell) || cell.phase != MatrixPhase.Loot) continue;
            if (cell.wave != wave || cell.count <= 0) continue;
            for (int i = 0; i < cell.count; i++)
            {
                GameObject chosen = ResolveCellPrefab(cell);
                if (chosen != null) drops.Add(new DropEntry(chosen, cell.group > 0 ? null : cell.weapon));
            }
        }
    }

    // Prefabs válidos (não vazios) do grupo aleatório N (1 = primeiro), ou null se o grupo não existe / está vazio.
    List<GameObject> GroupOptions(int group)
    {
        if (config == null || config.randomGroups == null || group < 1 || group > config.randomGroups.Length) return null;
        MatrixRandomGroup g = config.randomGroups[group - 1];
        if (g == null || g.options == null) return null;

        List<GameObject> list = new List<GameObject>();
        foreach (GameObject option in g.options)
        {
            if (option != null) list.Add(option);
        }
        return list.Count > 0 ? list : null;
    }

    bool CellUsable(WaveMatrixCell cell)
    {
        if (cell == null) return false;
        return cell.group > 0 ? GroupOptions(cell.group) != null : cell.prefab != null;
    }

    // O prefab que esta célula faz nascer agora: o da célula, ou um sorteado do grupo (sorteio puro).
    GameObject ResolveCellPrefab(WaveMatrixCell cell)
    {
        if (cell.group <= 0) return cell.prefab;
        List<GameObject> options = GroupOptions(cell.group);
        return options == null ? null : options[Random.Range(0, options.Count)];
    }

    // Caixa de arma que nasce com arma definida na planilha. Sempre repõe (null = sorteio), porque o objeto vem do pool.
    static void ApplyForcedWeapon(GameObject spawned, WeaponData weapon)
    {
        if (spawned == null) return;
        WeaponPickupReward box = spawned.GetComponent<WeaponPickupReward>();
        if (box != null) box.forcedWeapon = weapon;
    }

    // ---------- Fase de loot ----------

    IEnumerator LootPhaseRoutine(LootPhaseData loot)
    {
        int finishedWave = currentWave;
        List<DropEntry> drops = BuildLootDrops(loot, finishedWave);
        List<GameObject> spawned = new List<GameObject>();

        InLootPhase = true;
        if (ParallaxSpeedController.Instance != null) ParallaxSpeedController.Instance.EnterLootSlowdown();
        OnLootPhaseStarted?.Invoke(finishedWave);

        float maxDuration = Mathf.Max(1f, loot.durationSeconds);
        float dropWindow = maxDuration * Mathf.Clamp(loot.dropWindowFraction, 0.3f, 1f);
        float interval = drops.Count > 0 ? Mathf.Max(Mathf.Max(0.05f, loot.minSecondsBetweenDrops), dropWindow / drops.Count) : 0f;
        Transform lastPoint = null;
        float elapsed = 0f;
        HashSet<GameObject> seenOnScreen = new HashSet<GameObject>();

        LogLoot("loot começou: " + drops.Count + " itens, intervalo " + interval.ToString("0.00") + "s => ~" + (drops.Count * interval).ToString("0.0") + "s só para nascer todos");

        for (int d = 0; d < drops.Count; d++)
        {
            Spawner spawner = FindSpawner(drops[d].prefab);
            if (spawner != null)
            {
                GameObject item = spawner.SpawnOne(lastPoint);
                if (item != null)
                {
                    lastPoint = spawner.LastSpawnPoint;
                    SlowLootItem(item, loot.itemSpeedMultiplier);
                    ApplyForcedWeapon(item, drops[d].weapon);
                    spawned.Add(item);
                }
            }

            if (d == drops.Count - 1) break; // depois do último item não precisa esperar o intervalo

            // Espera o intervalo, mas se o player já limpou tudo o que nasceu antes, não faz ele esperar à toa.
            float waitedInterval = 0f;
            while (waitedInterval < interval)
            {
                bool visibleNow;
                if (waitedInterval >= 0.3f && !AnyLootPending(spawned, seenOnScreen, out visibleNow)) break;
                yield return null;
                waitedInterval += Time.deltaTime;
                elapsed += Time.deltaTime;
            }
        }

        LogLoot("todos os itens nasceram (" + spawned.Count + ") após " + elapsed.ToString("0.0") + "s de loot");

        // Com fim antecipado: espera todos os itens sumirem (pegos ou fora da tela = voltaram ao pool).
        // O limite de segurança só conta enquanto NENHUM item pendente está visível na tela: um item lento
        // atravessando a tela não é "travado". Existe ainda um teto absoluto para a fase nunca ficar infinita.
        // Sem fim antecipado: dura a duração planejada.
        float safety = Mathf.Max(1f, loot.safetyTimeoutSeconds);
        float waitedAfterDrops = 0f;
        float waitedWithNothingVisible = 0f;
        float nextPendingReport = 3f;
        string endReason = "";
        while (true)
        {
            if (loot.endWhenAllCollected)
            {
                bool anyVisible;
                bool pending = AnyLootPending(spawned, seenOnScreen, out anyVisible);
                if (elapsed >= loot.minSecondsBeforeEarlyEnd && !pending) { endReason = "todos os itens consumidos"; break; }
                if (anyVisible) waitedWithNothingVisible = 0f;
                if (waitedWithNothingVisible >= safety) { endReason = "limite de segurança (nenhum item visível)"; break; }
                if (waitedAfterDrops >= LootHardCapSeconds) { endReason = "teto absoluto"; break; }
                if (pending && waitedAfterDrops >= nextPendingReport)
                {
                    nextPendingReport += 3f;
                    LogLoot("esperando itens há " + waitedAfterDrops.ToString("0.0") + "s: " + DescribeLootPending(spawned));
                }
            }
            else if (elapsed >= maxDuration)
            {
                endReason = "duração planejada";
                break;
            }

            yield return null;
            elapsed += Time.deltaTime;
            waitedAfterDrops += Time.deltaTime;
            waitedWithNothingVisible += Time.deltaTime;
        }

        LogLoot("loot terminou: " + endReason + " (loot durou " + elapsed.ToString("0.0") + "s)");

        InLootPhase = false;
        if (ParallaxSpeedController.Instance != null) ParallaxSpeedController.Instance.ExitLootSlowdown();
        OnLootPhaseEnded?.Invoke(finishedWave);
    }

    static bool AnyEnemyAlive()
    {
        enemy2D[] enemies = FindObjectsByType<enemy2D>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].isActiveAndEnabled) return true;
        }
        return false;
    }

    const float LootHardCapSeconds = 180f;

    // Logs de tempo da fase de loot. Desligados: as chamadas nem são compiladas.
    // Para ligar de novo: Project Settings > Player > Scripting Define Symbols > adicione COSMO_LOOT_DEBUG.
    [System.Diagnostics.Conditional("COSMO_LOOT_DEBUG")]
    void LogLoot(string message)
    {
        Debug.Log("[Loot] t=" + Time.time.ToString("0.0") + " " + message, this);
    }

    static string DescribeEnemiesAlive()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Camera cam = Camera.main;
        enemy2D[] enemies = FindObjectsByType<enemy2D>(FindObjectsSortMode.None);
        int shown = 0;
        foreach (enemy2D e in enemies)
        {
            if (e == null || !e.isActiveAndEnabled) continue;
            if (shown++ >= 5) { sb.Append(" ..."); break; }
            Vector3 v = cam != null ? cam.WorldToViewportPoint(e.transform.position) : Vector3.zero;
            sb.Append(e.name).Append(" (tela ").Append(v.x.ToString("0.00")).Append(",").Append(v.y.ToString("0.00")).Append(") ");
        }
        return sb.ToString();
    }

    static string DescribeLootPending(List<GameObject> items)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Camera cam = Camera.main;
        int shown = 0;
        foreach (GameObject item in items)
        {
            if (item == null || !item.activeInHierarchy) continue;
            if (shown++ >= 5) { sb.Append(" ..."); break; }
            Vector3 v = cam != null ? cam.WorldToViewportPoint(item.transform.position) : Vector3.zero;
            sb.Append(item.name).Append(" (tela ").Append(v.x.ToString("0.00")).Append(",").Append(v.y.ToString("0.00")).Append(") ");
        }
        return sb.ToString();
    }

    // Um item está "pendente" enquanto estiver ativo e ainda não tiver saído da tela depois de aparecer.
    // Pegar/destruir o item ou ele sair da tela (com folga) o tira da lista; se saiu da tela e ainda está ativo,
    // devolvemos ao pool na hora (sem esperar o OnBecameInvisible, que no Editor também depende da Scene view).
    static bool AnyLootPending(List<GameObject> items, HashSet<GameObject> seen, out bool anyVisible)
    {
        anyVisible = false;
        bool pending = false;
        Camera cam = Camera.main;

        foreach (GameObject item in items)
        {
            if (item == null || !item.activeInHierarchy) continue;

            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(item.transform.position);
                const float margin = 0.05f;
                bool inside = v.x > -margin && v.x < 1f + margin && v.y > -margin && v.y < 1f + margin;

                if (inside)
                {
                    seen.Add(item);
                    anyVisible = true;
                    pending = true;
                    continue;
                }

                // Saiu da tela depois de aparecer, OU está muito longe da tela (ex.: item lançado para cima/para o lado
                // que nunca entrou na tela e fica caindo pela gravidade): conta como consumido e volta ao pool.
                bool farAway = v.x < -1.5f || v.x > 2.5f || v.y < -1.5f || v.y > 2.5f;
                if (seen.Remove(item) || farAway)
                {
                    if (PoolManager.Instance != null) PoolManager.Instance.Return(item);
                    continue;
                }
            }

            pending = true;
        }
        return pending;
    }

    // Deixa o item mais lento sem pará-lo. Chamado logo depois de nascer (o OnEnable dos movimentos já rodou).
    static void SlowLootItem(GameObject item, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f)) return;

        Rigidbody2D body = item.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity *= multiplier;

        SineMovement sine = item.GetComponent<SineMovement>();
        if (sine != null) sine.timeScale = multiplier;
    }

    // Monta a lista (embaralhada) do que nasce nesta fase: cura, arma especial e recompensa por desempenho.
    List<DropEntry> BuildLootDrops(LootPhaseData loot, int finishedWave)
    {
        List<DropEntry> drops = new List<DropEntry>();

        // Planilha: o que nasce no loot é exatamente o que está na linha "Loot N" (sem regras de cura/arma/desempenho).
        if (MatrixMode)
        {
            AddMatrixLootDrops(drops, finishedWave);
            Shuffle(drops);
            return drops;
        }

        // Cura: sempre os itens fixos + extras se a vida está baixa.
        AddLootItems(drops, loot.healItems, 1f);
        float healthFraction = GetPlayerHealthFraction();
        if (healthFraction < loot.lowHealthThreshold && loot.extraHealsWhenLow > 0)
        {
            GameObject medkit = FirstPrefab(loot.healItems);
            for (int i = 0; i < loot.extraHealsWhenLow; i++) if (medkit != null) drops.Add(new DropEntry(medkit));
        }

        // Arma especial: na primeira wave configurada e depois a cada N waves.
        int every = Mathf.Max(1, loot.weaponEveryNWaves);
        if (finishedWave >= loot.weaponFirstWave && (finishedWave - loot.weaponFirstWave) % every == 0)
        {
            AddLootItems(drops, loot.weaponItems, 1f);
        }

        // Desempenho: quanto menos dano sofrido, mais prêmios (e um bônus se foi perfeito).
        float maxHealth = trackedPlayerHealth != null ? Mathf.Max(1f, trackedPlayerHealth.maxHealth) : 1f;
        float performance = Mathf.Clamp01(1f - damageTakenThisWave / maxHealth);
        AddLootItems(drops, loot.performanceItems, performance);
        if (damageTakenThisWave <= 0.01f && loot.flawlessBonusCount > 0)
        {
            GameObject reward = FirstPrefab(loot.performanceItems);
            for (int i = 0; i < loot.flawlessBonusCount; i++) if (reward != null) drops.Add(new DropEntry(reward));
        }

        // Embaralha para os tipos não saírem em bloco.
        Shuffle(drops);

        return drops;
    }

    static void AddLootItems(List<DropEntry> drops, LootItem[] items, float multiplier)
    {
        if (items == null) return;
        foreach (LootItem item in items)
        {
            if (item == null || item.prefab == null) continue;
            int amount = Mathf.RoundToInt(item.count * multiplier);
            for (int i = 0; i < amount; i++) drops.Add(new DropEntry(item.prefab));
        }
    }

    static GameObject FirstPrefab(LootItem[] items)
    {
        if (items == null) return null;
        foreach (LootItem item in items) if (item != null && item.prefab != null) return item.prefab;
        return null;
    }

    float GetPlayerHealthFraction()
    {
        if (trackedPlayerHealth == null || trackedPlayerHealth.maxHealth <= 0f) return 1f;
        return Mathf.Clamp01(trackedPlayerHealth.currentHealth / trackedPlayerHealth.maxHealth);
    }

    // Zera o dano da wave e (re)liga a escuta da vida do player.
    void BeginPerformanceTracking()
    {
        damageTakenThisWave = 0f;

        if (trackedPlayerHealth == null)
        {
            Player2D player = FindFirstObjectByType<Player2D>();
            if (player != null)
            {
                trackedPlayerHealth = player.GetComponent<healthSystem>();
                if (trackedPlayerHealth == null) trackedPlayerHealth = player.GetComponentInChildren<healthSystem>();
                if (trackedPlayerHealth != null) trackedPlayerHealth.OnHealthChanged += HandlePlayerHealthChanged;
            }
        }

        if (trackedPlayerHealth != null) lastPlayerHealth = trackedPlayerHealth.currentHealth;
    }

    void HandlePlayerHealthChanged(float current, float max)
    {
        if (current < lastPlayerHealth) damageTakenThisWave += lastPlayerHealth - current;
        lastPlayerHealth = current;
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
