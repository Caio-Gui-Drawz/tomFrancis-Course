using UnityEngine;

// Colocar em um GameObject vazio na cena (ex: "GameManager"), uma única vez.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int currentScore;

    void Awake()
    {
        Instance = this;
    }

    public void AddScore(int amount)
    {
        currentScore += amount;
        Debug.Log("Score atual: " + currentScore);
        // TODO: quando o HUD existir, trocar o Debug.Log por atualização de um Text/TMP_Text.
    }
}
