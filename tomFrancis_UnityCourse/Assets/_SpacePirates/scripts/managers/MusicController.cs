using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using FMODUnity;

// Música dinâmica do FMOD por "estágios": cada estágio diz QUANDO (gatilho do jogo) e O QUE mudar (parâmetros de
// qualquer tipo: labeled, float, int; globais ou locais). Opcionalmente o estágio espera o FMOD REALMENTE
// chegar em um marker da timeline (a transição quantizada) e só então dispara "On Reached" — é aí que você liga
// animações, PlayableDirectors etc.
//
// Gatilhos: Game Started (corrida começou), Menu, Game Over, Wave Started, Wave Completed, Loot Phase Started,
// All Waves Completed, Loot Phase Ended (fim da fase de loot: a wave que acabou), Manual (chamado por código/UnityEvent: RequestStage("nome")).
// "Wave" = 0 vale para qualquer wave; N = só a wave N.
//
// Coloque em um GameObject da cena. Dois modos de tocar:
//   A) arraste o StudioEventEmitter que já toca a música (recomendado);
//   B) sem emitter: escolha o evento em "Music" e o script toca sozinho (remova o emitter antigo).
public class MusicController : MonoBehaviour
{
    public enum Trigger { GameStarted, Menu, GameOver, WaveStarted, WaveCompleted, LootPhaseStarted, AllWavesCompleted, Manual, LootPhaseEnded }
    public enum ChangeMode { Label, Value }

    [Serializable]
    public class ParameterChange
    {
        [Tooltip("Nome EXATO do parâmetro no FMOD (ex.: gameState).")]
        public string parameter = "gameState";
        [Tooltip("Ligado = parâmetro GLOBAL (Window > Parameters). Desligado = parâmetro local do evento da música.")]
        public bool isGlobal = true;
        [Tooltip("Label = parâmetro labeled (escreva o texto do label). Value = parâmetro float/int (escreva o número).")]
        public ChangeMode mode = ChangeMode.Label;
        [Tooltip("Modo Label: texto exato do label no FMOD (ex.: wave4_8).")]
        public string label;
        [Tooltip("Modo Value: valor numérico (para int use 0, 1, 2...).")]
        public float value;
    }

    [Serializable]
    public class Stage
    {
        public string name = "Novo estágio";
        public Trigger trigger = Trigger.WaveCompleted;
        [Tooltip("0 = qualquer wave. N = só na wave N (vale para Wave Started / Wave Completed / Loot Phase Started).")]
        [Min(0)] public int wave = 0;
        [Tooltip("Parâmetros que mudam quando este estágio é pedido.")]
        public ParameterChange[] changes;

        [Tooltip("Opcional: nome de um marker da timeline do FMOD. Se preenchido, 'On Reached' só dispara quando o FMOD passar por esse marker (o fim da transição quantizada). Vazio = 'On Reached' dispara na hora.")]
        public string waitForMarker;
        [Tooltip("Se o marker não chegar neste tempo (s), dispara 'On Reached' mesmo assim e avisa no Console.")]
        [Min(1f)] public float maxWaitSeconds = 60f;

        [Tooltip("Dispara quando o estágio é pedido (parâmetro já enviado ao FMOD).")]
        public UnityEvent onRequested;
        [Tooltip("Dispara quando a música realmente chegou (marker). Ligue aqui animações / playables.")]
        public UnityEvent onReached;
    }

    public static MusicController Instance { get; private set; }

    [Header("FMOD")]
    [Tooltip("OPÇÃO A: Studio Event Emitter que já toca a música na cena.")]
    public StudioEventEmitter emitter;
    [Tooltip("OPÇÃO B (sem emitter): evento da música; o script toca sozinho.")]
    public EventReference music;

    [Header("Estágios")]
    public Stage[] stages;

    [Header("Debug")]
    public bool logChanges = true;
    [Tooltip("Mostra no Console o nome de TODO marker que o FMOD passar. Use para descobrir se os markers de destino disparam.")]
    public bool logMarkers = true;

    // Eventos para outros scripts.
    public event Action<Stage> StageRequested;
    public event Action<Stage> StageReached;
    public Stage CurrentStage { get; private set; }

    // ---- FMOD ----
    FMOD.Studio.EventInstance ownInstance;
    bool hasOwnInstance;
    FMOD.Studio.EventInstance attached;
    bool hasAttached;
    GCHandle selfHandle;
    static readonly FMOD.Studio.EVENT_CALLBACK markerCallback = MarkerCallback;

    class MarkerQueue
    {
        public readonly object sync = new object();
        public readonly List<string> names = new List<string>();
    }
    readonly MarkerQueue queue = new MarkerQueue();
    GCHandle queueHandle;

    // Último valor pedido de cada parâmetro local (reaplicado quando a instância passa a existir).
    readonly Dictionary<string, ParameterChange> desiredLocal = new Dictionary<string, ParameterChange>();

    // ---- estágio esperando marker ----
    Stage pending;
    float pendingDeadline;

    WaveManager waves;
    GameState previousState = GameState.Menu;

    void Awake()
    {
        Instance = this;
        queueHandle = GCHandle.Alloc(queue);
    }

    void Start()
    {
        if (emitter == null)
        {
            if (!music.IsNull)
            {
                ownInstance = RuntimeManager.CreateInstance(music);
                hasOwnInstance = true;
                ownInstance.start();
            }
            else
            {
                Debug.LogWarning("MusicController: arraste um Studio Event Emitter ou escolha um evento de música.", this);
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            if (GameManager.Instance.CurrentState == GameState.Menu) RunTrigger(Trigger.Menu, 0);
            else if (GameManager.Instance.CurrentState != GameState.Menu) HandleStateChanged(GameManager.Instance.CurrentState);
        }

        waves = FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            waves.OnWaveStarted += HandleWaveStarted;
            waves.OnWaveCompleted += HandleWaveCompleted;
            waves.OnLootPhaseStarted += HandleLootStarted;
            waves.OnLootPhaseEnded += HandleLootEnded;
            waves.OnAllWavesCompleted += HandleAllWavesCompleted;
        }
    }

    void Update()
    {
        AttachIfNeeded();
        DrainMarkers();

        if (pending != null && Time.unscaledTime >= pendingDeadline)
        {
            Debug.LogWarning("MusicController: o marker '" + pending.waitForMarker + "' não chegou em " + pending.maxWaitSeconds +
                             "s (estágio '" + pending.name + "'). Disparando 'On Reached' mesmo assim. Confira o nome do marker e se o destino da transição tem um marker nomeado.", this);
            Reach(pending);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        if (waves != null)
        {
            waves.OnWaveStarted -= HandleWaveStarted;
            waves.OnWaveCompleted -= HandleWaveCompleted;
            waves.OnLootPhaseStarted -= HandleLootStarted;
            waves.OnLootPhaseEnded -= HandleLootEnded;
            waves.OnAllWavesCompleted -= HandleAllWavesCompleted;
        }

        // Solta o callback ANTES de liberar o handle (senão o FMOD chamaria memória inválida).
        if (hasAttached && attached.isValid())
        {
            attached.setCallback(null, 0);
            attached.setUserData(IntPtr.Zero);
        }
        hasAttached = false;

        if (hasOwnInstance)
        {
            ownInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            ownInstance.release();
            hasOwnInstance = false;
        }

        if (queueHandle.IsAllocated) queueHandle.Free();
    }

    // ------------------------------------------------------------------ gatilhos

    void HandleStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Menu: RunTrigger(Trigger.Menu, 0); break;
            case GameState.GameOver: RunTrigger(Trigger.GameOver, 0); break;
            case GameState.Playing:
                if (previousState != GameState.Paused) RunTrigger(Trigger.GameStarted, 0);
                break;
        }
        previousState = state;
    }

    void HandleWaveStarted(int wave) { RunTrigger(Trigger.WaveStarted, wave); }
    void HandleWaveCompleted(int wave) { RunTrigger(Trigger.WaveCompleted, wave); }
    void HandleLootStarted(int wave) { RunTrigger(Trigger.LootPhaseStarted, wave); }
    void HandleLootEnded(int wave) { RunTrigger(Trigger.LootPhaseEnded, wave); }
    void HandleAllWavesCompleted() { RunTrigger(Trigger.AllWavesCompleted, 0); }

    void RunTrigger(Trigger trigger, int wave)
    {
        if (stages == null) return;
        foreach (Stage s in stages)
        {
            if (s == null || s.trigger != trigger) continue;
            if (s.wave != 0 && s.wave != wave) continue;
            RunStage(s);
        }
    }

    // Nomes dos estágios (para listas de seleção no Inspector).
    public IEnumerable<string> StageNames
    {
        get
        {
            if (stages == null) yield break;
            foreach (Stage s in stages) if (s != null && !string.IsNullOrEmpty(s.name)) yield return s.name;
        }
    }

    // true enquanto o estágio foi pedido e o FMOD ainda não passou pelo marker dele.
    public bool IsStagePending(string stageName)
    {
        return pending != null && string.Equals(pending.name, stageName, StringComparison.OrdinalIgnoreCase);
    }

    // Pede um estágio pelo nome (gatilho Manual, ou chamado de um UnityEvent/código).
    public void RequestStage(string stageName)
    {
        if (stages == null) return;
        foreach (Stage s in stages)
        {
            if (s != null && string.Equals(s.name, stageName, StringComparison.OrdinalIgnoreCase)) { RunStage(s); return; }
        }
        Debug.LogWarning("MusicController: não existe estágio chamado '" + stageName + "'.", this);
    }

    void RunStage(Stage s)
    {
        if (logChanges) Debug.Log("[Music] estágio '" + s.name + "' (" + s.trigger + (s.wave != 0 ? " wave " + s.wave : "") + ")", this);

        if (s.changes != null)
        {
            foreach (ParameterChange c in s.changes) Apply(c);
        }

        CurrentStage = s;
        s.onRequested?.Invoke();
        StageRequested?.Invoke(s);

        if (string.IsNullOrEmpty(s.waitForMarker))
        {
            if (pending == s) pending = null;
            Reach(s);
            return;
        }

        if (pending != null && pending != s && logChanges)
            Debug.Log("[Music] estágio '" + pending.name + "' foi substituído por '" + s.name + "' antes do marker chegar.", this);

        // Descarta markers antigos: só vale marker que passar DEPOIS do pedido.
        lock (queue.sync) { queue.names.Clear(); }
        pending = s;
        pendingDeadline = Time.unscaledTime + s.maxWaitSeconds;
    }

    void Reach(Stage s)
    {
        if (pending == s) pending = null;
        if (logChanges) Debug.Log("[Music] estágio '" + s.name + "' ALCANÇADO" + (string.IsNullOrEmpty(s.waitForMarker) ? "" : " (marker " + s.waitForMarker + ")"), this);
        s.onReached?.Invoke();
        StageReached?.Invoke(s);
    }

    // ------------------------------------------------------------------ parâmetros

    void Apply(ParameterChange c)
    {
        if (c == null || string.IsNullOrEmpty(c.parameter)) return;

        if (c.isGlobal)
        {
            FMOD.RESULT r = c.mode == ChangeMode.Label
                ? RuntimeManager.StudioSystem.setParameterByNameWithLabel(c.parameter, c.label ?? "")
                : RuntimeManager.StudioSystem.setParameterByName(c.parameter, c.value);
            Report(r, c);
        }
        else
        {
            desiredLocal[c.parameter] = c;
            FMOD.Studio.EventInstance inst;
            if (TryGetInstance(out inst)) ApplyLocal(inst, c);
        }
    }

    void ApplyLocal(FMOD.Studio.EventInstance inst, ParameterChange c)
    {
        FMOD.RESULT r = c.mode == ChangeMode.Label
            ? inst.setParameterByNameWithLabel(c.parameter, c.label ?? "")
            : inst.setParameterByName(c.parameter, c.value);
        Report(r, c);
    }

    void Report(FMOD.RESULT r, ParameterChange c)
    {
        string what = c.mode == ChangeMode.Label ? "label '" + c.label + "'" : "valor " + c.value;
        if (r != FMOD.RESULT.OK)
            Debug.LogWarning("MusicController: falha ao setar '" + c.parameter + "' (" + what + "): " + r +
                             ". Confira o nome do parâmetro, se é global/local e se o label existe EXATAMENTE assim no FMOD.", this);
        else if (logChanges)
            Debug.Log("[Music] " + c.parameter + " = " + what + (c.isGlobal ? " (global)" : " (local)"), this);
    }

    bool TryGetInstance(out FMOD.Studio.EventInstance inst)
    {
        inst = default(FMOD.Studio.EventInstance);
        if (emitter != null)
        {
            if (!emitter.IsPlaying()) return false;
            inst = emitter.EventInstance;
            return inst.isValid();
        }
        if (hasOwnInstance)
        {
            inst = ownInstance;
            return inst.isValid();
        }
        return false;
    }

    // ------------------------------------------------------------------ markers

    void AttachIfNeeded()
    {
        FMOD.Studio.EventInstance inst;
        if (!TryGetInstance(out inst)) return;
        if (hasAttached && attached.handle == inst.handle) return;

        inst.setUserData(GCHandle.ToIntPtr(queueHandle));
        FMOD.RESULT r = inst.setCallback(markerCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
        if (r != FMOD.RESULT.OK) Debug.LogWarning("MusicController: não consegui ouvir os markers: " + r, this);

        attached = inst;
        hasAttached = true;

        // A instância acabou de existir: reaplica os parâmetros locais pedidos até agora.
        foreach (KeyValuePair<string, ParameterChange> kv in desiredLocal) ApplyLocal(inst, kv.Value);
    }

    // Roda na thread do FMOD: só enfileira. Nada de Unity API aqui.
    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT MarkerCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr eventPtr, IntPtr parameters)
    {
        if (type != FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER) return FMOD.RESULT.OK;

        FMOD.Studio.EventInstance inst = new FMOD.Studio.EventInstance(eventPtr);
        IntPtr userData;
        if (inst.getUserData(out userData) != FMOD.RESULT.OK || userData == IntPtr.Zero) return FMOD.RESULT.OK;

        GCHandle handle = GCHandle.FromIntPtr(userData);
        MarkerQueue q = handle.Target as MarkerQueue;
        if (q == null) return FMOD.RESULT.OK;

        FMOD.Studio.TIMELINE_MARKER_PROPERTIES props =
            (FMOD.Studio.TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(FMOD.Studio.TIMELINE_MARKER_PROPERTIES));
        string markerName = props.name;

        lock (q.sync) { q.names.Add(markerName); }
        return FMOD.RESULT.OK;
    }

    readonly List<string> drained = new List<string>();

    void DrainMarkers()
    {
        drained.Clear();
        lock (queue.sync)
        {
            if (queue.names.Count == 0) return;
            drained.AddRange(queue.names);
            queue.names.Clear();
        }

        foreach (string m in drained)
        {
            if (logMarkers) Debug.Log("[Music] marker passou: '" + m + "'", this);
            if (pending != null && string.Equals(m, pending.waitForMarker, StringComparison.OrdinalIgnoreCase))
            {
                Reach(pending);
                break;
            }
        }
    }
}
