using UnityEngine;
using UnityEngine.SceneManagement;

// Coloque num objeto sempre ativo (ex: o Canvas), arrastando o painel de Game Over em gameOverPanel.
public class GameOverMenu : MonoBehaviour
{
    public GameObject gameOverPanel;

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
    }

    // Ligar no OnClick do botão "Jogar de Novo".
    public void PlayAgainButton()
    {
        GameManager.startPlayingOnLoad = true;
        ReloadScene();
    }

    // Ligar no OnClick do botão "Menu Principal".
    public void BackToMenuButton()
    {
        GameManager.startPlayingOnLoad = false;
        ReloadScene();
    }

    void ReloadScene()
    {
        Time.timeScale = 1f; // por segurança, caso a morte tenha acontecido com o jogo pausado
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
