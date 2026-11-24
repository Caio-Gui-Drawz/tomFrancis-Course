using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Coloque num objeto sempre ativo (ex: o Canvas), arrastando o painel de Game Over em gameOverPanel.
public class GameOverMenu : MonoBehaviour
{
    public GameObject gameOverPanel;
    [Tooltip("Tela de vitória (quando todas as waves terminam e o GameManager.Victory() é chamado). Vazio = sem tela de vitória.")]
    public GameObject victoryPanel;
    [Tooltip("Dispara quando a tela de vitória aparece (som, música...).")]
    public UnityEvent onVictoryShown;
    [Tooltip("Opcional: SceneTransition que faz o fade out da música e só então recarrega a cena. Vazio = recarrega na hora.")]
    public SceneTransition transition;

    [Header("Vitória automática")]
    [Tooltip("Ligado = quando todas as waves terminam, a tela de vitória aparece sozinha (sem precisar ligar evento nenhum).")]
    public bool victoryOnAllWaves = true;
    [Tooltip("Espera (s) depois do fim das waves antes de mostrar a vitória.")]
    [Min(0f)] public float victoryDelaySeconds = 1.0f;

    WaveManager waves;

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void Start()
    {
        waves = FindFirstObjectByType<WaveManager>();
        if (waves != null) waves.OnAllWavesCompleted += HandleAllWavesCompleted;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (waves != null) waves.OnAllWavesCompleted -= HandleAllWavesCompleted;
    }

    void HandleAllWavesCompleted()
    {
        if (victoryOnAllWaves) StartCoroutine(VictoryRoutine());
    }

    IEnumerator VictoryRoutine()
    {
        if (victoryDelaySeconds > 0f) yield return new WaitForSecondsRealtime(victoryDelaySeconds);

        // Só mostra se o jogador ainda está jogando (não morreu / não voltou ao menu nesse meio tempo).
        GameState st = GameManager.Instance.CurrentState;
        if (st == GameState.Playing || st == GameState.Paused) GameManager.Instance.Victory();
    }

    void HandleStateChanged(GameState newState)
    {
        UIAnimator.SetPanelVisible(gameOverPanel, newState == GameState.GameOver);
        UIAnimator.SetPanelVisible(victoryPanel, newState == GameState.Victory);

        if (newState == GameState.GameOver)
        {
            AudioEvents.PlayDeathScreen(); // som só da interface (o som da morte em si é o playerDeath)
        }

        if (newState == GameState.Victory)
        {
            if (victoryPanel == null) Debug.LogWarning("GameOverMenu: o estado Victory começou, mas o campo 'Victory Panel' está vazio. Arraste o painel de vitória.", this);
            onVictoryShown?.Invoke();
        }
    }

    // Ligar no OnClick do botão "Jogar de Novo".
    public void PlayAgainButton()
    {
        if (transition != null) { transition.PlayAgain(); return; }
        GameManager.startPlayingOnLoad = true;
        ReloadScene();
    }

    // Ligar no OnClick do botão "Menu Principal".
    public void BackToMenuButton()
    {
        if (transition != null) { transition.BackToMenu(); return; }
        GameManager.startPlayingOnLoad = false;
        ReloadScene();
    }

    void ReloadScene()
    {
        Time.timeScale = 1f; // por segurança, caso a morte tenha acontecido com o jogo pausado
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
