using UnityEngine;

public class enemy2D : MonoBehaviour
{
    public float damage;

    [Tooltip("Ligado = ao encostar no player, este inimigo MORRE de verdade (efeito de morte/explosão, som de morte, filhos do SpawnOnDeath), como se tivesse sido destruído por bala. Desligado = só some, sem efeito (comportamento antigo). Use na mina.")]
    public bool dieOnPlayerContact = false;

    [Tooltip("Ligado = ao encostar, aplica o 'Damage' direto no player. Desligue na mina se o dano já vem da explosão (senão o player leva os dois).")]
    public bool applyContactDamage = true;

    [Header("Pontuação")]
    [Tooltip("Pontos que o player ganha quando este inimigo MORRE (por tiro, explosão...). 0 = não dá pontos. Não conta quando ele morre ao encostar no player (ex.: mina).")]
    [Min(0)] public int scoreValue = 0;

    healthSystem health;
    bool diedByPlayerContact;

    void Awake()
    {
        health = GetComponent<healthSystem>();
    }

    // Inscreve no Enable/Disable pra funcionar com objetos do pool.
    void OnEnable()
    {
        diedByPlayerContact = false;
        if (health != null) health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    void HandleDeath()
    {
        if (scoreValue <= 0 || diedByPlayerContact) return;
        if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(scoreValue);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Player2D>() != null)
        {
            healthSystem theirHealthSystem = other.GetComponent<healthSystem>();
            if (theirHealthSystem != null)
            {
                if (applyContactDamage) theirHealthSystem.TakeDamage(damage);

                if (dieOnPlayerContact)
                {
                    healthSystem ourHealthSystem = GetComponent<healthSystem>();
                    if (ourHealthSystem != null && ourHealthSystem.currentHealth > 0f)
                    {
                        // Morte de verdade: toca o efeito (explosão), o som, avisa o OnDeath e devolve ao pool.
                        diedByPlayerContact = true; // morrer no player não dá pontos
                        ourHealthSystem.TakeDamage(ourHealthSystem.currentHealth);
                        return;
                    }
                }

                gameObject.SetActive(false);
            }
        }
    }
    
}
