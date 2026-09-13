using UnityEngine;

// Colocar em um objeto que fica sempre ativo na cena de gameplay (não dentro do painel que
// aparece/some, senão o Update para de rodar quando o painel estiver desligado).
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel; // arraste aqui o painel de UI do menu de pausa

    void OnEnable()
    {
       
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (GameManager.Instance.CurrentState == GameState.Playing)
        {
            pauseGame();
        }
            
        else if (GameManager.Instance.CurrentState == GameState.Paused)
        {
            unpauseGame();
        }
            
    }

    public void pauseGame()
    {
        GameManager.Instance.SetState(GameState.Paused);
            Time.timeScale = 0f; // Pausa o jogo
    }

    public void unpauseGame()
    {
        GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = 1f; // Retoma o jogo
    }
    void HandleStateChanged(GameState newState)
    {
        pausePanel.SetActive(newState == GameState.Paused);
    }

    // Ligar este método no OnClick do botão "Continuar" (via Inspector).
    public void ResumeButton()
    {
        GameManager.Instance.SetState(GameState.Playing);
    }

}
