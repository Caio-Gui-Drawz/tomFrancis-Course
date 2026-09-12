using UnityEngine;

// Colocar este componente apenas nos prefabs de TESOURO (não no inimigo comum).
// O valor de scoreValue pode variar por prefab/tier (tesouro pequeno = pouco, "grande" = mais).
public class TreasureReward : MonoBehaviour
{
    public int scoreValue;

    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        ScoreManager.Instance.AddScore(scoreValue);
    }
}
