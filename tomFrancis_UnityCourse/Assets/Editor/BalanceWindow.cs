using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Janela de balanceamento: Tools > Cosmo Treasure > Balance Window
// Edita direto os valores do jogo (WaveManager na cena, prefabs, ScriptableObjects de arma) e mostra
// uma estimativa da curva de dificuldade por wave. Veja BalanceEdit.cs para como o Play mode é tratado.
public class BalanceWindow : EditorWindow
{
    enum Tab { Waves, Armas, Inimigos, Itens, Presets, Mudancas }

    static readonly string[] TabNames = { "Waves", "Armas", "Inimigos", "Itens & Player", "Presets", "Mudanças" };

    static readonly string[] SeriesNames =
    {
        "Pressão de abate (tempo p/ matar ÷ duração)",
        "Dano esperado ao player",
        "Cura esperada",
        "HP projetado do player",
        "Vida total dos inimigos",
        "Duração da wave (s)"
    };

    static readonly Color[] SeriesColors =
    {
        new Color(1f, 0.65f, 0.15f),
        new Color(1f, 0.3f, 0.3f),
        new Color(0.35f, 0.9f, 0.4f),
        new Color(0.3f, 0.8f, 1f),
        new Color(0.7f, 0.7f, 0.75f),
        new Color(0.9f, 0.9f, 0.4f)
    };

    const string AssumePref = "CosmoBalance.assumptions";

    Tab tab;
    Vector2 scroll;
    WaveManager waveManager;
    double nextFindTime;
    bool needRefresh;
    double lastRefresh;

    readonly Dictionary<UnityEngine.Object, SerializedObject> soCache = new Dictionary<UnityEngine.Object, SerializedObject>();
    readonly HashSet<string> openFoldouts = new HashSet<string>();

    BalanceAssumptions assume = new BalanceAssumptions();
    bool showAssumptions;
    bool normalize = true;
    bool[] seriesOn = { true, true, true, true, false, false };
    int previewWave = 1;

    Spawner[] sceneSpawners = new Spawner[0];
    List<PresetData> presets;
    PresetData compareTo;
    PresetData diffPreset;
    string presetName = "novo preset";
    string presetMessage = "";
    List<PresetDiff> cachedDiffs;
    int cachedMissing;
    double cachedDiffAt;
    PresetData cachedDiffFor;

    [MenuItem("Tools/Cosmo Treasure/Balance Window")]
    static void Open()
    {
        BalanceWindow window = GetWindow<BalanceWindow>("Balance");
        window.minSize = new Vector2(620f, 420f);
        window.Show();
    }

    void OnEnable()
    {
        LoadAssumptions();
        BalanceModel.Refresh();
        lastRefresh = EditorApplication.timeSinceStartup;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= Repaint;
        SaveAssumptions();
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        soCache.Clear();
        waveManager = null;
        needRefresh = true;
        Repaint();
    }

    void OnProjectChange()
    {
        needRefresh = true;
    }

    void OnInspectorUpdate()
    {
        // Em Play mode os valores mudam sozinhos (ex.: vida); mantém a janela viva.
        if (Application.isPlaying) Repaint();
    }

    void LoadAssumptions()
    {
        string json = EditorPrefs.GetString(AssumePref, "");
        if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, assume);
    }

    void SaveAssumptions()
    {
        EditorPrefs.SetString(AssumePref, JsonUtility.ToJson(assume));
    }

    // ---------------------------------------------------------------- helpers

    SerializedObject GetSO(UnityEngine.Object target)
    {
        SerializedObject so;
        if (!soCache.TryGetValue(target, out so) || so == null || so.targetObject == null)
        {
            so = new SerializedObject(target);
            soCache[target] = so;
        }
        else
        {
            so.Update();
        }
        return so;
    }

    // Desenha um campo e, se mudou, aplica e dispara o pipeline (salvar / espelhar nos vivos / keep).
    bool Field(UnityEngine.Object target, string path, string label, float width = 0f)
    {
        if (target == null) return false;

        SerializedObject so = GetSO(target);
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null)
        {
            EditorGUILayout.LabelField(label, "(campo não encontrado: " + path + ")");
            return false;
        }

        EditorGUI.BeginChangeCheck();
        if (width > 0f) EditorGUILayout.PropertyField(prop, new GUIContent(label), true, GUILayout.Width(width));
        else EditorGUILayout.PropertyField(prop, new GUIContent(label), true);

        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            BalanceEdit.AfterEdit(so, path);
            return true;
        }
        return false;
    }

    bool Foldout(string key, string title)
    {
        bool open = openFoldouts.Contains(key);
        bool now = EditorGUILayout.Foldout(open, title, true);
        if (now != open)
        {
            if (now) openFoldouts.Add(key);
            else openFoldouts.Remove(key);
        }
        return now;
    }

    static string KindTag(BalanceKind kind)
    {
        switch (kind)
        {
            case BalanceKind.Enemy: return "Inimigo";
            case BalanceKind.Treasure: return "Tesouro";
            case BalanceKind.Medkit: return "Medkit";
            case BalanceKind.WeaponBox: return "Caixa de arma";
        }
        return "Outro";
    }

    static void Header(string text)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
    }

    // Procura o WaveManager e os Spawners da cena (no máximo 1x por segundo).
    void FindWaveManager()
    {
        if (EditorApplication.timeSinceStartup < nextFindTime && waveManager != null) return;
        if (EditorApplication.timeSinceStartup < nextFindTime) return;

        nextFindTime = EditorApplication.timeSinceStartup + 1.0;
        waveManager = FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        sceneSpawners = FindObjectsByType<Spawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    // ---------------------------------------------------------------- GUI

    void OnGUI()
    {
        if (needRefresh && EditorApplication.timeSinceStartup - lastRefresh > 1.0)
        {
            needRefresh = false;
            lastRefresh = EditorApplication.timeSinceStartup;
            BalanceModel.Refresh();
        }

        DrawToolbar();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case Tab.Waves: DrawWaves(); break;
            case Tab.Armas: DrawWeapons(); break;
            case Tab.Inimigos: DrawEnemies(); break;
            case Tab.Itens: DrawItems(); break;
            case Tab.Presets: DrawPresets(); break;
            case Tab.Mudancas: DrawChanges(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tab = (Tab)GUILayout.Toolbar((int)tab, TabNames, EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Recarregar", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                soCache.Clear();
                waveManager = null;
                BalanceModel.Refresh();
            }
            if (GUILayout.Button("Salvar assets", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                BalanceEdit.SaveNow();
            }
        }

        string status;
        if (Application.isPlaying)
        {
            status = BalanceEdit.KeepPlayChanges
                ? "PLAY MODE: assets (armas, prefabs) já ficam salvos e valem nos objetos vivos. Mudanças em objetos de cena (WaveManager) serão reaplicadas ao sair do Play."
                : "PLAY MODE: 'Manter mudanças' está DESLIGADO, então mudanças em objetos de cena serão perdidas ao sair do Play (aba Mudanças).";
        }
        else
        {
            status = "Edição normal. Mudanças em assets são salvas sozinhas; objetos de cena ficam na cena (salve a cena).";
        }
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // ---------------------------------------------------------------- Waves

    void DrawWaves()
    {
        FindWaveManager();
        if (waveManager == null)
        {
            EditorGUILayout.HelpBox("Nenhum WaveManager na cena aberta. Abra a cena do jogo (a 2D) para editar e visualizar as waves.", MessageType.Info);
            return;
        }

        Header("Ritmo das waves  (WaveManager)");
        Field(waveManager, "startingBudget", "Spawns na wave 1");
        Field(waveManager, "budgetIncrementPerWave", "+ spawns por wave");
        Field(waveManager, "secondsBetweenSpawnsInWave", "Segundos entre spawns");
        Field(waveManager, "secondsBetweenWaves", "Intervalo entre waves (s)");
        Field(waveManager, "secondsBeforeFirstWave", "Espera antes da wave 1 (s)");
        Field(waveManager, "secondsBetweenBonusSpawns", "Segundos entre spawns bônus");

        Header("Quem nasce em cada wave  (Spawner Ranges)");
        SerializedObject so = GetSO(waveManager);
        SerializedProperty ranges = so.FindProperty("spawnerRanges");
        int rangeCount = ranges != null ? ranges.arraySize : 0;
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        for (int i = 0; i < rangeCount; i++)
        {
            WaveManager.SpawnerRange r = waveManager.spawnerRanges[i];
            string spawnerName = "(vazio)";
            string tag = "";
            if (r != null && r.spawner != null && r.spawner.prefab != null)
            {
                spawnerName = r.spawner.prefab.name;
                tag = "  [" + KindTag(BalanceModel.Info(r.spawner.prefab).kind) + "]";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(spawnerName + tag, GUILayout.Width(200f));
                EditorGUIUtility.labelWidth = 42f;
                Field(waveManager, "spawnerRanges.Array.data[" + i + "].startWave", "início", 95f);
                Field(waveManager, "spawnerRanges.Array.data[" + i + "].endWave", "fim", 95f);
                Field(waveManager, "spawnerRanges.Array.data[" + i + "].weight", "peso", 95f);
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }
        EditorGUIUtility.labelWidth = oldLabelWidth;

        DrawSpawners();

        DrawComposition();

        showAssumptions = EditorGUILayout.Foldout(showAssumptions, "Premissas da simulação (afetam só o gráfico)", true);
        if (showAssumptions) DrawAssumptions();

        List<WaveStats> stats = BalanceModel.ComputeAll(waveManager, assume);
        List<WaveStats> compareStats = compareTo != null ? compareTo.ToStats() : null;

        Header("Curva de dificuldade estimada");
        if (compareTo != null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Comparando (linhas pontilhadas) com: " + compareTo.name + "  (salvo em " + compareTo.savedAt + ")");
                if (GUILayout.Button("Parar de comparar", GUILayout.Width(130f))) compareTo = null;
            }
        }
        DrawSeriesToggles();
        Rect chartRect = GUILayoutUtility.GetRect(100f, 240f, GUILayout.ExpandWidth(true));
        DrawChart(chartRect, stats, compareStats);

        DrawTable(stats);
        DrawLossHint(stats, compareStats);
    }

    void DrawSpawners()
    {
        if (sceneSpawners == null || sceneSpawners.Length == 0) return;

        if (!Foldout("spawners", "Spawners da cena (" + sceneSpawners.Length + ")")) return;

        EditorGUI.indentLevel++;
        foreach (Spawner sp in sceneSpawners)
        {
            if (sp == null) continue;
            string prefabName = sp.prefab != null ? sp.prefab.name : "(sem prefab)";
            Field(sp, "offsetRange", sp.name + " → " + prefabName);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.HelpBox("O deslocamento aleatório (mín., máx.) é somado à posição do ponto de spawn. Os pontos de spawn em si são objetos da cena: mova-os na Scene.", MessageType.None);
    }

    void DrawComposition()
    {
        previewWave = Mathf.Max(1, EditorGUILayout.IntSlider("Composição na wave", previewWave, 1, Mathf.Max(2, assume.waves)));

        float totalWeight = 0f;
        List<WaveManager.SpawnerRange> active = new List<WaveManager.SpawnerRange>();
        if (waveManager.spawnerRanges != null)
        {
            foreach (WaveManager.SpawnerRange r in waveManager.spawnerRanges)
            {
                if (r == null || r.spawner == null || r.spawner.prefab == null) continue;
                if (previewWave < r.startWave || previewWave > r.endWave || r.weight <= 0f) continue;
                active.Add(r);
                totalWeight += r.weight;
            }
        }

        int budget = Mathf.Max(0, waveManager.startingBudget + (previewWave - 1) * waveManager.budgetIncrementPerWave);
        if (active.Count == 0)
        {
            EditorGUILayout.LabelField("Nada nasce nessa wave (nenhum spawner ativo).");
            return;
        }

        foreach (WaveManager.SpawnerRange r in active)
        {
            float chance = r.weight / totalWeight;
            BalancePrefab info = BalanceModel.Info(r.spawner.prefab);
            EditorGUILayout.LabelField("   " + r.spawner.prefab.name + "  [" + KindTag(info.kind) + "]",
                (chance * 100f).ToString("0.#") + "%  ≈ " + (chance * budget).ToString("0.#") + " de " + budget);
        }
    }

    void DrawAssumptions()
    {
        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        assume.accuracy = EditorGUILayout.Slider(new GUIContent("Precisão do player", "Fração do dano da arma que realmente acerta."), assume.accuracy, 0.05f, 1f);
        assume.pelletHitFraction = EditorGUILayout.Slider(new GUIContent("Projéteis que acertam", "Armas com vários projéteis (escopeta): fração que acerta o mesmo alvo."), assume.pelletHitFraction, 0.05f, 1f);
        assume.pickupCatchRate = EditorGUILayout.Slider(new GUIContent("Chance de pegar itens", "Medkits e caixas de arma que o player realmente pega."), assume.pickupCatchRate, 0f, 1f);
        assume.baseLeakFraction = EditorGUILayout.Slider(new GUIContent("Inimigos que sempre escapam", "Mesmo dominando a wave, essa fração encosta no player."), assume.baseLeakFraction, 0f, 1f);
        assume.explosionHitsPlayerChance = EditorGUILayout.Slider(new GUIContent("Explosão atinge o player", "Chance do dano de explosão (mina) pegar o player."), assume.explosionHitsPlayerChance, 0f, 1f);
        assume.waves = EditorGUILayout.IntSlider("Waves no gráfico", assume.waves, 3, 40);
        if (EditorGUI.EndChangeCheck()) SaveAssumptions();
        EditorGUI.indentLevel--;
    }

    void DrawSeriesToggles()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Color oldColor = GUI.color;
            for (int i = 0; i < SeriesNames.Length; i++)
            {
                GUI.color = seriesOn[i] ? SeriesColors[i] : new Color(1f, 1f, 1f, 0.45f);
                seriesOn[i] = GUILayout.Toggle(seriesOn[i], new GUIContent(ShortName(i), SeriesNames[i]), EditorStyles.miniButton);
            }
            GUI.color = oldColor;
            GUILayout.FlexibleSpace();
            normalize = GUILayout.Toggle(normalize, new GUIContent("Normalizar", "Ligado: cada linha usa a própria escala (0–100%). Desligado: todas na mesma escala."), EditorStyles.miniButton);
        }
    }

    static string ShortName(int i)
    {
        switch (i)
        {
            case 0: return "Pressão abate";
            case 1: return "Dano";
            case 2: return "Cura";
            case 3: return "HP player";
            case 4: return "Vida inimigos";
        }
        return "Duração";
    }

    static float Value(int series, WaveStats w)
    {
        switch (series)
        {
            case 0: return w.killPressure;
            case 1: return w.expectedDamage;
            case 2: return w.expectedHeal;
            case 3: return w.hpEnd;
            case 4: return w.enemyHp;
            case 5: return w.totalSeconds;
        }
        return 0f;
    }

    float SeriesMax(int series, List<WaveStats> stats, List<WaveStats> other)
    {
        float max = 0f;
        foreach (WaveStats w in stats) max = Mathf.Max(max, Value(series, w));
        if (other != null)
        {
            foreach (WaveStats w in other) max = Mathf.Max(max, Value(series, w));
        }
        return max > 0.0001f ? max : 1f;
    }

    void DrawChart(Rect r, List<WaveStats> stats, List<WaveStats> compare)
    {
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.14f, 1f));
        if (stats == null || stats.Count < 2) return;

        Rect plot = new Rect(r.x + 44f, r.y + 10f, r.width - 58f, r.height - 34f);
        int lastWave = stats[stats.Count - 1].wave;
        float xSpan = Mathf.Max(1f, lastWave - 1f);

        float sharedMax = 0f;
        if (!normalize)
        {
            for (int s = 0; s < seriesOn.Length; s++)
            {
                if (seriesOn[s]) sharedMax = Mathf.Max(sharedMax, SeriesMax(s, stats, compare));
            }
            if (sharedMax <= 0f) sharedMax = 1f;
        }

        GUIStyle small = new GUIStyle(EditorStyles.miniLabel);
        small.alignment = TextAnchor.MiddleRight;
        GUIStyle smallCenter = new GUIStyle(EditorStyles.miniLabel);
        smallCenter.alignment = TextAnchor.UpperCenter;

        for (int g = 0; g <= 4; g++)
        {
            float y = plot.yMax - plot.height * g / 4f;
            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.09f);
                Handles.DrawLine(new Vector3(plot.x, y), new Vector3(plot.xMax, y));
            }
            string text = normalize ? (g * 25) + "%" : (sharedMax * g / 4f).ToString("0.#");
            GUI.Label(new Rect(r.x, y - 8f, 40f, 16f), text, small);
        }

        int step = stats.Count > 16 ? 2 : 1;
        for (int i = 0; i < stats.Count; i += step)
        {
            float x = plot.x + plot.width * (stats[i].wave - 1f) / xSpan;
            GUI.Label(new Rect(x - 14f, plot.yMax + 3f, 28f, 16f), stats[i].wave.ToString(), smallCenter);
        }

        if (Event.current.type != EventType.Repaint) return;

        // Linha de referência: pressão de abate = 1 (a partir daqui os inimigos se acumulam).
        if (seriesOn[0])
        {
            float refScale = normalize ? SeriesMax(0, stats, compare) : sharedMax;
            float refValue = 1f / refScale;
            if (refValue <= 1f)
            {
                float y = plot.yMax - plot.height * refValue;
                Handles.color = new Color(SeriesColors[0].r, SeriesColors[0].g, SeriesColors[0].b, 0.5f);
                Handles.DrawDottedLine(new Vector3(plot.x, y), new Vector3(plot.xMax, y), 4f);
            }
        }

        for (int s = 0; s < seriesOn.Length; s++)
        {
            if (!seriesOn[s]) continue;

            float scale = normalize ? SeriesMax(s, stats, compare) : sharedMax;

            Vector3[] points = new Vector3[stats.Count];
            for (int i = 0; i < stats.Count; i++)
            {
                float x = plot.x + plot.width * (stats[i].wave - 1f) / xSpan;
                float y = plot.yMax - plot.height * Mathf.Clamp01(Value(s, stats[i]) / scale);
                points[i] = new Vector3(x, y);
            }
            Handles.color = SeriesColors[s];
            Handles.DrawAAPolyLine(2.5f, points);

            if (compare != null)
            {
                List<Vector3> cmpPoints = new List<Vector3>();
                foreach (WaveStats c in compare)
                {
                    if (c.wave > lastWave) continue;
                    float x = plot.x + plot.width * (c.wave - 1f) / xSpan;
                    float y = plot.yMax - plot.height * Mathf.Clamp01(Value(s, c) / scale);
                    cmpPoints.Add(new Vector3(x, y));
                }

                List<Vector3> segments = new List<Vector3>();
                for (int i = 0; i + 1 < cmpPoints.Count; i++)
                {
                    segments.Add(cmpPoints[i]);
                    segments.Add(cmpPoints[i + 1]);
                }
                if (segments.Count > 0)
                {
                    Handles.color = new Color(SeriesColors[s].r, SeriesColors[s].g, SeriesColors[s].b, 0.75f);
                    Handles.DrawDottedLines(segments.ToArray(), 4f);
                }
            }
        }
    }

    void DrawTable(List<WaveStats> stats)
    {
        Header("Números por wave (valores esperados)");

        string[] heads = { "Wave", "Dur.(s)", "Spawns", "Inim.", "Vida inim.", "Dano pot.", "Dano esp.", "Cura esp.", "Caixas", "Pressão", "HP fim" };
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (string h in heads) GUILayout.Label(h, EditorStyles.miniBoldLabel, GUILayout.Width(56f));
        }

        Color oldColor = GUI.contentColor;
        foreach (WaveStats w in stats)
        {
            GUI.contentColor = w.hpEnd <= 0.001f ? new Color(1f, 0.45f, 0.45f) : oldColor;
            using (new EditorGUILayout.HorizontalScope())
            {
                Cell(w.wave.ToString());
                Cell(w.totalSeconds.ToString("0.#"));
                Cell(w.budget.ToString());
                Cell(w.enemies.ToString("0.#"));
                Cell(w.enemyHp.ToString("0.#"));
                Cell(w.contactDamage.ToString("0.#"));
                Cell(w.expectedDamage.ToString("0.#"));
                Cell(w.expectedHeal.ToString("0.#"));
                Cell(w.weaponBoxes.ToString("0.#"));
                Cell(w.killPressure.ToString("0.00"));
                Cell(w.hpEnd.ToString("0.#"));
            }
        }
        GUI.contentColor = oldColor;
    }

    static void Cell(string text)
    {
        GUILayout.Label(text, GUILayout.Width(56f));
    }

    static int DeathWave(List<WaveStats> stats)
    {
        if (stats == null) return -1;
        foreach (WaveStats w in stats)
        {
            if (w.hpEnd <= 0.001f) return w.wave;
        }
        return -1;
    }

    void DrawLossHint(List<WaveStats> stats, List<WaveStats> compare)
    {
        int death = DeathWave(stats);
        string text = death > 0
            ? "Com essas premissas, o player provavelmente morre na wave " + death +
              ". Pressão de abate acima de 1,0 significa que os inimigos se acumulam mais rápido do que ele consegue matar."
            : "Com essas premissas, o player sobrevive a todas as waves mostradas.";

        if (compare != null)
        {
            int cmpDeath = DeathWave(compare);
            text += "\nNo preset comparado: " + (cmpDeath > 0 ? "o player morre na wave " + cmpDeath + "." : "o player sobrevive a todas as waves.");
        }
        EditorGUILayout.HelpBox(text, MessageType.Info);
    }

    // ---------------------------------------------------------------- Armas

    void DrawWeapons()
    {
        if (BalanceModel.weapons.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum asset WeaponData encontrado.", MessageType.Info);
            return;
        }

        WeaponData def = BalanceModel.DefaultWeapon();
        foreach (WeaponData w in BalanceModel.weapons)
        {
            float raw = BalanceModel.RawDps(w, assume);
            float hold = BalanceModel.HoldSeconds(w);
            string holdText = float.IsInfinity(hold) ? "sem limite" : hold.ToString("0.#") + " s";
            string title = w.name + (w == def ? "  (padrão)" : "") + "    DPS " + raw.ToString("0.0") + " (efetivo " + (raw * assume.accuracy).ToString("0.0") + ")    dura " + holdText;

            if (!Foldout("w:" + AssetDatabase.GetAssetPath(w), title)) continue;

            EditorGUI.indentLevel++;
            Field(w, "fireRate", "Segundos entre tiros");
            Field(w, "numberOfProjectiles", "Projéteis por tiro");
            Field(w, "spreadAngleDegrees", "Dispersão (graus)");
            Field(w, "limitMode", "Limite");
            if (w.limitMode == WeaponData.LimitMode.Time) Field(w, "durationSeconds", "Duração (s)");
            if (w.limitMode == WeaponData.LimitMode.Ammo) Field(w, "ammo", "Tiros (munição)");

            bullet2D bullet = w.bulletPrefab != null ? w.bulletPrefab.GetComponent<bullet2D>() : null;
            if (bullet == null)
            {
                EditorGUILayout.HelpBox("Esta arma não tem bulletPrefab com bullet2D: sem dano calculado.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Bala: " + w.bulletPrefab.name, EditorStyles.miniBoldLabel);
                Field(bullet, "damage", "Dano por projétil");
                Field(bullet, "bulletSpeed", "Velocidade da bala");
                Field(bullet, "secondsUntilDestroy", "Vida da bala (s)");
                Field(bullet, "knockbackForce", "Knockback (força)");
                Field(bullet, "knockbackDuration", "Knockback (duração)");
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }
    }

    // ---------------------------------------------------------------- Inimigos

    void DrawEnemies()
    {
        WeaponData def = BalanceModel.DefaultWeapon();
        float effectiveDps = BalanceModel.RawDps(def, assume) * assume.accuracy;
        EditorGUILayout.HelpBox("Tempo p/ matar usa a arma padrão" + (def != null ? " (" + def.name + ")" : "") + " com a precisão das premissas.", MessageType.None);

        bool any = false;
        foreach (BalancePrefab p in BalanceModel.prefabs)
        {
            if (p.kind != BalanceKind.Enemy) continue;
            any = true;

            float hp = p.health.maxHealth;
            float ttk = effectiveDps > 0.0001f ? hp / effectiveDps : 0f;
            string title = p.Name + "    vida " + hp.ToString("0.#") + "   dano " + (p.enemy != null ? p.enemy.damage : 0f).ToString("0.#") + "   mata em " + ttk.ToString("0.0") + " s";
            if (!Foldout("p:" + p.path, title)) continue;

            EditorGUI.indentLevel++;
            Field(p.health, "maxHealth", "Vida máxima");
            if (p.enemy != null) Field(p.enemy, "damage", "Dano ao encostar no player");
            if (p.knockback != null) Field(p.knockback, "resistance", "Resistência a knockback");
            DrawMovementFields(p.root);
            DrawExplosionFields(p);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        if (!any) EditorGUILayout.HelpBox("Nenhum prefab de inimigo encontrado em " + BalanceModel.PrefabFolder + ".", MessageType.Info);
    }

    void DrawExplosionFields(BalancePrefab p)
    {
        if (p.explosion == null) return;

        EditorGUILayout.LabelField("Explosão ao morrer: " + p.health.deathEffectPrefab.name, EditorStyles.miniBoldLabel);
        Field(p.explosion, "damage", "Dano");
        Field(p.explosion, "radius", "Raio");
        Field(p.explosion, "knockbackForce", "Knockback (força)");
        Field(p.explosion, "knockbackDuration", "Knockback (duração)");
    }

    // Mostra os números simples (float/int/bool/enum) dos scripts de movimento do prefab.
    void DrawMovementFields(GameObject root)
    {
        IMovementBehavior[] movements = root.GetComponentsInChildren<IMovementBehavior>(true);
        foreach (IMovementBehavior m in movements)
        {
            Component c = m as Component;
            if (c == null) continue;

            List<string> paths = new List<string>();
            SerializedObject so = new SerializedObject(c);
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
            if (paths.Count == 0) continue;

            EditorGUILayout.LabelField("Movimento: " + c.GetType().Name, EditorStyles.miniBoldLabel);
            foreach (string path in paths) Field(c, path, ObjectNames.NicifyVariableName(path));
        }
    }

    // ---------------------------------------------------------------- Itens & Player

    void DrawItems()
    {
        Header("Player");
        if (BalanceModel.playerPrefab != null)
        {
            healthSystem playerHealth = BalanceModel.playerPrefab.GetComponent<healthSystem>();
            Player2D playerComponent = BalanceModel.playerPrefab.GetComponent<Player2D>();
            if (playerHealth != null) Field(playerHealth, "maxHealth", "Vida máxima");
            if (playerComponent != null) Field(playerComponent, "defaultWeapon", "Arma padrão");
        }
        else
        {
            EditorGUILayout.HelpBox("Prefab do player (Player2D) não encontrado em " + BalanceModel.PrefabFolder + ".", MessageType.Info);
        }

        DrawKind(BalanceKind.Medkit, "Medkits");
        DrawKind(BalanceKind.WeaponBox, "Caixas de arma");
        DrawKind(BalanceKind.Treasure, "Tesouros");
        DrawKind(BalanceKind.Other, "Outros (com vida)");
    }

    void DrawKind(BalanceKind kind, string title)
    {
        bool header = false;
        foreach (BalancePrefab p in BalanceModel.prefabs)
        {
            if (p.kind != kind) continue;
            if (!header) { Header(title); header = true; }

            string summary = "vida " + p.health.maxHealth.ToString("0.#");
            if (p.medkit != null) summary += "   cura " + p.medkit.healAmount.ToString("0.#");
            if (p.treasure != null) summary += "   pontos " + p.treasure.scoreValue;
            if (p.HasWeaponList) summary += "   " + p.weaponBox.possibleWeapons.Length + " armas";

            if (!Foldout("p:" + p.path, p.Name + "    " + summary)) continue;

            EditorGUI.indentLevel++;
            Field(p.health, "maxHealth", "Vida máxima");
            if (p.medkit != null) Field(p.medkit, "healAmount", "Cura");
            if (p.treasure != null) Field(p.treasure, "scoreValue", "Pontos");
            if (p.weaponBox != null)
            {
                Field(p.weaponBox, "possibleWeapons", "Armas possíveis");
                Field(p.weaponBox, "avoidCurrentWeapon", "Evitar arma atual");
                Field(p.weaponBox, "avoidLastBoxWeapon", "Evitar última da caixa");
            }
            if (p.medkit != null && p.HasWeaponList)
            {
                EditorGUILayout.HelpBox("Este medkit também tem uma lista de armas (WeaponPickupReward): ao ser destruído ele cura E dá uma arma. Se não era a intenção, esvazie a lista ou remova o componente no prefab.", MessageType.Warning);
            }
            DrawMovementFields(p.root);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }
    }

    // ---------------------------------------------------------------- Presets

    void ReloadPresets()
    {
        presets = BalancePresets.LoadAll();
    }

    void DrawPresets()
    {
        if (presets == null) ReloadPresets();

        Header("Salvar o estado atual");
        using (new EditorGUILayout.HorizontalScope())
        {
            presetName = EditorGUILayout.TextField("Nome", presetName);
            if (GUILayout.Button("Salvar preset", GUILayout.Width(110f))) SaveCurrentPreset();
        }
        EditorGUILayout.HelpBox(
            "Um preset guarda os valores que esta janela edita (waves, spawners, armas, balas, inimigos, itens, player) e a curva de dificuldade do momento. " +
            "Fica em " + BalancePresets.Folder + " e entra no git. Waves e spawners só entram se a cena do jogo estiver aberta.",
            MessageType.None);

        if (!string.IsNullOrEmpty(presetMessage)) EditorGUILayout.HelpBox(presetMessage, MessageType.Info);

        Header("Presets salvos");
        if (GUILayout.Button("Recarregar lista", GUILayout.Width(120f))) ReloadPresets();

        if (presets.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum preset salvo ainda.", MessageType.None);
            return;
        }

        PresetData toDelete = null;
        foreach (PresetData d in presets)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(d.name + "   (" + d.savedAt + ", " + d.entries.Count + " valores)", GUILayout.MinWidth(180f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Aplicar", GUILayout.Width(60f))) ApplyPreset(d);

                bool comparing = compareTo != null && compareTo.filePath == d.filePath;
                if (GUILayout.Button(comparing ? "Parar" : "Comparar", GUILayout.Width(75f))) compareTo = comparing ? null : d;

                bool showingDiff = diffPreset != null && diffPreset.filePath == d.filePath;
                if (GUILayout.Button(showingDiff ? "Fechar" : "Diferenças", GUILayout.Width(80f))) diffPreset = showingDiff ? null : d;

                if (GUILayout.Button("Apagar", GUILayout.Width(60f))) toDelete = d;
            }
        }

        if (toDelete != null)
        {
            if (EditorUtility.DisplayDialog("Apagar preset", "Apagar o preset \"" + toDelete.name + "\"?", "Apagar", "Cancelar"))
            {
                if (compareTo != null && compareTo.filePath == toDelete.filePath) compareTo = null;
                if (diffPreset != null && diffPreset.filePath == toDelete.filePath) diffPreset = null;
                BalancePresets.Delete(toDelete);
                ReloadPresets();
            }
        }

        if (diffPreset != null) DrawDiff(diffPreset);
    }

    void SaveCurrentPreset()
    {
        string name = string.IsNullOrWhiteSpace(presetName) ? "preset" : presetName.Trim();

        if (BalancePresets.Exists(name) &&
            !EditorUtility.DisplayDialog("Sobrescrever preset", "Já existe um preset chamado \"" + name + "\". Sobrescrever?", "Sobrescrever", "Cancelar"))
        {
            return;
        }

        FindWaveManager();
        List<WaveStats> stats = waveManager != null ? BalanceModel.ComputeAll(waveManager, assume) : null;
        PresetData data = BalancePresets.Capture(name, stats);
        BalancePresets.Save(data);
        ReloadPresets();
        presetMessage = "Preset \"" + name + "\" salvo com " + data.entries.Count + " valores" + (stats == null ? " (sem a curva: nenhum WaveManager na cena aberta)." : ".");
    }

    void ApplyPreset(PresetData d)
    {
        int missing;
        List<PresetDiff> diffs = BalancePresets.Diff(d, out missing);
        if (diffs.Count == 0)
        {
            presetMessage = "O preset \"" + d.name + "\" já é igual ao estado atual" + (missing > 0 ? " (" + missing + " valores não encontrados)." : ".");
            return;
        }

        if (!EditorUtility.DisplayDialog("Aplicar preset", "Aplicar \"" + d.name + "\"? Isso muda " + diffs.Count + " valores nos objetos do jogo.", "Aplicar", "Cancelar"))
        {
            return;
        }

        int applied;
        int skipped;
        soCache.Clear();
        BalancePresets.Apply(d, out applied, out skipped);
        presetMessage = "Preset \"" + d.name + "\" aplicado: " + applied + " valores mudaram" + (skipped > 0 ? ", " + skipped + " ignorados (objeto não encontrado)." : ".");
    }

    void DrawDiff(PresetData d)
    {
        // Comparar tudo com o projeto é caro: recalcula no máximo 1x por segundo.
        if (cachedDiffs == null || cachedDiffFor != d || EditorApplication.timeSinceStartup - cachedDiffAt > 1.0)
        {
            cachedDiffs = BalancePresets.Diff(d, out cachedMissing);
            cachedDiffFor = d;
            cachedDiffAt = EditorApplication.timeSinceStartup;
        }
        List<PresetDiff> diffs = cachedDiffs;
        int missing = cachedMissing;

        Header("Diferenças: atual × " + d.name);
        if (diffs.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhuma diferença nos valores encontrados" + (missing > 0 ? " (" + missing + " não encontrados)." : "."));
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Valor", EditorStyles.miniBoldLabel, GUILayout.MinWidth(200f));
            GUILayout.Label("Atual", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
            GUILayout.Label("Preset", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
        }
        foreach (PresetDiff diff in diffs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(diff.label, GUILayout.MinWidth(200f));
                GUILayout.Label(diff.current, GUILayout.Width(90f));
                GUILayout.Label(diff.preset, GUILayout.Width(90f));
            }
        }
        if (missing > 0) EditorGUILayout.LabelField(missing + " valores do preset não existem mais (objeto não encontrado).", EditorStyles.miniLabel);
    }

    // ---------------------------------------------------------------- Mudanças (Keep)

    void DrawChanges()
    {
        Header("Manter mudanças do Play mode");
        bool keep = EditorGUILayout.ToggleLeft("Manter mudanças em objetos de cena ao sair do Play mode", BalanceEdit.KeepPlayChanges);
        if (keep != BalanceEdit.KeepPlayChanges) BalanceEdit.KeepPlayChanges = keep;

        bool auto = EditorGUILayout.ToggleLeft("Salvar assets automaticamente (armas, prefabs)", BalanceEdit.AutoSave);
        if (auto != BalanceEdit.AutoSave) BalanceEdit.AutoSave = auto;

        EditorGUILayout.HelpBox(
            "Armas (ScriptableObjects) e prefabs são assets: o que você muda aqui já fica salvo, inclusive em Play mode, e é copiado para os objetos vivos.\n" +
            "Objetos de cena (como o WaveManager) voltam ao normal ao sair do Play. Com a opção acima ligada, a janela anota o que você mudou e reaplica na cena ao sair do Play (a cena fica marcada como alterada: salve com Ctrl+S).",
            MessageType.None);

        Header("Mudanças esperando para serem reaplicadas (" + BalanceEdit.KeptCount + ")");
        foreach (BalanceKeepStore.Entry e in BalanceEdit.KeptEntries)
        {
            EditorGUILayout.LabelField(e.label, e.value);
        }

        using (new EditorGUI.DisabledScope(BalanceEdit.KeptCount == 0))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reaplicar na cena agora")) BalanceEdit.ApplyKept();
                if (GUILayout.Button("Descartar")) BalanceEdit.DiscardKept();
            }
        }
    }
}
