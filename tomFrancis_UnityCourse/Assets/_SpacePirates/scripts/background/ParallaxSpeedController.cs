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

    public float CurrentGlobalSpeed { get; private set; }

    float targetSpeed;
    bool accelerating;
    GameState previousState = GameState.Menu;

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

    void HandleStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.Menu:
                CurrentGlobalSpeed = menuSpeed;
                targetSpeed = menuSpeed;
                accelerating = false;
                break;

            case GameState.Playing:
                if (previousState != GameState.Paused)
                {
                    // Corrida nova (veio do Menu ou de um replay) — reseta e acelera do zero.
                    CurrentGlobalSpeed = startingPlaySpeed;
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
                break;
        }

        previousState = newState;
    }

    void Update()
    {
        if (!accelerating) return;

        CurrentGlobalSpeed = Mathf.MoveTowards(CurrentGlobalSpeed, targetSpeed, accelerationRate * Time.deltaTime);
        if (Mathf.Approximately(CurrentGlobalSpeed, targetSpeed))
        {
            accelerating = false;
        }
    }
}
