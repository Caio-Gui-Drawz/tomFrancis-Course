using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class PresetEntry
{
    public string key;
    public string path;
    public string kind;
    public string value;
    public string label;
}

[Serializable]
public class PresetWave
{
    public int wave;
    public float killPressure;
    public float expectedDamage;
    public float expectedHeal;
    public float hpEnd;
    public float enemyHp;
    public float totalSeconds;
}

[Serializable]
public class PresetData
{
    public string name;
    public string savedAt;
    public List<PresetEntry> entries = new List<PresetEntry>();
    public List<PresetWave> waves = new List<PresetWave>();

    [NonSerialized] public string filePath;

    // Curva guardada junto do preset, no mesmo formato que o gráfico usa.
    public List<WaveStats> ToStats()
    {
        List<WaveStats> list = new List<WaveStats>();
        foreach (PresetWave w in waves)
        {
            WaveStats s = new WaveStats();
            s.wave = w.wave;
            s.killPressure = w.killPressure;
            s.expectedDamage = w.expectedDamage;
            s.expectedHeal = w.expectedHeal;
            s.hpEnd = w.hpEnd;
            s.enemyHp = w.enemyHp;
            s.totalSeconds = w.totalSeconds;
            list.Add(s);
        }
        return list;
    }
}

// Um valor editável encontrado no projeto (usado para salvar, aplicar e comparar presets).
public class FieldRef
{
    public UnityEngine.Object target;
    public string path;
    public string key;
    public string label;
    public string kind;
    public string value;
}

public class PresetDiff
{
    public string label;
    public string current;
    public string preset;
}

public static class BalancePresets
{
    public const string Folder = "Assets/_SpacePirates/BalancePresets";

    static readonly string[] WavePaths =
    {
        "startingBudget", "budgetIncrementPerWave", "secondsBetweenSpawnsInWave",
        "secondsBetweenWaves", "secondsBeforeFirstWave", "secondsBetweenBonusSpawns",
        // Fase de loot (só existe no WaveConfig; no modo legado o campo não é encontrado e é ignorado).
        "lootPhase.enabled", "lootPhase.durationSeconds", "lootPhase.dropWindowFraction",
        "lootPhase.endWhenAllCollected", "lootPhase.minSecondsBetweenDrops", "lootPhase.safetyTimeoutSeconds",
        "lootPhase.waitForEnemiesToClear", "lootPhase.clearEnemiesTimeoutSeconds", "lootPhase.minSecondsBeforeEarlyEnd", "lootPhase.itemSpeedMultiplier",
        "lootPhase.lowHealthThreshold", "lootPhase.extraHealsWhenLow",
        "lootPhase.weaponFirstWave", "lootPhase.weaponEveryNWaves", "lootPhase.flawlessBonusCount"
    };

    static readonly string[] WeaponPaths =
    {
        "fireRate", "numberOfProjectiles", "spreadAngleDegrees", "limitMode", "durationSeconds", "ammo"
    };

    static readonly string[] BulletPaths =
    {
        "damage", "bulletSpeed", "secondsUntilDestroy", "knockbackForce", "knockbackDuration"
    };

    static readonly string[] ExplosionPaths = { "damage", "radius", "knockbackForce", "knockbackDuration" };

    // ---------------------------------------------------------------- campos

    public static List<FieldRef> EnumerateFields()
    {
        List<FieldRef> list = new List<FieldRef>();
        HashSet<string> seen = new HashSet<string>();

        // Waves guardadas em assets WaveConfig: valem em qualquer cena.
        foreach (WaveConfig cfg in BalanceModel.waveConfigs)
        {
            AddPaths(list, seen, cfg, "Waves (" + cfg.name + ")", WavePaths);
            if (cfg.lootPhase != null)
            {
                AddLootCounts(list, seen, cfg, "healItems", "Loot cura", cfg.lootPhase.healItems);
                AddLootCounts(list, seen, cfg, "weaponItems", "Loot arma", cfg.lootPhase.weaponItems);
                AddLootCounts(list, seen, cfg, "performanceItems", "Loot desempenho", cfg.lootPhase.performanceItems);
            }
            if (cfg.ranges == null) continue;
            for (int i = 0; i < cfg.ranges.Length; i++)
            {
                string prefabName = cfg.ranges[i] != null && cfg.ranges[i].prefab != null ? cfg.ranges[i].prefab.name : "?";
                string basePath = "ranges.Array.data[" + i + "].";
                AddPaths(list, seen, cfg, "Faixa " + i + " (" + prefabName + ")", new[] { basePath + "startWave", basePath + "endWave", basePath + "weight" });
            }
        }

        // Modo legado: waves guardadas no WaveManager da cena.
        WaveManager wm = UnityEngine.Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (wm != null && wm.config == null)
        {
            AddPaths(list, seen, wm, "WaveManager", WavePaths);
            if (wm.spawnerRanges != null)
            {
                for (int i = 0; i < wm.spawnerRanges.Length; i++)
                {
                    WaveManager.SpawnerRange r = wm.spawnerRanges[i];
                    string prefabName = r != null && r.spawner != null && r.spawner.prefab != null ? r.spawner.prefab.name : "?";
                    string basePath = "spawnerRanges.Array.data[" + i + "].";
                    AddPaths(list, seen, wm, "Faixa " + i + " (" + prefabName + ")", new[] { basePath + "startWave", basePath + "endWave", basePath + "weight" });
                }
            }
        }

        foreach (Spawner sp in UnityEngine.Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sp.gameObject.scene.IsValid()) AddPaths(list, seen, sp, "Spawner " + sp.name, new[] { "offsetRange" });
        }

        foreach (WeaponData w in BalanceModel.weapons)
        {
            AddPaths(list, seen, w, "Arma " + w.name, WeaponPaths);
            bullet2D bullet = w.bulletPrefab != null ? w.bulletPrefab.GetComponent<bullet2D>() : null;
            if (bullet != null) AddPaths(list, seen, bullet, "Bala " + w.bulletPrefab.name, BulletPaths);
        }

        foreach (BalancePrefab p in BalanceModel.prefabs)
        {
            AddPaths(list, seen, p.health, p.Name, new[] { "maxHealth" });
            if (p.enemy != null) AddPaths(list, seen, p.enemy, p.Name, new[] { "damage" });
            if (p.knockback != null) AddPaths(list, seen, p.knockback, p.Name, new[] { "resistance" });
            if (p.medkit != null) AddPaths(list, seen, p.medkit, p.Name, new[] { "healAmount" });
            if (p.treasure != null) AddPaths(list, seen, p.treasure, p.Name, new[] { "scoreValue" });
            if (p.weaponBox != null) AddPaths(list, seen, p.weaponBox, p.Name, new[] { "avoidCurrentWeapon", "avoidLastBoxWeapon" });
            if (p.explosion != null) AddPaths(list, seen, p.explosion, "Explosão de " + p.Name, ExplosionPaths);

            foreach (IMovementBehavior m in p.root.GetComponentsInChildren<IMovementBehavior>(true))
            {
                Component c = m as Component;
                if (c != null) AddPaths(list, seen, c, p.Name + " (" + c.GetType().Name + ")", SimplePaths(c));
            }
        }

        if (BalanceModel.playerPrefab != null)
        {
            healthSystem playerHealth = BalanceModel.playerPrefab.GetComponent<healthSystem>();
            if (playerHealth != null) AddPaths(list, seen, playerHealth, "Player", new[] { "maxHealth" });
        }

        return list;
    }

    // Caminhos dos números simples (float/int/bool/enum/vector2) de um componente.
    public static string[] SimplePaths(UnityEngine.Object target)
    {
        List<string> paths = new List<string>();
        SerializedObject so = new SerializedObject(target);
        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;
            if (it.propertyType == SerializedPropertyType.Float || it.propertyType == SerializedPropertyType.Integer ||
                it.propertyType == SerializedPropertyType.Boolean || it.propertyType == SerializedPropertyType.Enum ||
                it.propertyType == SerializedPropertyType.Vector2)
            {
                paths.Add(it.propertyPath);
            }
        }
        return paths.ToArray();
    }

    static void AddLootCounts(List<FieldRef> list, HashSet<string> seen, WaveConfig cfg, string arrayName, string label, LootItem[] items)
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            string itemName = items[i] != null && items[i].prefab != null ? items[i].prefab.name : "?";
            AddPaths(list, seen, cfg, label + " " + i + " (" + itemName + ")", new[] { "lootPhase." + arrayName + ".Array.data[" + i + "].count" });
        }
    }

    static void AddPaths(List<FieldRef> list, HashSet<string> seen, UnityEngine.Object target, string labelPrefix, string[] paths)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        string targetKey = TargetKey(target);

        foreach (string path in paths)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop == null) continue;

            string kind;
            string value;
            if (!BalanceEdit.Encode(prop, out kind, out value)) continue;

            string key = targetKey + "#" + path;
            if (!seen.Add(key)) continue;

            FieldRef f = new FieldRef();
            f.target = target;
            f.path = path;
            f.key = key;
            f.kind = kind;
            f.value = value;
            f.label = labelPrefix + " · " + ObjectNames.NicifyVariableName(path.Substring(path.LastIndexOf('.') + 1));
            list.Add(f);
        }
    }

    static string TargetKey(UnityEngine.Object target)
    {
        Component c = target as Component;
        if (c != null)
        {
            if (EditorUtility.IsPersistent(c))
            {
                return AssetDatabase.GetAssetPath(c) + "|" + RelativePath(c.transform) + "|" + c.GetType().Name;
            }
            return "scene|" + HierarchyPath(c.transform) + "|" + c.GetType().Name;
        }
        return AssetDatabase.GetAssetPath(target) + "|" + target.GetType().Name;
    }

    static string RelativePath(Transform t)
    {
        if (t.parent == null) return "";
        string s = t.name;
        Transform p = t.parent;
        while (p.parent != null)
        {
            s = p.name + "/" + s;
            p = p.parent;
        }
        return s;
    }

    static string HierarchyPath(Transform t)
    {
        string s = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }

    // ---------------------------------------------------------------- capturar / aplicar / diferenças

    public static PresetData Capture(string presetName, List<WaveStats> stats)
    {
        PresetData d = new PresetData();
        d.name = presetName;
        d.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        foreach (FieldRef f in EnumerateFields())
        {
            PresetEntry e = new PresetEntry();
            e.key = f.key;
            e.path = f.path;
            e.kind = f.kind;
            e.value = f.value;
            e.label = f.label;
            d.entries.Add(e);
        }

        if (stats != null)
        {
            foreach (WaveStats s in stats)
            {
                PresetWave w = new PresetWave();
                w.wave = s.wave;
                w.killPressure = s.killPressure;
                w.expectedDamage = s.expectedDamage;
                w.expectedHeal = s.expectedHeal;
                w.hpEnd = s.hpEnd;
                w.enemyHp = s.enemyHp;
                w.totalSeconds = s.totalSeconds;
                d.waves.Add(w);
            }
        }
        return d;
    }

    // Aplica os valores do preset nos objetos atuais. Retorna quantos mudaram e quantos não foram encontrados.
    public static void Apply(PresetData d, out int applied, out int skipped)
    {
        applied = 0;
        skipped = 0;

        Dictionary<string, FieldRef> map = new Dictionary<string, FieldRef>();
        foreach (FieldRef f in EnumerateFields()) map[f.key] = f;

        foreach (PresetEntry e in d.entries)
        {
            FieldRef f;
            if (!map.TryGetValue(e.key, out f)) { skipped++; continue; }
            if (f.value == e.value) continue;

            SerializedObject so = new SerializedObject(f.target);
            SerializedProperty prop = so.FindProperty(f.path);
            if (prop == null || !BalanceEdit.Decode(prop, e.kind, e.value)) { skipped++; continue; }

            so.ApplyModifiedProperties();
            BalanceEdit.AfterEdit(so, f.path);
            applied++;
        }
    }

    public static List<PresetDiff> Diff(PresetData d, out int missing)
    {
        missing = 0;
        List<PresetDiff> diffs = new List<PresetDiff>();

        Dictionary<string, FieldRef> map = new Dictionary<string, FieldRef>();
        foreach (FieldRef f in EnumerateFields()) map[f.key] = f;

        foreach (PresetEntry e in d.entries)
        {
            FieldRef f;
            if (!map.TryGetValue(e.key, out f)) { missing++; continue; }
            if (f.value == e.value) continue;

            PresetDiff diff = new PresetDiff();
            diff.label = e.label;
            diff.current = f.value;
            diff.preset = e.value;
            diffs.Add(diff);
        }
        return diffs;
    }

    // ---------------------------------------------------------------- arquivos

    static string FullFolder
    {
        get { return Path.Combine(Application.dataPath, "_SpacePirates/BalancePresets"); }
    }

    public static string SafeFileName(string presetName)
    {
        string s = string.IsNullOrEmpty(presetName) ? "preset" : presetName.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "_");
        return s.Length == 0 ? "preset" : s;
    }

    public static bool Exists(string presetName)
    {
        return File.Exists(Path.Combine(FullFolder, SafeFileName(presetName) + ".json"));
    }

    public static void Save(PresetData d)
    {
        Directory.CreateDirectory(FullFolder);
        string path = Path.Combine(FullFolder, SafeFileName(d.name) + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(d, true));
        AssetDatabase.Refresh();
    }

    public static List<PresetData> LoadAll()
    {
        List<PresetData> list = new List<PresetData>();
        if (!Directory.Exists(FullFolder)) return list;

        foreach (string file in Directory.GetFiles(FullFolder, "*.json"))
        {
            try
            {
                PresetData d = JsonUtility.FromJson<PresetData>(File.ReadAllText(file));
                if (d == null) continue;
                d.filePath = file;
                if (string.IsNullOrEmpty(d.name)) d.name = Path.GetFileNameWithoutExtension(file);
                list.Add(d);
            }
            catch (Exception)
            {
                // arquivo inválido: ignora
            }
        }

        list.Sort((a, b) => string.Compare(b.savedAt, a.savedAt, StringComparison.Ordinal));
        return list;
    }

    public static void Delete(PresetData d)
    {
        if (string.IsNullOrEmpty(d.filePath)) return;
        string assetPath = Folder + "/" + Path.GetFileName(d.filePath);
        if (!AssetDatabase.DeleteAsset(assetPath))
        {
            File.Delete(d.filePath);
            AssetDatabase.Refresh();
        }
    }
}
