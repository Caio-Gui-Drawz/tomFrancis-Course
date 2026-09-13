using UnityEngine;

public enum GameState { Menu, Playing, Paused, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // Usado pelo GameOverMenu para avisar, antes de recarregar a cena, se deve
    // cair direto na gameplay (Jogar de Novo) ou no menu (Voltar ao Menu).
    public static bool startPlayingOnLoad = false;

    public GameState CurrentState { get; private set; }

    public event System.Action<GameState> OnStateChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetState(startPlayingOnLoad ? GameState.Playing : GameState.Menu);
        startPlayingOnLoad = false; // reseta para a próxima vez
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
}
