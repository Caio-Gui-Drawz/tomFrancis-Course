using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum BalanceKind { Enemy, Treasure, Medkit, WeaponBox, Other }

// Resumo de um prefab "balanceável" (inimigo, tesouro, medkit, caixa de arma...).
public class BalancePrefab
{
    public string path;
    public GameObject root;
    public BalanceKind kind;
    public healthSystem health;
    public enemy2D enemy;
    public TreasureReward treasure;
    public HealthPickupReward medkit;
    public WeaponPickupReward weaponBox;
    public ExplosionDamage2D explosion;
    public KnockbackReceiver knockback;

    public string Name { get { return root != null ? root.name : "?"; } }

    public bool HasWeaponList
    {
        get { return weaponBox != null && weaponBox.possibleWeapons != null && weaponBox.possibleWeapons.Length > 0; }
    }
}

// Premissas editáveis do modelo de dificuldade (salvas nas EditorPrefs).
[Serializable]
public class BalanceAssumptions
{
    [Tooltip("Fração do dano da arma que realmente acerta (mira, erros, alvo se movendo).")]
    public float accuracy = 0.7f;
    [Tooltip("Para armas com vários projéteis: fração dos projéteis que acerta o mesmo alvo.")]
    public float pelletHitFraction = 0.5f;
    [Tooltip("Chance de o player conseguir pegar um item (medkit / caixa de arma) que apareceu.")]
    public float pickupCatchRate = 0.6f;
    [Tooltip("Mesmo se o player der conta de tudo, essa fração dos inimigos ainda encosta nele.")]
    public float baseLeakFraction = 0.1f;
    [Tooltip("Chance de o dano de uma explosão (mina) atingir o player.")]
    public float explosionHitsPlayerChance = 0.25f;
    public float lootCatchRate = 0.85f; // na fase de loot não há inimigos pressionando
    public int waves = 12;
}

public class WaveStats
{
    public int wave;
    public int budget;
    public float spawnSeconds;
    public float gapSeconds;
    public float totalSeconds;

    public float enemies;
    public float enemyHp;
    public float contactDamage;   // dano total se TODOS os inimigos encostassem no player
    public float explosionDamage; // dano total de explosões (potencial)
    public float treasures;
    public float score;
    public float medkits;
    public float healTotal;
    public float weaponBoxes;
    public float lootBoxes; // parte de weaponBoxes que vem da fase de loot

    public float specialFraction;
    public float effectiveDps;
    public float timeToKill;
    public float killPressure;
    public float expectedDamage;
    public float expectedHeal;
    public float hpEnd;
}

// Os números das waves, lidos do WaveConfig (asset) ou, enquanto não houver um, do WaveManager antigo da cena.
public class WaveSource
{
    public int startingBudget;
    public int budgetIncrementPerWave;
    public float secondsBetweenSpawnsInWave;
    public float secondsBetweenWaves;
    public float secondsBeforeFirstWave;
    public float secondsBetweenBonusSpawns;
    public List<WaveRangeData> ranges = new List<WaveRangeData>();
    public List<GameObject> bonusPrefabs = new List<GameObject>();
    public LootPhaseData loot; // null = fase de loot desligada (vale a rodada de bônus antiga)
    public bool limitWaves;
    public int totalWaves = 8;
    public bool useMatrix;     // planilha: contagem exata por wave (ignora orçamento e faixas)
    public List<WaveMatrixCell> cells = new List<WaveMatrixCell>();

    public static WaveSource FromConfig(WaveConfig c)
    {
        WaveSource s = new WaveSource();
        s.startingBudget = c.startingBudget;
        s.budgetIncrementPerWave = c.budgetIncrementPerWave;
        s.secondsBetweenSpawnsInWave = c.secondsBetweenSpawnsInWave;
        s.secondsBetweenWaves = c.secondsBetweenWaves;
        s.secondsBeforeFirstWave = c.secondsBeforeFirstWave;
        s.secondsBetweenBonusSpawns = c.secondsBetweenBonusSpawns;
        if (c.ranges != null)
        {
            foreach (WaveRangeData r in c.ranges)
            {
                if (r != null) s.ranges.Add(r);
            }
        }
        if (c.treasureBonusPrefabs != null) s.bonusPrefabs.AddRange(c.treasureBonusPrefabs);
        // Na planilha o loot roda nas waves que têm itens de loot, mesmo com 'enabled' desligado.
        if (c.lootPhase != null && (c.lootPhase.enabled || c.useMatrix)) s.loot = c.lootPhase;
        s.limitWaves = c.limitWaves;
        s.totalWaves = c.totalWaves;
        s.useMatrix = c.useMatrix;
        if (c.matrixCells != null)
        {
            foreach (WaveMatrixCell cell in c.matrixCells)
            {
                if (cell != null) s.cells.Add(cell);
            }
        }
        return s;
    }

    // Modo legado: uma entrada para CADA faixa do WaveManager (mesma ordem, para os índices baterem com o Inspector).
    public static WaveSource FromManager(WaveManager wm)
    {
        WaveSource s = new WaveSource();
        s.startingBudget = wm.startingBudget;
        s.budgetIncrementPerWave = wm.budgetIncrementPerWave;
        s.secondsBetweenSpawnsInWave = wm.secondsBetweenSpawnsInWave;
        s.secondsBetweenWaves = wm.secondsBetweenWaves;
        s.secondsBeforeFirstWave = wm.secondsBeforeFirstWave;
        s.secondsBetweenBonusSpawns = wm.secondsBetweenBonusSpawns;
        if (wm.spawnerRanges != null)
        {
            foreach (WaveManager.SpawnerRange r in wm.spawnerRanges)
            {
                WaveRangeData d = new WaveRangeData();
                if (r != null)
                {
                    d.prefab = r.spawner != null ? r.spawner.prefab : null;
                    d.startWave = r.startWave;
                    d.endWave = r.endWave;
                    d.weight = r.weight;
                }
                s.ranges.Add(d);
            }
        }
        if (wm.treasureBonusSpawners != null)
        {
            foreach (Spawner sp in wm.treasureBonusSpawners)
            {
                if (sp != null && sp.prefab != null) s.bonusPrefabs.Add(sp.prefab);
            }
        }
        return s;
    }
}

public static class BalanceModel
{
    public static readonly List<BalancePrefab> prefabs = new List<BalancePrefab>();
    public static readonly List<WeaponData> weapons = new List<WeaponData>();
    public static readonly List<WaveConfig> waveConfigs = new List<WaveConfig>();
    public static GameObject playerPrefab;

    static readonly Dictionary<GameObject, BalancePrefab> byRoot = new Dictionary<GameObject, BalancePrefab>();

    public const string PrefabFolder = "Assets/_SpacePirates/prefabs";

    public static void Refresh()
    {
        prefabs.Clear();
        weapons.Clear();
        waveConfigs.Clear();
        byRoot.Clear();
        playerPrefab = null;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            if (go.GetComponent<Player2D>() != null) { playerPrefab = go; continue; }

            BalancePrefab info = Build(go, path);
            byRoot[go] = info;
            if (info.health != null) prefabs.Add(info);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
        {
            WeaponData w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (w != null) weapons.Add(w);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:WaveConfig"))
        {
            WaveConfig c = AssetDatabase.LoadAssetAtPath<WaveConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) waveConfigs.Add(c);
        }

        prefabs.Sort((a, b) =>
        {
            int c = a.kind.CompareTo(b.kind);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        weapons.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    static BalancePrefab Build(GameObject go, string path)
    {
        BalancePrefab p = new BalancePrefab();
        p.path = path;
        p.root = go;
        p.health = go.GetComponent<healthSystem>();
        p.enemy = go.GetComponent<enemy2D>();
        p.treasure = go.GetComponent<TreasureReward>();
        p.medkit = go.GetComponent<HealthPickupReward>();
        p.weaponBox = go.GetComponent<WeaponPickupReward>();
        p.knockback = go.GetComponent<KnockbackReceiver>();
        if (p.health != null && p.health.deathEffectPrefab != null)
        {
            p.explosion = p.health.deathEffectPrefab.GetComponent<ExplosionDamage2D>();
        }

        if (p.medkit != null) p.kind = BalanceKind.Medkit;
        else if (p.HasWeaponList) p.kind = BalanceKind.WeaponBox;
        else if (p.enemy != null) p.kind = BalanceKind.Enemy;
        else if (p.treasure != null) p.kind = BalanceKind.Treasure;
        else p.kind = BalanceKind.Other;
        return p;
    }

    // Info de um prefab qualquer (um prefab de spawner pode estar fora da pasta escaneada).
    public static BalancePrefab Info(GameObject prefab)
    {
        if (prefab == null) return null;
        BalancePrefab info;
        if (byRoot.TryGetValue(prefab, out info)) return info;
        info = Build(prefab, AssetDatabase.GetAssetPath(prefab));
        byRoot[prefab] = info;
        return info;
    }

    // ---------- Armas ----------

    public static float BulletDamage(WeaponData w)
    {
        if (w == null || w.bulletPrefab == null) return 0f;
        bullet2D b = w.bulletPrefab.GetComponent<bullet2D>();
        return b != null ? b.damage : 0f;
    }

    public static float RawDps(WeaponData w, BalanceAssumptions a)
    {
        if (w == null) return 0f;
        float shotsPerSecond = 1f / Mathf.Max(0.01f, w.fireRate);
        float pellets = Mathf.Max(1, w.numberOfProjectiles);
        float hitting = pellets > 1f ? Mathf.Max(1f, pellets * a.pelletHitFraction) : 1f;
        return shotsPerSecond * hitting * BulletDamage(w);
    }

    // Quantos segundos a arma dura na mão (infinito = nunca acaba).
    public static float HoldSeconds(WeaponData w)
    {
        if (w == null) return 0f;
        switch (w.limitMode)
        {
            case WeaponData.LimitMode.Time: return w.durationSeconds;
            case WeaponData.LimitMode.Ammo: return w.ammo * Mathf.Max(0.01f, w.fireRate);
        }
        return float.PositiveInfinity;
    }

    // ---------- Player ----------

    public static float PlayerMaxHp()
    {
        healthSystem live = LivePlayerHealth();
        if (live != null) return live.maxHealth;
        if (playerPrefab != null)
        {
            healthSystem h = playerPrefab.GetComponent<healthSystem>();
            if (h != null) return h.maxHealth;
        }
        return 10f;
    }

    public static WeaponData DefaultWeapon()
    {
        Player2D live = UnityEngine.Object.FindFirstObjectByType<Player2D>(FindObjectsInactive.Include);
        if (live != null && live.defaultWeapon != null) return live.defaultWeapon;
        if (playerPrefab != null)
        {
            Player2D p = playerPrefab.GetComponent<Player2D>();
            if (p != null) return p.defaultWeapon;
        }
        return null;
    }

    static healthSystem LivePlayerHealth()
    {
        Player2D live = UnityEngine.Object.FindFirstObjectByType<Player2D>(FindObjectsInactive.Include);
        return live != null ? live.GetComponent<healthSystem>() : null;
    }

    // ---------- Simulação das waves ----------

    public static List<WaveStats> ComputeAll(WaveSource wm, BalanceAssumptions a)
    {
        List<WaveStats> list = new List<WaveStats>();
        if (wm == null) return list;

        float maxHp = PlayerMaxHp();
        float defaultDps = RawDps(DefaultWeapon(), a);
        float hp = maxHp;
        float fixedStep = Mathf.Max(0.001f, Time.fixedDeltaTime);

        // Waves limitadas (ou planilha): o gráfico tem exatamente 'totalWaves' waves.
        int waveCount = (wm.useMatrix || wm.limitWaves) ? Mathf.Max(1, wm.totalWaves) : Mathf.Max(1, a.waves);

        for (int n = 1; n <= waveCount; n++)
        {
            WaveStats s = new WaveStats();
            s.wave = n;
            s.budget = wm.useMatrix ? MatrixSpawnCount(wm, n, MatrixPhase.Wave) : Mathf.Max(0, wm.startingBudget + (n - 1) * wm.budgetIncrementPerWave);

            // O spawn roda no FixedUpdate: o intervalo real é arredondado para cima ao próximo passo de física.
            float period = Mathf.Max(fixedStep, Mathf.Ceil(wm.secondsBetweenSpawnsInWave / fixedStep - 0.001f) * fixedStep);
            s.spawnSeconds = s.budget * period;

            int bonusRounds = 0;
            int matrixLootCount = wm.useMatrix ? MatrixSpawnCount(wm, n, MatrixPhase.Loot) : 0;
            if (wm.loot != null && (!wm.useMatrix || matrixLootCount > 0))
            {
                // Fase de loot: dura no máximo 'durationSeconds'; com fim antecipado, estimamos janela de spawn + ~2,5 s para pegar.
                float maxDuration = Mathf.Max(1f, wm.loot.durationSeconds);
                float window = maxDuration * Mathf.Clamp(wm.loot.dropWindowFraction, 0.3f, 1f);

                // Os itens nascem com pelo menos 'minSecondsBetweenDrops' entre eles: com muitos itens isso passa da janela.
                int plannedDrops = wm.useMatrix ? matrixLootCount : CountLootItems(wm.loot.healItems) + CountLootItems(wm.loot.weaponItems) + CountLootItems(wm.loot.performanceItems);
                float spawnTime = Mathf.Max(window, Mathf.Max(0, plannedDrops - 1) * Mathf.Max(0.05f, wm.loot.minSecondsBetweenDrops));
                s.gapSeconds = wm.loot.endWhenAllCollected
                    ? Mathf.Max(spawnTime + 2.5f, wm.loot.minSecondsBeforeEarlyEnd)
                    : Mathf.Max(maxDuration, spawnTime);
                if (wm.loot.waitForEnemiesToClear) s.gapSeconds += 2f; // espera estimada dos últimos inimigos
            }
            else if (wm.secondsBetweenWaves > 0f)
            {
                if (wm.secondsBetweenBonusSpawns > 0.0001f)
                {
                    bonusRounds = Mathf.CeilToInt(wm.secondsBetweenWaves / wm.secondsBetweenBonusSpawns - 0.001f);
                    s.gapSeconds = bonusRounds * wm.secondsBetweenBonusSpawns;
                }
                else
                {
                    s.gapSeconds = wm.secondsBetweenWaves;
                }
            }
            s.totalSeconds = s.spawnSeconds + s.gapSeconds;

            List<WeaponData> pool = new List<WeaponData>();

            float totalWeight = 0f;
            List<WaveRangeData> active = new List<WaveRangeData>();
            foreach (WaveRangeData r in wm.ranges)
            {
                if (r == null || r.prefab == null) continue;
                if (n < r.startWave || n > r.endWave || r.weight <= 0f) continue;
                active.Add(r);
                totalWeight += r.weight;
            }
            if (wm.useMatrix)
            {
                AccumulateCells(s, wm, n, MatrixPhase.Wave, pool);
            }
            else
            {
                foreach (WaveRangeData r in active)
                {
                    Accumulate(s, r.prefab, s.budget * r.weight / totalWeight, pool);
                }
            }

            if (wm.bonusPrefabs.Count > 0 && bonusRounds > 0 && !wm.useMatrix)
            {
                float each = bonusRounds / (float)wm.bonusPrefabs.Count;
                foreach (GameObject bonusPrefab in wm.bonusPrefabs)
                {
                    if (bonusPrefab != null) Accumulate(s, bonusPrefab, each, pool);
                }
            }

            // Fase de loot: cura e armas fixas (o que depende do desempenho entra mais abaixo).
            WaveStats ls = new WaveStats();
            List<WeaponData> lootPool = new List<WeaponData>();
            if (wm.loot != null)
            {
                if (wm.useMatrix)
                {
                    AccumulateCells(ls, wm, n, MatrixPhase.Loot, lootPool);
                }
                else
                {
                    AccumulateItems(ls, wm.loot.healItems, 1f, lootPool);
                    int every = Mathf.Max(1, wm.loot.weaponEveryNWaves);
                    if (n >= wm.loot.weaponFirstWave && (n - wm.loot.weaponFirstWave) % every == 0)
                    {
                        AccumulateItems(ls, wm.loot.weaponItems, 1f, lootPool);
                    }
                }
                pool.AddRange(lootPool);
            }

            // Armas especiais: quanto do tempo o player passa com uma arma de caixa na mão.
            float avgHold = 0f;
            float avgDps = 0f;
            foreach (WeaponData w in pool)
            {
                float hold = HoldSeconds(w);
                if (float.IsInfinity(hold)) hold = s.totalSeconds;
                avgHold += hold;
                avgDps += RawDps(w, a);
            }
            if (pool.Count > 0) { avgHold /= pool.Count; avgDps /= pool.Count; }

            s.specialFraction = pool.Count > 0 && s.totalSeconds > 0f
                ? Mathf.Clamp01((s.weaponBoxes * a.pickupCatchRate + ls.weaponBoxes * a.lootCatchRate) * avgHold / s.totalSeconds)
                : 0f;
            float specialDps = pool.Count > 0 ? avgDps : defaultDps;
            s.effectiveDps = a.accuracy * ((1f - s.specialFraction) * defaultDps + s.specialFraction * specialDps);

            s.timeToKill = s.effectiveDps > 0.0001f ? s.enemyHp / s.effectiveDps : (s.enemyHp > 0f ? 999f : 0f);
            s.killPressure = s.totalSeconds > 0f ? s.timeToKill / s.totalSeconds : 0f;

            float killed = s.killPressure > 1f ? 1f / s.killPressure : 1f;
            float leaked = Mathf.Clamp01(a.baseLeakFraction + (1f - a.baseLeakFraction) * (1f - killed));
            s.expectedDamage = s.contactDamage * leaked + s.explosionDamage * a.explosionHitsPlayerChance;

            if (wm.loot != null && !wm.useMatrix)
            {
                // Recompensa por desempenho: quanto menos dano, mais itens (igual ao WaveManager).
                float performance = maxHp > 0f ? Mathf.Clamp01(1f - s.expectedDamage / maxHp) : 1f;
                AccumulateItems(ls, wm.loot.performanceItems, performance, lootPool);
                if (s.expectedDamage <= 0.5f && wm.loot.flawlessBonusCount > 0)
                {
                    GameObject reward = FirstLootPrefab(wm.loot.performanceItems);
                    if (reward != null) Accumulate(ls, reward, wm.loot.flawlessBonusCount, lootPool);
                }

                // Cura extra se a vida projetada ao fim do combate está baixa.
                float hpAfterCombat = Mathf.Clamp(Mathf.Min(maxHp, hp + s.healTotal * a.pickupCatchRate) - s.expectedDamage, 0f, maxHp);
                if (maxHp > 0f && hpAfterCombat / maxHp < wm.loot.lowHealthThreshold && wm.loot.extraHealsWhenLow > 0)
                {
                    GameObject medkit = FirstLootPrefab(wm.loot.healItems);
                    if (medkit != null) Accumulate(ls, medkit, wm.loot.extraHealsWhenLow, lootPool);
                }
            }

            s.expectedHeal = s.healTotal * a.pickupCatchRate + ls.healTotal * a.lootCatchRate;
            s.healTotal += ls.healTotal;
            s.medkits += ls.medkits;
            s.treasures += ls.treasures;
            s.score += ls.score;
            s.weaponBoxes += ls.weaponBoxes;
            s.lootBoxes = ls.weaponBoxes;

            hp = Mathf.Clamp(Mathf.Min(maxHp, hp + s.expectedHeal) - s.expectedDamage, 0f, maxHp);
            s.hpEnd = hp;

            list.Add(s);
        }
        return list;
    }

    // Quantos objetos a planilha manda nascer nesta wave/fase (soma de todas as células).
    public static int MatrixSpawnCount(WaveSource wm, int wave, MatrixPhase phase)
    {
        int total = 0;
        foreach (WaveMatrixCell cell in wm.cells)
        {
            if (cell != null && cell.prefab != null && cell.wave == wave && cell.phase == phase) total += Mathf.Max(0, cell.count);
        }
        return total;
    }

    static void AccumulateCells(WaveStats s, WaveSource wm, int wave, MatrixPhase phase, List<WeaponData> pool)
    {
        foreach (WaveMatrixCell cell in wm.cells)
        {
            if (cell == null || cell.prefab == null || cell.wave != wave || cell.phase != phase || cell.count <= 0) continue;

            if (cell.weapon != null)
            {
                // Arma forçada: a caixa dá sempre essa arma, não a lista inteira dela.
                Accumulate(s, cell.prefab, cell.count, new List<WeaponData>());
                BalancePrefab info = Info(cell.prefab);
                if (info != null && info.HasWeaponList) pool.Add(cell.weapon);
            }
            else
            {
                Accumulate(s, cell.prefab, cell.count, pool);
            }
        }
    }

    static int CountLootItems(LootItem[] items)
    {
        int total = 0;
        if (items == null) return 0;
        foreach (LootItem item in items) if (item != null && item.prefab != null) total += Mathf.Max(0, item.count);
        return total;
    }

    static void AccumulateItems(WaveStats s, LootItem[] items, float multiplier, List<WeaponData> pool)
    {
        if (items == null) return;
        foreach (LootItem item in items)
        {
            if (item == null || item.prefab == null) continue;
            Accumulate(s, item.prefab, item.count * multiplier, pool);
        }
    }

    static GameObject FirstLootPrefab(LootItem[] items)
    {
        if (items == null) return null;
        foreach (LootItem item in items) if (item != null && item.prefab != null) return item.prefab;
        return null;
    }

    static void Accumulate(WaveStats s, GameObject prefab, float count, List<WeaponData> pool)
    {
        BalancePrefab info = Info(prefab);
        if (info == null || count <= 0f) return;

        float maxHealth = info.health != null ? info.health.maxHealth : 0f;

        switch (info.kind)
        {
            case BalanceKind.Enemy:
                s.enemies += count;
                s.enemyHp += count * maxHealth;
                s.contactDamage += count * (info.enemy != null ? info.enemy.damage : 0f);
                if (info.explosion != null) s.explosionDamage += count * info.explosion.damage;
                break;
            case BalanceKind.Treasure:
                s.treasures += count;
                s.score += count * (info.treasure != null ? info.treasure.scoreValue : 0);
                break;
            case BalanceKind.Medkit:
                s.medkits += count;
                s.healTotal += count * (info.medkit != null ? info.medkit.healAmount : 0f);
                break;
        }

        // Caixa de arma (ou medkit que também tem lista de armas).
        if (info.HasWeaponList)
        {
            s.weaponBoxes += count;
            foreach (WeaponData w in info.weaponBox.possibleWeapons)
            {
                if (w != null) pool.Add(w);
            }
        }
    }
}
