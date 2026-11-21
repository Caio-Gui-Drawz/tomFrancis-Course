using System;
using UnityEngine;
using UnityEngine.Events;

// Colocar no MESMO objeto do player (o que tem Player2D e healthSystem).
// Escuta o que o player pega e dispara UnityEvents, para você ligar efeitos que são FILHOS do player
// (ex.: ParticleSystem.Play, VfxFeedback.Play, ShakeFeedback.Play, som...). O player nunca vai pro pool,
// então não precisa de ParticleDetachFx: é só deixar o ParticleSystem como filho e desligar "Play On Awake".
public class PlayerPickupFeedback : MonoBehaviour
{
    [Serializable]
    public class WeaponFeedback
    {
        [Tooltip("A arma (asset WeaponData) que dispara o evento abaixo quando é pega.")]
        public WeaponData weapon;
        public UnityEvent onPicked;
    }

    [Header("Cura")]
    [Tooltip("Dispara quando a vida do player SOBE (medkit, tecla de debug...).")]
    public UnityEvent onHealed;

    [Header("Armas de caixa")]
    [Tooltip("Um evento por arma. Dispara quando o player pega essa arma (inclusive se pegar a mesma de novo, pois a munição reinicia).")]
    public WeaponFeedback[] weapons;

    [Tooltip("Dispara ao pegar QUALQUER arma de caixa, além do evento específico dela.")]
    public UnityEvent onAnyWeaponPicked;

    [Header("Volta pra pistola")]
    [Tooltip("Dispara quando a arma de caixa acaba (munição ou tempo) e o player volta pra arma padrão.")]
    public UnityEvent onRevertedToDefault;

    [Header("Debug")]
    public bool log = false;

    Player2D player;
    healthSystem health;
    float lastHealth;
    WeaponData lastWeapon;

    void Awake()
    {
        player = GetComponent<Player2D>();
        health = GetComponent<healthSystem>();
    }

    // Inscreve/desinscreve no Enable/Disable.
    void OnEnable()
    {
        if (health != null)
        {
            lastHealth = health.currentHealth;
            health.OnHealthChanged += HandleHealthChanged;
        }
        if (player != null)
        {
            lastWeapon = player.currentWeapon;
            player.OnWeaponChanged += HandleWeaponChanged;
        }
    }

    void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        if (player != null) player.OnWeaponChanged -= HandleWeaponChanged;
    }

    void HandleHealthChanged(float current, float max)
    {
        // lastHealth > 0: ignora o "enchimento" inicial da vida e qualquer coisa depois da morte.
        bool healed = current > lastHealth && lastHealth > 0f;
        lastHealth = current;
        if (!healed) return;

        if (log) Debug.Log("[PickupFx] curou", this);
        onHealed.Invoke();
    }

    void HandleWeaponChanged(WeaponData weapon)
    {
        WeaponData previous = lastWeapon;
        lastWeapon = weapon;
        if (weapon == null) return;

        if (weapon == player.defaultWeapon)
        {
            // Só conta como "voltou" se antes era uma arma de caixa (ignora trocas pistola -> pistola).
            bool wasDefault = previous == null || previous == player.defaultWeapon;
            if (wasDefault) return;

            if (log) Debug.Log("[PickupFx] voltou pra arma padrão: " + weapon.name, this);
            onRevertedToDefault.Invoke();
            return;
        }

        if (log) Debug.Log("[PickupFx] pegou arma: " + weapon.name, this);
        onAnyWeaponPicked.Invoke();

        if (weapons == null) return;
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponFeedback entry = weapons[i];
            if (entry != null && entry.weapon == weapon && entry.onPicked != null) entry.onPicked.Invoke();
        }
    }
}
