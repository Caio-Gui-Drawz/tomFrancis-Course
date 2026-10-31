using UnityEngine;

// Colocar nos prefabs de MEDKIT voador, junto com o healthSystem (o HP do medkit é o "Max Health"
// dele). Quando é destruído, o player recupera vida.
public class HealthPickupReward : MonoBehaviour
{
    [Tooltip("Quanto de vida o player recupera (até o máximo dele).")]
    public float healAmount = 3f;

    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        if (references.thePlayer == null) return;

        healthSystem playerHealth = references.thePlayer.GetComponent<healthSystem>();
        if (playerHealth == null || playerHealth.currentHealth <= 0f) return;

        playerHealth.Heal(healAmount);
        AudioEvents.PlayPlayerHeal();
    }
}
