using UnityEngine;
using UnityEngine.UI;

// Colocar num Image de UI dentro do HUD, com Image Type = Filled (Fill Method: Horizontal).
public class PlayerHUDHealthBar : MonoBehaviour
{
    public healthSystem playerHealth; // arraste aqui o healthSystem do player
    public Image fillImage; // o próprio Image (pode arrastar este mesmo objeto se o script estiver nele)

    void OnEnable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth); // estado inicial
    }

    void OnDisable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    void HandleHealthChanged(float current, float max)
    {
        fillImage.fillAmount = current / max;
    }
}
