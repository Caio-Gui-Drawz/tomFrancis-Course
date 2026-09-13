using UnityEngine;
using TMPro;

// Colocar num TMP_Text dentro do HUD.
public class ScoreHUD : MonoBehaviour
{
    public TMP_Text scoreText;

    void OnEnable()
    {
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        HandleScoreChanged(ScoreManager.Instance.currentScore); // estado inicial
    }

    void OnDisable()
    {
        ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    void HandleScoreChanged(int newScore)
    {
        scoreText.text = "Score: " + newScore;
    }
}
