using UnityEngine;

// Coloque num objeto sempre ativo (ex: o Canvas), arrastando o painel do HUD em hudPanel.
public class HUDController : MonoBehaviour
{
    public GameObject hudPanel;

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
        HandleStateChanged(GameManager.Instance.CurrentState); // já aplica o estado atual (antes do primeiro evento)
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
