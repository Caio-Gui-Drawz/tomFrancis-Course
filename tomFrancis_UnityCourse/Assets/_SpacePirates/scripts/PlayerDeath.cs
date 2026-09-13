using UnityEngine;

// Colocar no mesmo objeto que tem o healthSystem do player.
public class PlayerDeath : MonoBehaviour
{
    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        GameManager.Instance.SetState(GameState.GameOver);
    }
}
