using UnityEngine;

// Colocar em um GameObject vazio na cena (ex: "GameManager"), uma única vez.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int currentScore;

    public event System.Action<int> OnScoreChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        OnScoreChanged?.Invoke(currentScore); // avisa o estado inicial (0) pra quem já estiver escutando
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
        OnScoreChanged?.Invoke(currentScore);
    }
}
