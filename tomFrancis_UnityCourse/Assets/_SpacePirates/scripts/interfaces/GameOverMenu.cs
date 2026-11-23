using UnityEngine;
using UnityEngine.SceneManagement;

// Coloque num objeto sempre ativo (ex: o Canvas), arrastando o painel de Game Over em gameOverPanel.
public class GameOverMenu : MonoBehaviour
{
    public GameObject gameOverPanel;
    [Tooltip("Opcional: SceneTransition que faz o fade out da música e só então recarrega a cena. Vazio = recarrega na hora.")]
    public SceneTransition transition;

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
        gameOverPanel.SetActive(newState == GameState.GameOver);

        if (newState == GameState.GameOver)
        {
            AudioEvents.PlayDeathScreen(); // som só da interface (o som da morte em si é o playerDeath)
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
