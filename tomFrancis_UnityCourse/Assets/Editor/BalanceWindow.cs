using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Janela de balanceamento: Tools > Cosmo Treasure > Balance Window
// Edita direto os valores do jogo (WaveManager na cena, prefabs, ScriptableObjects de arma) e mostra
// uma estimativa da curva de dificuldade por wave. Veja BalanceEdit.cs para como o Play mode é tratado.
public class BalanceWindow : EditorWindow
{
    enum Tab { Waves, Planilha, Armas, Inimigos, Itens, Presets, Mudancas }

    static readonly string[] TabNames = { "Waves", "Planilha", "Armas", "Inimigos", "Itens & Player", "Presets", "Mudanças" };

    static readonly string[] SeriesNames =
    {
        "Pressão de abate (tempo p/ matar ÷ duração)",
        "Dano esperado ao player",
        "Cura esperada",
        "HP projetado do player",
        "Vida total dos inimigos",
        "Duração da wave (s)",
        "Inimigos (quantidade)",
        "Itens (quantidade: medkits, caixas de arma e tesouros)",
        "Dano potencial (contato + explosão, se tudo acertasse o player)"
    };

    static readonly Color[] SeriesColors =
    {
        new Color(1f, 0.65f, 0.15f),
        new Color(1f, 0.3f, 0.3f),
        new Color(0.35f, 0.9f, 0.4f),
        new Color(0.3f, 0.8f, 1f),
        new Color(0.7f, 0.7f, 0.75f),
        new Color(0.9f, 0.9f, 0.4f),
        new Color(1f, 0.5f, 0.75f),
        new Color(0.4f, 0.95f, 0.75f),
        new Color(1f, 0.5f, 0.2f)
    };

    const string AssumePref = "CosmoBalance.assumptions";

    Tab tab;
    Vector2 scroll;
    WaveManager waveManager;
    ParallaxSpeedController parallax;
    double nextFindTime;
    bool needRefresh;
    double lastRefresh;

    readonly Dictionary<UnityEngine.Object, SerializedObject> soCache = new Dictionary<UnityEngine.Object, SerializedObject>();
    readonly HashSet<string> openFoldouts = new HashSet<string>();

    BalanceAssumptions assume = new BalanceAssumptions();
    bool showAssumptions;
    bool normalize = true;
    bool[] seriesOn = { true, true, true, true, false, false, false, false, false };
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
        parallax = FindFirstObjectByType<ParallaxSpeedController>(FindObjectsInactive.Include);
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
            case Tab.Planilha: DrawMatrix(); break;
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

    WaveConfig activeConfig;

    // Fonte das waves: o WaveConfig (asset) do WaveManager da cena, ou qualquer WaveConfig do projeto,
    // ou (legado) os campos do próprio WaveManager.
    WaveSource GetWaveSource()
    {
        FindWaveManager();

        activeConfig = null;
        if (waveManager != null && waveManager.config != null) activeConfig = waveManager.config;
        else if (BalanceModel.waveConfigs.Count > 0) activeConfig = BalanceModel.waveConfigs[0];

        if (activeConfig != null) return WaveSource.FromConfig(activeConfig);
        if (waveManager != null) return WaveSource.FromManager(waveManager);
        return null;
    }

    void DrawWaves()
    {
        WaveSource source = GetWaveSource();
        if (source == null)
        {
            EditorGUILayout.HelpBox("Nenhum WaveConfig no projeto e nenhum WaveManager na cena aberta. Abra a cena do jogo (a 2D) e use o menu de contexto do WaveManager > 'Criar WaveConfig a partir dos valores atuais'.", MessageType.Info);
            return;
        }

        UnityEngine.Object target;
        string rangesPath;
        if (activeConfig != null)
        {
            target = activeConfig;
            rangesPath = "ranges";
            EditorGUILayout.HelpBox("Editando o asset '" + activeConfig.name + "' (vale para todas as cenas)." +
                (waveManager != null && waveManager.config != activeConfig ? "\nAtenção: o WaveManager desta cena NÃO usa este asset. Arraste-o para o campo Config dele." : ""),
                waveManager != null && waveManager.config != activeConfig ? MessageType.Warning : MessageType.None);
        }
        else
        {
            target = waveManager;
            rangesPath = "spawnerRanges";
            EditorGUILayout.HelpBox("O WaveManager desta cena ainda guarda as waves nele mesmo (vale só para esta cena). Crie um WaveConfig para compartilhar entre cenas e salvar mudanças do Play mode.", MessageType.Warning);
            if (GUILayout.Button("Criar WaveConfig a partir dos valores atuais"))
            {
                waveManager.MigrateToConfigEditor();
                soCache.Clear();
                needRefresh = true;
                return;
            }
        }

        if (activeConfig != null && activeConfig.useMatrix)
        {
            EditorGUILayout.HelpBox("Modo planilha LIGADO: o orçamento, as faixas de peso e os tesouros bônus abaixo são IGNORADOS (quem nasce em cada wave vem da aba Planilha). O intervalo entre waves, a espera pelos inimigos e a fase de loot continuam valendo.", MessageType.Warning);
        }

        Header("Ritmo das waves");
        Field(target, "startingBudget", "Spawns na wave 1");
        Field(target, "budgetIncrementPerWave", "+ spawns por wave");
        Field(target, "secondsBetweenSpawnsInWave", "Segundos entre spawns");
        bool lootOn = activeConfig != null && activeConfig.lootPhase != null && activeConfig.lootPhase.enabled;
        Field(target, "secondsBetweenWaves", lootOn ? "Intervalo entre waves (s) — ignorado: fase de loot ligada" : "Intervalo entre waves (s)");
        Field(target, "secondsBeforeFirstWave", "Espera antes da wave 1 (s)");
        Field(target, "secondsBetweenBonusSpawns", lootOn ? "Segundos entre spawns bônus — ignorado" : "Segundos entre spawns bônus");

        if (activeConfig != null) DrawLootPhase();

        Header("Quem nasce em cada wave");
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        for (int i = 0; i < source.ranges.Count; i++)
        {
            WaveRangeData r = source.ranges[i];
            string rangeName = "(vazio)";
            string tag = "";
            if (r.prefab != null)
            {
                rangeName = r.prefab.name;
                tag = "  [" + KindTag(BalanceModel.Info(r.prefab).kind) + "]";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(rangeName + tag, GUILayout.Width(200f));
                EditorGUIUtility.labelWidth = 42f;
                Field(target, rangesPath + ".Array.data[" + i + "].startWave", "início", 95f);
                Field(target, rangesPath + ".Array.data[" + i + "].endWave", "fim", 95f);
                Field(target, rangesPath + ".Array.data[" + i + "].weight", "peso", 95f);
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }
        EditorGUIUtility.labelWidth = oldLabelWidth;

        if (activeConfig != null)
        {
            if (Foldout("ranges-full", "Editar listas completas (adicionar / remover faixas e tesouros bônus)"))
            {
                EditorGUI.indentLevel++;
                Field(activeConfig, "ranges", "Faixas");
                Field(activeConfig, "treasureBonusPrefabs", "Tesouros bônus (prefabs)");
                EditorGUI.indentLevel--;
            }
        }

        DrawSpawners();

        DrawComposition(source);

        showAssumptions = EditorGUILayout.Foldout(showAssumptions, "Premissas da simulação (afetam só o gráfico)", true);
        if (showAssumptions) DrawAssumptions();

        List<WaveStats> stats = BalanceModel.ComputeAll(source, assume);
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

    float weaponBoxTarget = 2f;

    void SetRangeWeight(int index, float weight)
    {
        string path = "ranges.Array.data[" + index + "].weight";
        SerializedObject so = GetSO(activeConfig);
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null) return;

        prop.floatValue = Mathf.Round(weight * 100f) / 100f;
        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, path);
        needRefresh = true;
    }

    // ---------- Fase de loot ----------

    void DrawLootPhase()
    {
        Header("Fase de loot entre waves");
        WaveConfig cfg = activeConfig;
        Field(cfg, "lootPhase.enabled", "Ligada");

        if (cfg.lootPhase == null || !cfg.lootPhase.enabled)
        {
            EditorGUILayout.HelpBox("Desligada: o intervalo entre waves usa 'Intervalo entre waves' e a rodada de bônus aleatória.", MessageType.None);
            return;
        }

        Field(cfg, "lootPhase.waitForEnemiesToClear", "Espera os inimigos sumirem antes");
        Field(cfg, "lootPhase.clearEnemiesTimeoutSeconds", "Limite dessa espera (s)");
        Field(cfg, "lootPhase.durationSeconds", "Duração planejada (s)");
        Field(cfg, "lootPhase.dropWindowFraction", "Janela de spawn (fração)");
        Field(cfg, "lootPhase.endWhenAllCollected", "Termina antes se tudo for pego");
        Field(cfg, "lootPhase.minSecondsBeforeEarlyEnd", "Duração mínima (s)");
        Field(cfg, "lootPhase.minSecondsBetweenDrops", "Tempo mínimo entre itens (s)");
        Field(cfg, "lootPhase.safetyTimeoutSeconds", "Limite de segurança após o último item (s)");
        Field(cfg, "lootPhase.itemSpeedMultiplier", "Velocidade dos itens (x)");

        if (parallax != null)
        {
            Field(parallax, "lootSpeedMultiplier", "Parallax durante o loot (x)");
            Field(parallax, "lootSlowDownSeconds", "Parallax: tempo para desacelerar (s)");
            Field(parallax, "lootSpeedUpSeconds", "Parallax: tempo para voltar (s)");
        }
        else
        {
            EditorGUILayout.HelpBox("Nenhum ParallaxSpeedController na cena aberta (a velocidade do fundo no loot fica lá).", MessageType.None);
        }

        Header("Loot: cura");
        DrawLootItems(cfg, "lootPhase.healItems", cfg.lootPhase.healItems);
        Field(cfg, "lootPhase.lowHealthThreshold", "Vida abaixo disso → cura extra");
        Field(cfg, "lootPhase.extraHealsWhenLow", "Unidades de cura extra");

        Header("Loot: arma especial");
        DrawLootItems(cfg, "lootPhase.weaponItems", cfg.lootPhase.weaponItems);
        Field(cfg, "lootPhase.weaponFirstWave", "Primeira wave que dá arma");
        Field(cfg, "lootPhase.weaponEveryNWaves", "Repete a cada N waves");

        Header("Loot: recompensa por desempenho");
        DrawLootItems(cfg, "lootPhase.performanceItems", cfg.lootPhase.performanceItems);
        Field(cfg, "lootPhase.flawlessBonusCount", "Extra se não tomou dano");

        if (Foldout("loot-lists", "Editar listas completas do loot (adicionar / remover itens)"))
        {
            EditorGUI.indentLevel++;
            Field(cfg, "lootPhase.healItems", "Itens de cura");
            Field(cfg, "lootPhase.weaponItems", "Itens de arma");
            Field(cfg, "lootPhase.performanceItems", "Itens de desempenho");
            EditorGUI.indentLevel--;
        }
    }

    void DrawLootItems(WaveConfig cfg, string path, LootItem[] items)
    {
        if (items == null || items.Length == 0)
        {
            EditorGUILayout.LabelField("(lista vazia — use 'Editar listas completas')", EditorStyles.miniLabel);
            return;
        }

        float oldLabelWidth = EditorGUIUtility.labelWidth;
        for (int i = 0; i < items.Length; i++)
        {
            LootItem item = items[i];
            string itemName = "(vazio)";
            if (item != null && item.prefab != null)
            {
                itemName = item.prefab.name + "  [" + KindTag(BalanceModel.Info(item.prefab).kind) + "]";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(itemName, GUILayout.Width(200f));
                EditorGUIUtility.labelWidth = 42f;
                Field(cfg, path + ".Array.data[" + i + "].count", "qtd", 95f);
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }
        EditorGUIUtility.labelWidth = oldLabelWidth;
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

    void DrawComposition(WaveSource source)
    {
        previewWave = Mathf.Max(1, EditorGUILayout.IntSlider("Composição na wave", previewWave, 1, Mathf.Max(2, assume.waves)));

        float totalWeight = 0f;
        List<WaveRangeData> active = new List<WaveRangeData>();
        foreach (WaveRangeData r in source.ranges)
        {
            if (r == null || r.prefab == null) continue;
            if (previewWave < r.startWave || previewWave > r.endWave || r.weight <= 0f) continue;
            active.Add(r);
            totalWeight += r.weight;
        }

        int budget = Mathf.Max(0, source.startingBudget + (previewWave - 1) * source.budgetIncrementPerWave);
        if (active.Count == 0)
        {
            EditorGUILayout.LabelField("Nada nasce nessa wave (nenhuma faixa ativa).");
            return;
        }

        foreach (WaveRangeData r in active)
        {
            float chance = r.weight / totalWeight;
            BalancePrefab info = BalanceModel.Info(r.prefab);
            EditorGUILayout.LabelField("   " + r.prefab.name + "  [" + KindTag(info.kind) + "]",
                (chance * 100f).ToString("0.#") + "%  ≈ " + (chance * budget).ToString("0.#") + " de " + budget);

            // Caixas de arma: atalho para dizer "quero ~N caixas nesta wave" e o painel calcula o peso.
            if (info.HasWeaponList && activeConfig != null)
            {
                int rangeIndex = source.ranges.IndexOf(r);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(24f);
                    GUILayout.Label(new GUIContent("Quero ≈", "Quantas caixas esperar na wave escolhida acima. Muda o PESO desta faixa (vale para todas as waves dela)."), GUILayout.Width(60f));
                    weaponBoxTarget = EditorGUILayout.FloatField(weaponBoxTarget, GUILayout.Width(50f));
                    GUILayout.Label("caixas na wave " + previewWave, GUILayout.Width(130f));
                    if (rangeIndex >= 0 && GUILayout.Button("Ajustar peso", GUILayout.Width(100f)))
                    {
                        float others = totalWeight - r.weight;
                        float p = Mathf.Clamp(weaponBoxTarget / Mathf.Max(1, budget), 0.001f, 0.9f);
                        SetRangeWeight(rangeIndex, p / (1f - p) * others);
                    }
                }
            }
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
        assume.lootCatchRate = EditorGUILayout.Slider(new GUIContent("Chance de pegar itens no loot", "Na fase de loot não há inimigos pressionando, então o player pega mais."), assume.lootCatchRate, 0f, 1f);
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
            case 5: return "Duração";
            case 6: return "Inimigos";
            case 7: return "Itens";
        }
        return "Dano pot.";
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
            case 6: return w.enemies;
            case 7: return w.medkits + w.weaponBoxes + w.treasures;
            case 8: return w.contactDamage + w.explosionDamage;
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

        string[] heads = { "Wave", "Dur.(s)", "Spawns", "Inim.", "Vida inim.", "Dano pot.", "Dano esp.", "Cura esp.", "Cx.waves", "Cx.loot", "Pressão", "HP fim" };
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
                Cell((w.weaponBoxes - w.lootBoxes).ToString("0.#"));
                Cell(w.lootBoxes.ToString("0.#"));
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

    // ---------------------------------------------------------------- Planilha (waves x objetos)

    class MxColumn
    {
        public GameObject prefab;
        public BalancePrefab info;
        public bool isEnemy;
        public int group;     // > 0 = coluna aleatória (número do grupo, 1 = primeiro); nesse caso prefab é null
        public string title;

        public string Name { get { return group > 0 ? title : prefab.name; } }
        public int Key { get { return group > 0 ? int.MinValue + group : prefab.GetInstanceID(); } }
    }

    const string MxChartsPref = "CosmoBalance.matrixCharts";
    const float MxLabelW = 96f;
    const float MxCellW = 84f;
    const float MxTotalW = 70f;

    static readonly Color MxHpColor = new Color(1f, 0.38f, 0.38f, 0.55f);
    static readonly Color MxSpeedColor = new Color(1f, 0.95f, 0.5f, 0.45f);
    static readonly Color MxDamageColor = new Color(0.45f, 0.75f, 0.45f, 0.55f);
    static readonly Color MxEffectColor = new Color(0.5f, 0.7f, 1f, 0.45f);
    static readonly Color MxWaveColor = new Color(0.45f, 0.65f, 1f, 0.40f);
    static readonly Color MxLootColor = new Color(0.75f, 0.75f, 0.75f, 0.28f);
    static readonly Color MxHeadColor = new Color(0.5f, 0.65f, 0.95f, 0.55f);

    static readonly string[] MxSpeedFields = { "speed", "forwardSpeed", "moveSpeed", "initialSpeedX" };
    static readonly string[] MxTimeFields = { "duration", "flightTime", "lifetimeSeconds" };

    Vector2 matrixScroll;
    GUIStyle mxCenter;
    GUIStyle mxHead;
    Dictionary<(int, int, int), int> mxIndex = new Dictionary<(int, int, int), int>();

    void DrawMatrix()
    {
        GetWaveSource(); // atualiza activeConfig
        if (activeConfig == null)
        {
            EditorGUILayout.HelpBox("A planilha precisa de um WaveConfig (asset). Abra a cena do jogo e use, na aba Waves, 'Criar WaveConfig a partir dos valores atuais'.", MessageType.Info);
            return;
        }

        if (mxCenter == null)
        {
            mxCenter = new GUIStyle(EditorStyles.label);
            mxCenter.alignment = TextAnchor.MiddleCenter;
            mxHead = new GUIStyle(EditorStyles.boldLabel);
            mxHead.alignment = TextAnchor.MiddleCenter;
            mxHead.wordWrap = false;
            mxHead.clipping = TextClipping.Clip;
            mxHead.fontSize = 10;
        }

        WaveConfig cfg = activeConfig;

        EditorGUILayout.HelpBox("Asset '" + cfg.name + "'. Cada linha é uma wave: o número é quantos daquele objeto nascem (misturados, em ordem aleatória). " +
            "'Loot N' é o que aparece na fase de loot DEPOIS da wave N. As alterações valem na hora, inclusive em Play mode (na próxima wave). Arraste os nomes no cabeçalho para reordenar as colunas.", MessageType.None);

        Header("Número de waves");
        Field(cfg, "useMatrix", "Usar a planilha (contagem exata por wave)");
        if (cfg.useMatrix)
        {
            Field(cfg, "totalWaves", "Total de waves");
        }
        else
        {
            Field(cfg, "limitWaves", "Limitar o número de waves");
            if (cfg.limitWaves) Field(cfg, "totalWaves", "Total de waves");
            EditorGUILayout.HelpBox("Planilha DESLIGADA: o jogo usa o orçamento + faixas da aba Waves. Você pode preencher a planilha agora; ela só passa a valer quando 'Usar a planilha' estiver ligado.", MessageType.Warning);
        }

        int waves = Mathf.Max(1, cfg.totalWaves);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preencher a partir do orçamento atual", GUILayout.Width(250f))) MxFillFromBudget(cfg, waves);
            if (GUILayout.Button("Limpar planilha", GUILayout.Width(120f))) MxClear(cfg);
            if (GUILayout.Button("Ordem padrão das colunas", GUILayout.Width(170f))) MxResetOrder(cfg);
        }

        if (Foldout("mxGroups", "Grupos aleatórios (colunas que sorteiam entre vários prefabs)"))
        {
            EditorGUILayout.HelpBox("Cada grupo vira uma coluna na planilha. Cada vez que a planilha manda nascer um item dessa coluna, o jogo sorteia UM prefab da lista (sorteio puro: pode repetir). " +
                "Cada prefab continua usando o próprio Spawner da cena. Ex.: um grupo com as 3 caixas de arma; depois é só pôr 1 na coluna dele no Loot das waves desejadas.", MessageType.None);
            Field(cfg, "randomGroups", "Grupos");
        }

        List<MxColumn> cols = BuildMatrixColumns(cfg);
        if (cols.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum prefab de inimigo/item encontrado em " + BalanceModel.PrefabFolder + ".", MessageType.Info);
            return;
        }

        MxRebuildIndex(cfg);

        bool anyItem = false;
        bool anyExplosion = false;
        foreach (MxColumn col in cols)
        {
            if (!col.isEnemy) anyItem = true;
            if (col.info != null && col.info.explosion != null) anyExplosion = true;
        }

        EditorGUILayout.Space(6);
        // Altura explícita: sem ela, a rolagem interna encolhia e só aparecia o começo da planilha.
        float matrixHeight = 34f + MxRowH * 3f + (anyExplosion ? MxRowH : 0f) + (anyItem ? MxRowH : 0f) + 8f + waves * 2f * MxRowH + 22f;
        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll, true, false, GUILayout.Height(matrixHeight));

        // Cabeçalho: nomes
        MxRowBegin(MxHeadColor, "");
        MxDrawHeaders(cfg, cols);
        MxRowEnd();

        // Status
        MxRowBegin(MxHpColor, "Vida");
        foreach (MxColumn col in cols) CellField(col.info != null ? col.info.health : null, "maxHealth", MxCellW);
        MxRowEnd();

        MxRowBegin(MxSpeedColor, "Velocidade");
        foreach (MxColumn col in cols) MxSpeedCell(col);
        MxRowEnd();

        MxRowBegin(MxDamageColor, "Dano (contato)");
        foreach (MxColumn col in cols)
        {
            if (col.isEnemy && col.info.enemy != null) CellField(col.info.enemy, "damage", MxCellW);
            else MxLabelCell("—", null, MxCellW);
        }
        MxRowEnd();

        if (anyExplosion)
        {
            MxRowBegin(MxDamageColor, "Dano (explosão)");
            foreach (MxColumn col in cols)
            {
                if (col.info != null && col.info.explosion != null) CellField(col.info.explosion, "damage", MxCellW);
                else MxLabelCell("—", null, MxCellW);
            }
            MxRowEnd();
        }

        if (anyItem)
        {
            MxRowBegin(MxEffectColor, "Efeito do item");
            foreach (MxColumn col in cols) MxEffectCell(col);
            MxRowEnd();
        }

        EditorGUILayout.Space(4);

        // Waves
        float grandHp = 0f;
        int grandEnemies = 0;
        for (int w = 1; w <= waves; w++)
        {
            MxRowBegin(MxWaveColor, "Wave " + w);
            int spawns = 0;
            float hp = 0f;
            foreach (MxColumn col in cols)
            {
                int count = MxCountCell(cfg, col, w, MatrixPhase.Wave);
                spawns += count;
                if (col.isEnemy && col.info != null && col.info.health != null)
                {
                    hp += count * col.info.health.maxHealth;
                    grandEnemies += count;
                }
            }
            grandHp += hp;
            MxLabelCell("Σ " + spawns, "Total de objetos que nascem na wave", MxTotalW);
            MxLabelCell("HP " + hp.ToString("0.#"), "Vida total dos inimigos da wave", MxTotalW + 14f);
            MxRowEnd();

            MxRowBegin(MxLootColor, "Loot " + w);
            int lootSpawns = 0;
            foreach (MxColumn col in cols)
            {
                if (col.isEnemy)
                {
                    MxCellRect(MxCellW);
                    continue;
                }
                lootSpawns += MxCountCell(cfg, col, w, MatrixPhase.Loot);
            }
            MxLabelCell("Σ " + lootSpawns, "Total de itens na fase de loot", MxTotalW);
            MxRowEnd();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        bool showCharts = EditorPrefs.GetBool(MxChartsPref, true);
        bool nowShowCharts = EditorGUILayout.Foldout(showCharts, "Curvas da planilha (clique para fechar / abrir)", true, EditorStyles.foldoutHeader);
        if (nowShowCharts != showCharts) EditorPrefs.SetBool(MxChartsPref, nowShowCharts);

        if (nowShowCharts)
        {
            // Curvas calculadas a partir da planilha (mesmo se ela ainda estiver desligada: serve de pré-visualização).
            WaveSource matrixSource = WaveSource.FromConfig(cfg);
            matrixSource.useMatrix = true;
            matrixSource.limitWaves = true;
            matrixSource.totalWaves = waves;
            if (matrixSource.loot == null) matrixSource.loot = cfg.lootPhase;
            List<WaveStats> matrixStats = BalanceModel.ComputeAll(matrixSource, assume);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Mostrar:", GUILayout.Width(56f));
                if (GUILayout.Button("Composição da planilha", GUILayout.Width(150f)))
                {
                    seriesOn = new[] { false, false, false, false, true, false, true, true, true };
                }
                if (GUILayout.Button("Dificuldade estimada", GUILayout.Width(140f)))
                {
                    seriesOn = new[] { true, true, true, true, false, false, false, false, false };
                }
            }
            DrawSeriesToggles();
            if (waves < 2) EditorGUILayout.HelpBox("O gráfico precisa de pelo menos 2 waves.", MessageType.None);
            Rect matrixChartRect = GUILayoutUtility.GetRect(100f, 240f, GUILayout.ExpandWidth(true));
            DrawChart(matrixChartRect, matrixStats, null);
            DrawTable(matrixStats);
            DrawLossHint(matrixStats, null);
        }


        EditorGUILayout.HelpBox("Total no jogo: " + grandEnemies + " inimigos, " + grandHp.ToString("0.#") + " de vida somada.", MessageType.None);
        EditorGUILayout.HelpBox("Velocidade mostra o campo 'speed' (ou forwardSpeed) do movimento do prefab; movimentos sem velocidade simples mostram a duração com um 't' (ex.: Path). " +
            "Para editar a lista de armas de uma caixa, use a aba 'Itens & Player'. Presets guardam a planilha (células, nº de waves e ordem das colunas).", MessageType.None);
    }

    List<MxColumn> BuildMatrixColumns(WaveConfig cfg)
    {
        List<MxColumn> cols = new List<MxColumn>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        BalanceKind[] order = { BalanceKind.WeaponBox, BalanceKind.Medkit, BalanceKind.Treasure, BalanceKind.Enemy };
        foreach (BalanceKind kind in order)
        {
            foreach (BalancePrefab p in BalanceModel.prefabs)
            {
                if (p.kind != kind || p.root == null || !seen.Add(p.root)) continue;
                MxColumn col = new MxColumn();
                col.prefab = p.root;
                col.info = p;
                col.isEnemy = kind == BalanceKind.Enemy;
                cols.Add(col);
            }
        }

        // Prefabs que já estão na planilha mas não foram achados na pasta padrão.
        if (cfg.matrixCells != null)
        {
            foreach (WaveMatrixCell cell in cfg.matrixCells)
            {
                if (cell == null || cell.prefab == null || !seen.Add(cell.prefab)) continue;
                BalancePrefab info = BalanceModel.Info(cell.prefab);
                MxColumn col = new MxColumn();
                col.prefab = cell.prefab;
                col.info = info;
                col.isEnemy = info != null && info.kind == BalanceKind.Enemy;
                cols.Add(col);
            }
        }

        // Ordem escolhida pelo usuário (arrastando os cabeçalhos): as colunas salvas vêm primeiro, nessa ordem;
        // as que não estão na lista (prefabs novos) ficam depois, na ordem padrão.
        if (cfg.matrixColumnOrder != null && cfg.matrixColumnOrder.Length > 0)
        {
            List<MxColumn> ordered = new List<MxColumn>();
            foreach (GameObject saved in cfg.matrixColumnOrder)
            {
                if (saved == null) continue;
                int i = cols.FindIndex(c => c.prefab == saved);
                if (i < 0) continue;
                ordered.Add(cols[i]);
                cols.RemoveAt(i);
            }
            ordered.AddRange(cols);
            cols = ordered;
        }

        // Colunas aleatórias: ficam logo depois da última caixa de arma (ou no começo, se não houver).
        if (cfg.randomGroups != null)
        {
            int insertAt = cols.FindLastIndex(c => c.info != null && c.info.kind == BalanceKind.WeaponBox) + 1;
            for (int g = 0; g < cfg.randomGroups.Length; g++)
            {
                MatrixRandomGroup group = cfg.randomGroups[g];
                MxColumn gc = new MxColumn();
                gc.group = g + 1;
                gc.title = group != null && !string.IsNullOrEmpty(group.label) ? group.label : "Grupo " + (g + 1);
                cols.Insert(insertAt++, gc);
            }
        }
        return cols;
    }

    // ---------- cabeçalho arrastável ----------

    Rect[] mxHeaderRects = new Rect[0];
    int mxDragFrom = -1;
    int mxDragTo = -1;
    int mxDragControl;

    // Desenha os nomes das colunas. Clicar e arrastar um nome para o lado troca a coluna de lugar.
    void MxDrawHeaders(WaveConfig cfg, List<MxColumn> cols)
    {
        if (mxHeaderRects.Length != cols.Count) mxHeaderRects = new Rect[cols.Count];
        Event e = Event.current;

        for (int i = 0; i < cols.Count; i++)
        {
            MxColumn col = cols[i];
            string kind = col.info != null ? KindTag(col.info.kind) : "";
            Rect rect = GUILayoutUtility.GetRect(MxCellW, 34f, GUILayout.Width(MxCellW), GUILayout.Height(34f));
            if (e.type != EventType.Layout) mxHeaderRects[i] = rect;

            int id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = id;
                mxDragControl = id;
                mxDragFrom = i;
                mxDragTo = i;
                e.Use();
            }
            else if (e.type == EventType.Repaint)
            {
                bool dragging = mxDragFrom >= 0 && GUIUtility.hotControl != 0 && GUIUtility.hotControl == mxDragControl;
                GUI.Label(rect, new GUIContent(MxHeaderText(col.Name), MxHeaderTip(cfg, col, kind)), mxHead);
                if (dragging && i == mxDragFrom) EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.3f));
                if (dragging && i == mxDragTo && mxDragTo != mxDragFrom)
                {
                    float x = mxDragTo < mxDragFrom ? rect.x - 1f : rect.xMax - 2f;
                    EditorGUI.DrawRect(new Rect(x, rect.y, 3f, rect.height), new Color(1f, 0.8f, 0.2f, 1f));
                }
            }
        }

        // Arrasto: descobre a coluna sob o mouse (os retângulos de todas as colunas já foram atualizados neste evento).
        if (mxDragFrom >= 0 && GUIUtility.hotControl != 0 && GUIUtility.hotControl == mxDragControl)
        {
            if (e.type == EventType.MouseDrag)
            {
                for (int i = 0; i < mxHeaderRects.Length; i++)
                {
                    bool last = i == mxHeaderRects.Length - 1;
                    if (e.mousePosition.x >= mxHeaderRects[i].x && (e.mousePosition.x < mxHeaderRects[i].xMax || last)) mxDragTo = i;
                }
                if (mxHeaderRects.Length > 0 && e.mousePosition.x < mxHeaderRects[0].x) mxDragTo = 0;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                int from = mxDragFrom;
                int to = mxDragTo;
                GUIUtility.hotControl = 0;
                mxDragFrom = -1;
                mxDragTo = -1;
                e.Use();
                if (from >= 0 && to >= 0 && from != to) MxMoveColumn(cfg, cols, from, to);
                Repaint();
            }
        }
    }

    // Nome no cabeçalho: tira o sufixo "Prop" e quebra em 2 linhas (no espaço ou na maiúscula mais perto do meio) em vez de cortar no meio da palavra.
    static string MxHeaderText(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length > 6 && name.EndsWith("Prop")) name = name.Substring(0, name.Length - 4);
        if (name.Length <= 10) return name;

        int mid = name.Length / 2;
        int best = -1;
        for (int i = 1; i < name.Length; i++)
        {
            bool boundary = name[i] == ' ' || char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]);
            if (boundary && (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid))) best = i;
        }
        if (best < 0) return name;
        return name.Substring(0, best).TrimEnd() + "\n" + name.Substring(best).TrimStart();
    }

    static string MxHeaderTip(WaveConfig cfg, MxColumn col, string kind)
    {
        if (col.group > 0)
        {
            List<string> names = new List<string>();
            MatrixRandomGroup g = cfg.randomGroups[col.group - 1];
            if (g != null && g.options != null) foreach (GameObject o in g.options) if (o != null) names.Add(o.name);
            return "Grupo aleatório " + col.group + ": sorteia entre " + (names.Count > 0 ? string.Join(", ", names.ToArray()) : "(nenhum prefab)");
        }
        return kind + ": " + col.prefab.name + "\n(arraste para mudar a ordem)";
    }

    void MxMoveColumn(WaveConfig cfg, List<MxColumn> cols, int from, int to)
    {
        // As colunas aleatórias não entram na ordem salva (ficam sempre depois da última caixa de arma).
        List<MxColumn> moving = new List<MxColumn>(cols);
        MxColumn movedCol = moving[from];
        moving.RemoveAt(from);
        moving.Insert(to, movedCol);

        List<GameObject> order = new List<GameObject>();
        foreach (MxColumn col in moving) if (col.group <= 0) order.Add(col.prefab);

        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixColumnOrder");
        if (arr == null) return;
        arr.arraySize = order.Count;
        for (int i = 0; i < order.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixColumnOrder");
    }

    void MxResetOrder(WaveConfig cfg)
    {
        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixColumnOrder");
        if (arr == null) return;
        arr.ClearArray();
        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixColumnOrder");
    }

    const float MxRowH = 22f;

    // Toda célula da planilha ocupa um retângulo de largura EXATA e sem margens (GUIStyle.none). Os campos do Unity
    // têm margem própria, e somar isso coluna a coluna desalinhava os campos do cabeçalho.
    static Rect MxCellRect(float width)
    {
        return GUILayoutUtility.GetRect(width, MxRowH, GUIStyle.none, GUILayout.Width(width), GUILayout.Height(MxRowH));
    }

    static Rect MxPad(Rect r)
    {
        return new Rect(r.x + 2f, r.y + 1f, r.width - 4f, r.height - 2f);
    }

    void MxLabelCell(string text, string tooltip, float width)
    {
        GUI.Label(MxCellRect(width), new GUIContent(text, tooltip), mxCenter);
    }

    Rect MxRowBegin(Color tint, string label)
    {
        Rect r = EditorGUILayout.BeginHorizontal();
        if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, tint);
        GUI.Label(MxCellRect(MxLabelW), label, EditorStyles.boldLabel);
        return r;
    }

    void MxRowEnd()
    {
        EditorGUILayout.EndHorizontal();
    }

    // Campo sem rótulo numa célula da planilha, com o mesmo caminho de edição do resto da janela.
    // Usa campos tipados (e não PropertyField) para não herdar [Header]/[Space] do script.
    bool CellField(UnityEngine.Object target, string path, float width)
    {
        Rect cell = MxCellRect(width);
        if (target == null)
        {
            GUI.Label(cell, "—", mxCenter);
            return false;
        }

        SerializedObject so = GetSO(target);
        SerializedProperty prop = so.FindProperty(path);
        if (prop == null)
        {
            GUI.Label(cell, "?", mxCenter);
            return false;
        }

        Rect r = MxPad(cell);
        EditorGUI.BeginChangeCheck();
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Float:
                prop.floatValue = EditorGUI.FloatField(r, prop.floatValue);
                break;
            case SerializedPropertyType.Integer:
                prop.intValue = EditorGUI.IntField(r, prop.intValue);
                break;
            default:
                EditorGUI.PropertyField(r, prop, GUIContent.none);
                break;
        }
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            BalanceEdit.AfterEdit(so, path);
            return true;
        }
        return false;
    }

    void MxSpeedCell(MxColumn col)
    {
        Component comp = null;
        string path = null;
        bool isTime = false;

        if (col.prefab != null)
        {
            foreach (IMovementBehavior m in col.prefab.GetComponentsInChildren<IMovementBehavior>(true))
            {
                Component c = m as Component;
                if (c == null) continue;
                SerializedObject so = GetSO(c);

                foreach (string n in MxSpeedFields)
                {
                    SerializedProperty p = so.FindProperty(n);
                    if (p != null && p.propertyType == SerializedPropertyType.Float) { comp = c; path = n; break; }
                }
                if (comp == null)
                {
                    foreach (string n in MxTimeFields)
                    {
                        SerializedProperty p = so.FindProperty(n);
                        if (p != null && p.propertyType == SerializedPropertyType.Float) { comp = c; path = n; isTime = true; break; }
                    }
                }
                if (comp != null) break;
            }
        }

        if (comp == null)
        {
            MxLabelCell("—", null, MxCellW);
            return;
        }

        if (isTime)
        {
            MxLabelCell("t", "Este movimento não tem velocidade simples; edita a duração (" + path + "). Maior = mais lento.", 16f);
            CellField(comp, path, MxCellW - 16f);
        }
        else
        {
            CellField(comp, path, MxCellW);
        }
    }

    void MxEffectCell(MxColumn col)
    {
        BalancePrefab p = col.info;
        if (p == null || col.isEnemy)
        {
            MxLabelCell("—", null, MxCellW);
        }
        else if (p.medkit != null)
        {
            MxLabelCell("cura", "Quanto o medkit cura", 32f);
            CellField(p.medkit, "healAmount", MxCellW - 32f);
        }
        else if (p.treasure != null)
        {
            MxLabelCell("pts", "Pontos do tesouro", 28f);
            CellField(p.treasure, "scoreValue", MxCellW - 28f);
        }
        else if (p.HasWeaponList)
        {
            List<string> names = new List<string>();
            foreach (WeaponData w in p.weaponBox.possibleWeapons) if (w != null) names.Add(w.name);
            MxLabelCell(names.Count + " arma(s)", string.Join(", ", names.ToArray()), MxCellW);
        }
        else
        {
            MxLabelCell("—", null, MxCellW);
        }
    }

    // ---------- células de contagem ----------

    void MxRebuildIndex(WaveConfig cfg)
    {
        mxIndex.Clear();
        if (cfg.matrixCells == null) return;
        for (int i = 0; i < cfg.matrixCells.Length; i++)
        {
            WaveMatrixCell cell = cfg.matrixCells[i];
            if (cell == null || (cell.prefab == null && cell.group <= 0)) continue;
            mxIndex[(MxKey(cell.prefab, cell.group), cell.wave, (int)cell.phase)] = i;
        }
    }

    static int MxKey(GameObject prefab, int group)
    {
        return group > 0 ? int.MinValue + group : prefab.GetInstanceID();
    }

    // Desenha a célula (quantidade, e para caixas de arma também a arma) e devolve a quantidade atual.
    int MxCountCell(WaveConfig cfg, MxColumn col, int wave, MatrixPhase phase)
    {
        int idx;
        int count = 0;
        WeaponData weapon = null;
        if (mxIndex.TryGetValue((col.Key, wave, (int)phase), out idx) && cfg.matrixCells != null && idx < cfg.matrixCells.Length)
        {
            count = Mathf.Max(0, cfg.matrixCells[idx].count);
            weapon = cfg.matrixCells[idx].weapon;
        }

        bool isWeaponBox = col.info != null && col.info.kind == BalanceKind.WeaponBox;
        const float countW = 30f;

        Rect countRect = MxCellRect(isWeaponBox ? countW : MxCellW);
        Rect popupRect = isWeaponBox ? MxCellRect(MxCellW - countW) : default(Rect);

        Color oldBg = GUI.backgroundColor;
        if (count > 0) GUI.backgroundColor = new Color(1f, 1f, 0.55f);
        EditorGUI.BeginChangeCheck();
        int newCount = EditorGUI.IntField(MxPad(countRect), count);
        bool countChanged = EditorGUI.EndChangeCheck();
        GUI.backgroundColor = oldBg;

        if (isWeaponBox)
        {
            List<WeaponData> list = BalanceModel.weapons;
            string[] options = new string[list.Count + 1];
            options[0] = "(sorteio)";
            int selected = 0;
            for (int i = 0; i < list.Count; i++)
            {
                options[i + 1] = list[i].name;
                if (list[i] == weapon) selected = i + 1;
            }

            using (new EditorGUI.DisabledScope(count <= 0 && !countChanged))
            {
                EditorGUI.BeginChangeCheck();
                int picked = EditorGUI.Popup(MxPad(popupRect), selected, options);
                if (EditorGUI.EndChangeCheck() && count > 0)
                {
                    MxSetWeapon(cfg, col.prefab, wave, phase, picked == 0 ? null : list[picked - 1], col.group);
                    return count;
                }
            }
        }

        if (countChanged)
        {
            newCount = Mathf.Max(0, newCount);
            MxSetCount(cfg, col.prefab, wave, phase, newCount, col.group);
            return newCount;
        }
        return count;
    }

    int MxFind(WaveConfig cfg, GameObject prefab, int wave, MatrixPhase phase, int group = 0)
    {
        int idx;
        return mxIndex.TryGetValue((MxKey(prefab, group), wave, (int)phase), out idx) ? idx : -1;
    }

    void MxSetCount(WaveConfig cfg, GameObject prefab, int wave, MatrixPhase phase, int count, int group = 0)
    {
        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixCells");
        if (arr == null) return;

        int idx = MxFind(cfg, prefab, wave, phase, group);
        if (count <= 0)
        {
            if (idx >= 0 && idx < arr.arraySize) arr.DeleteArrayElementAtIndex(idx);
        }
        else if (idx >= 0 && idx < arr.arraySize)
        {
            arr.GetArrayElementAtIndex(idx).FindPropertyRelative("count").intValue = count;
        }
        else
        {
            MxAppend(arr, prefab, wave, phase, count, null, group);
        }

        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixCells");
        MxRebuildIndex(cfg);
    }

    void MxSetWeapon(WaveConfig cfg, GameObject prefab, int wave, MatrixPhase phase, WeaponData weapon, int group = 0)
    {
        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixCells");
        int idx = MxFind(cfg, prefab, wave, phase, group);
        if (arr == null || idx < 0 || idx >= arr.arraySize) return;

        arr.GetArrayElementAtIndex(idx).FindPropertyRelative("weapon").objectReferenceValue = weapon;
        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixCells");
        MxRebuildIndex(cfg);
    }

    static void MxAppend(SerializedProperty arr, GameObject prefab, int wave, MatrixPhase phase, int count, WeaponData weapon, int group = 0)
    {
        int n = arr.arraySize;
        arr.arraySize = n + 1;
        SerializedProperty el = arr.GetArrayElementAtIndex(n);
        el.FindPropertyRelative("group").intValue = group;
        el.FindPropertyRelative("prefab").objectReferenceValue = group > 0 ? null : prefab;
        el.FindPropertyRelative("wave").intValue = wave;
        el.FindPropertyRelative("phase").enumValueIndex = (int)phase;
        el.FindPropertyRelative("count").intValue = count;
        el.FindPropertyRelative("weapon").objectReferenceValue = weapon;
    }

    void MxClear(WaveConfig cfg)
    {
        if (!EditorUtility.DisplayDialog("Limpar planilha", "Apagar TODAS as células da planilha de '" + cfg.name + "'?", "Limpar", "Cancelar")) return;

        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixCells");
        if (arr == null) return;
        arr.ClearArray();
        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixCells");
        MxRebuildIndex(cfg);
    }

    // Gera a planilha a partir do orçamento + faixas de peso atuais (contagem esperada por wave) e do loot fixo.
    void MxFillFromBudget(WaveConfig cfg, int waves)
    {
        if (!EditorUtility.DisplayDialog("Preencher a planilha",
            "Substituir a planilha de '" + cfg.name + "' pelas contagens esperadas do orçamento + faixas atuais (" + waves + " waves)?\nO loot recebe os itens fixos de cura/arma/desempenho (sem bônus).",
            "Preencher", "Cancelar")) return;

        SerializedObject so = GetSO(cfg);
        SerializedProperty arr = so.FindProperty("matrixCells");
        if (arr == null) return;
        arr.ClearArray();

        for (int w = 1; w <= waves; w++)
        {
            int budget = Mathf.Max(0, cfg.startingBudget + (w - 1) * cfg.budgetIncrementPerWave);
            float totalWeight = 0f;
            List<WaveRangeData> active = new List<WaveRangeData>();
            if (cfg.ranges != null)
            {
                foreach (WaveRangeData r in cfg.ranges)
                {
                    if (r == null || r.prefab == null || w < r.startWave || w > r.endWave || r.weight <= 0f) continue;
                    active.Add(r);
                    totalWeight += r.weight;
                }
            }
            foreach (WaveRangeData r in active)
            {
                int count = Mathf.RoundToInt(budget * r.weight / totalWeight);
                if (count > 0) MxAppend(arr, r.prefab, w, MatrixPhase.Wave, count, null);
            }

            LootPhaseData loot = cfg.lootPhase;
            if (loot == null) continue;
            MxAppendLootItems(arr, loot.healItems, w);
            int every = Mathf.Max(1, loot.weaponEveryNWaves);
            if (w >= loot.weaponFirstWave && (w - loot.weaponFirstWave) % every == 0) MxAppendLootItems(arr, loot.weaponItems, w);
            MxAppendLootItems(arr, loot.performanceItems, w);
        }

        so.ApplyModifiedProperties();
        BalanceEdit.AfterEdit(so, "matrixCells");
        MxRebuildIndex(cfg);
    }

    static void MxAppendLootItems(SerializedProperty arr, LootItem[] items, int wave)
    {
        if (items == null) return;
        foreach (LootItem item in items)
        {
            if (item == null || item.prefab == null || item.count <= 0) continue;
            MxAppend(arr, item.prefab, wave, MatrixPhase.Loot, item.count, null);
        }
    }

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
            "Um preset guarda os valores que esta janela edita (waves, planilha, spawners, armas, balas, inimigos, itens, player) e a curva de dificuldade do momento. " +
            "Fica em " + BalancePresets.Folder + " e entra no git. As waves entram pelo asset WaveConfig; os spawners só se a cena do jogo estiver aberta.",
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
                GUILayout.Label(d.name + "   (" + d.savedAt + ", " + d.entries.Count + " valores" + (d.matrices != null && d.matrices.Count > 0 ? ", com planilha" : "") + ")", GUILayout.MinWidth(180f));
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

        WaveSource source = GetWaveSource();
        List<WaveStats> stats = source != null ? BalanceModel.ComputeAll(source, assume) : null;
        PresetData data = BalancePresets.Capture(name, stats);
        BalancePresets.Save(data);
        ReloadPresets();
        presetMessage = "Preset \"" + name + "\" salvo com " + data.entries.Count + " valores" + (stats == null ? " (sem a curva: nenhum WaveConfig no projeto nem WaveManager na cena aberta)." : ".");
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
