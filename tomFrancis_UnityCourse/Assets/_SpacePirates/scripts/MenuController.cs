using UnityEngine;

// Colocar no objeto do painel/canvas do menu inicial.
public class MenuController : MonoBehaviour
{
    public GameObject menuPanel; // arraste aqui o painel de UI do menu inicial

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
        menuPanel.SetActive(newState == GameState.Menu);
    }

    // Ligar este método no OnClick do botão "Jogar" (via Inspector).
    public void PlayButton()
    {
        GameManager.Instance.SetState(GameState.Playing);
    }
}
