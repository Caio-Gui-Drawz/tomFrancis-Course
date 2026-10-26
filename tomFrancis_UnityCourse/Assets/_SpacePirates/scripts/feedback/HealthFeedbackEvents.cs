using UnityEngine;
using UnityEngine.Events;

// Ponte entre o healthSystem e os feedbacks. No Inspector você liga o que quiser em cada evento
// (ShakeFeedback.Play, CameraShakeFeedback.Play, VfxFeedback.Play, qualquer função pública...).
[RequireComponent(typeof(healthSystem))]
public class HealthFeedbackEvents : MonoBehaviour
{
    [Tooltip("Disparado quando toma dano e SOBREVIVE.")]
    public UnityEvent onDamaged;

    [Tooltip("Disparado quando morre.")]
    public UnityEvent onDeath;

    [Tooltip("Ligado = o golpe que mata também dispara 'On Damaged' (além de 'On Death').")]
    public bool triggerDamagedOnLethalHit = false;

    healthSystem health;

    void Awake()
    {
        health = GetComponent<healthSystem>();
    }

    // Inscreve/desinscreve no Enable/Disable pra funcionar com objetos do pool.
    void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    void HandleDamaged(float amount)
    {
        if (health.currentHealth <= 0f && !triggerDamagedOnLethalHit) return;
        onDamaged.Invoke();
    }

    void HandleDeath()
    {
        onDeath.Invoke();
    }
}
