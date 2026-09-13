using UnityEngine;

// Coloque num objeto sempre ativo (ex: o Canvas), arrastando o painel do HUD em hudPanel.
public class HUDController : MonoBehaviour
{
    public GameObject hudPanel;

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
        hudPanel.SetActive(newState == GameState.Playing || newState == GameState.Paused);
    }
}
