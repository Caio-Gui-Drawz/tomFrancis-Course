using UnityEngine;

// Multiplicador global aplicado por cima da velocidade relativa de cada camada de parallax.
public class ParallaxSpeedController : MonoBehaviour
{
    public static ParallaxSpeedController Instance;

    public float menuSpeed = 0.3f;
    public float startingPlaySpeed = 0.3f; // velocidade ao começar uma corrida nova
    public float maxPlaySpeed = 1f;
    public float gameOverSpeed = 0.1f;
    public float accelerationRate = 0.5f; // unidades de velocidade ganhas por segundo

    [Header("Fase de loot (pausa entre waves)")]
    [Tooltip("Multiplica a velocidade das TRÊS camadas ao mesmo tempo durante a fase de loot. 0.35 = tudo a 35% da velocidade normal (dá a impressão de que a nave desacelerou).")]
    [Range(0f, 1.5f)] public float lootSpeedMultiplier = 0.35f;
    [Tooltip("Segundos para desacelerar ao entrar na fase de loot.")]
    public float lootSlowDownSeconds = 0.8f;
    [Tooltip("Segundos para voltar à velocidade normal ao sair da fase de loot.")]
    public float lootSpeedUpSeconds = 1.2f;

    // Velocidade "base" (menu / aceleração da corrida), SEM o multiplicador extra.
    public float CurrentGlobalSpeed { get; private set; }

    // Multiplicador extra atual (1 = normal). Muda suavemente com SetExtraMultiplier.
    public float ExtraMultiplier { get; private set; } = 1f;

    // O que os ParallaxMover realmente usam: base x extra.
    public float EffectiveSpeed { get { return CurrentGlobalSpeed * ExtraMultiplier; } }

    float targetSpeed;
    bool accelerating;
    GameState previousState = GameState.Menu;

    float extraFrom = 1f;
    float extraTo = 1f;
    float extraTime;
    float extraDuration;

    void Awake()
    {
        Instance = this;
        CurrentGlobalSpeed = menuSpeed;
        targetSpeed = menuSpeed;
    }

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // Chamado pelo WaveManager ao começar/terminar a fase de loot.
    public void EnterLootSlowdown()
    {
        SetExtraMultiplier(lootSpeedMultiplier, lootSlowDownSeconds);
    }

    public void ExitLootSlowdown()
    {
        SetExtraMultiplier(1f, lootSpeedUpSeconds);
    }

    // Leva o multiplicador extra até 'target' em 'seconds' segundos (curva suave). 0 = instantâneo.
    public void SetExtraMultiplier(float target, float seconds)
    {
        extraFrom = ExtraMultiplier;
        extraTo = target;
        extraTime = 0f;
        extraDuration = Mathf.Max(0f, seconds);
        if (extraDuration <= 0f) ExtraMultiplier = target;
    }

    void ResetExtra()
    {
        ExtraMultiplier = 1f;
        extraFrom = 1f;
        extraTo = 1f;
        extraTime = 0f;
        extraDuration = 0f;
    }

    void HandleStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Menu:
                CurrentGlobalSpeed = menuSpeed;
                targetSpeed = menuSpeed;
                accelerating = false;
                ResetExtra();
                break;

            case GameState.Playing:
                if (previousState != GameState.Paused)
                {
                    // Corrida nova (veio do Menu ou de um replay) — reseta e acelera do zero.
                    CurrentGlobalSpeed = startingPlaySpeed;
                    ResetExtra();
                }
                // Se veio de Paused, não mexe em CurrentGlobalSpeed — só retoma a aceleração de onde parou.
                targetSpeed = maxPlaySpeed;
                accelerating = true;
                break;

            case GameState.Paused:
                // Não precisa fazer nada aqui: como o seu PauseMenu já usa Time.timeScale = 0,
                // o Update() abaixo já congela sozinho (Time.deltaTime vira 0).
                break;

            case GameState.GameOver:
                CurrentGlobalSpeed = gameOverSpeed;
                targetSpeed = gameOverSpeed;
                accelerating = false;
                ResetExtra();
                break;
        }

        previousState = newState;
    }

    void Update()
    {
        UpdateExtra();

        if (!accelerating) return;

        CurrentGlobalSpeed = Mathf.MoveTowards(CurrentGlobalSpeed, targetSpeed, accelerationRate * Time.deltaTime);
        if (Mathf.Approximately(CurrentGlobalSpeed, targetSpeed))
        {
            accelerating = false;
        }
    }

    void UpdateExtra()
    {
        if (Mathf.Approximately(ExtraMultiplier, extraTo)) return;

        extraTime += Time.deltaTime;
        float k = extraDuration > 0f ? Mathf.Clamp01(extraTime / extraDuration) : 1f;
        k = k * k * (3f - 2f * k); // smoothstep: começa e termina devagar
        ExtraMultiplier = Mathf.Lerp(extraFrom, extraTo, k);
        if (k >= 1f) ExtraMultiplier = extraTo;
    }

    // Para testar a sensação sem esperar uma wave: clique nos três pontinhos do componente em Play mode.
    [ContextMenu("Testar: entrar na fase de loot")]
    void TestEnter() { EnterLootSlowdown(); }

    [ContextMenu("Testar: sair da fase de loot")]
    void TestExit() { ExitLootSlowdown(); }
}
