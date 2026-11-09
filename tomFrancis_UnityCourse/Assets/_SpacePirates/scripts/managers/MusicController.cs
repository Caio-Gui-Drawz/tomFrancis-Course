using UnityEngine;
using FMODUnity;

// Música dinâmica do FMOD: um evento com um parâmetro float que alterna entre duas músicas.
//   valor "calmo"  (padrão 0) = música do menu e da fase de loot
//   valor "combate" (padrão 1) = música das waves
//
// Quando troca:
//   - Menu / Game Over ........................ calmo
//   - Ao entrar em Playing (começo da corrida) . combate (já na janela de introdução, ANTES da wave 1 começar)
//   - Fim de cada wave (OnWaveCompleted) ....... calmo
//   - Início de cada wave (OnWaveStarted) ...... combate
// Pausar e voltar não mexe na música.
//
// Coloque em um GameObject da cena (ex.: junto do AudioEvents). Dois modos:
//   A) arraste o StudioEventEmitter que já toca a música: ele continua tocando e este script só muda o parâmetro;
//   B) sem emitter: escolha o evento em "Music" e o script toca sozinho (remova o emitter antigo para não tocar duas).
public class MusicController : MonoBehaviour
{
    [Header("FMOD")]
    [Tooltip("OPÇÃO A: arraste aqui o Studio Event Emitter que já toca a música na cena. O emitter continua sendo quem toca; este script só muda o parâmetro. Deixe o parâmetro INICIAL dele igual ao valor calmo.")]
    public StudioEventEmitter emitter;

    [Tooltip("OPÇÃO B (se não usar o emitter): evento da música dinâmica. Aí o próprio script toca o evento.")]
    public EventReference music;

    [Tooltip("Parâmetro que alterna entre as duas músicas.")]
    [ParamRef] public string parameter;

    [Tooltip("Ligado = o parâmetro é GLOBAL no FMOD (Window > Parameters). Desligado = é um parâmetro local do evento.")]
    public bool parameterIsGlobal = false;

    [Header("Valores do parâmetro")]
    [Tooltip("Menu, fase de loot e tela de game over.")]
    public float calmValue = 0f;
    [Tooltip("Waves (e a introdução antes da wave 1).")]
    public float combatValue = 1f;

    [Header("Quando fica calmo")]
    [Tooltip("Ligado = volta à música calma ao FIM de cada wave (inclui o intervalo entre waves sem loot). Desligado = só durante a fase de loot.")]
    public bool calmOnWaveEnd = true;

    FMOD.Studio.EventInstance instance;
    bool hasInstance;
    bool emitterApplied;
    WaveManager waves;
    GameState previousState = GameState.Menu;
    float currentValue;

    void Start()
    {
        currentValue = calmValue;

        if (emitter != null)
        {
            // O emitter começa a tocar no gatilho dele; o valor é reaplicado assim que ele estiver tocando (Update).
        }
        else if (!music.IsNull)
        {
            instance = RuntimeManager.CreateInstance(music);
            hasInstance = true;
            ApplyValue(calmValue);
            instance.start();
        }
        else
        {
            Debug.LogWarning("MusicController: arraste um Studio Event Emitter ou escolha um evento de música.", this);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            previousState = GameState.Menu;
            // Se o GameManager já definiu o estado antes de nós, aplica agora.
            if (GameManager.Instance.CurrentState != GameState.Menu) HandleStateChanged(GameManager.Instance.CurrentState);
        }

        waves = FindFirstObjectByType<WaveManager>();
        if (waves != null)
        {
            waves.OnWaveStarted += HandleWaveStarted;
            waves.OnWaveCompleted += HandleWaveCompleted;
            waves.OnLootPhaseStarted += HandleLootStarted;
            waves.OnAllWavesCompleted += HandleAllWavesCompleted;
        }
    }

    void Update()
    {
        // Com emitter: o evento só existe depois que ele começa a tocar; aplica o valor atual nesse momento.
        if (emitter != null && !emitterApplied && emitter.IsPlaying())
        {
            emitterApplied = true;
            ApplyValue(currentValue);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;

        if (waves != null)
        {
            waves.OnWaveStarted -= HandleWaveStarted;
            waves.OnWaveCompleted -= HandleWaveCompleted;
            waves.OnLootPhaseStarted -= HandleLootStarted;
            waves.OnAllWavesCompleted -= HandleAllWavesCompleted;
        }

        if (hasInstance)
        {
            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
            hasInstance = false;
        }
    }

    void HandleStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
            case GameState.GameOver:
                SetValue(calmValue);
                break;

            case GameState.Playing:
                // Voltar de uma pausa não troca a música; corrida nova entra já na música de combate (introdução).
                if (previousState != GameState.Paused) SetValue(combatValue);
                break;
        }

        previousState = state;
    }

    void HandleWaveStarted(int wave) { SetValue(combatValue); }
    void HandleWaveCompleted(int wave) { if (calmOnWaveEnd) SetValue(calmValue); }
    void HandleLootStarted(int wave) { SetValue(calmValue); }
    void HandleAllWavesCompleted() { SetValue(calmValue); }

    public void SetValue(float value)
    {
        if (Mathf.Approximately(currentValue, value)) return;
        currentValue = value;
        ApplyValue(value);
    }

    void ApplyValue(float value)
    {
        if (string.IsNullOrEmpty(parameter)) return;

        if (parameterIsGlobal)
        {
            RuntimeManager.StudioSystem.setParameterByName(parameter, value);
        }
        else if (emitter != null)
        {
            if (emitter.IsPlaying()) emitter.SetParameter(parameter, value);
        }
        else if (hasInstance)
        {
            instance.setParameterByName(parameter, value);
        }
    }
}
