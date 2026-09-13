using UnityEngine;

public enum GameState { Menu, Playing, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState CurrentState { get; private set; }

    public event System.Action<GameState> OnStateChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Dispara o estado inicial para que menus/spawners/player já nasçam configurados certo.
        SetState(GameState.Menu);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}
