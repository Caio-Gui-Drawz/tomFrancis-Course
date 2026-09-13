using UnityEngine;

public class healthSystem : MonoBehaviour
{
    public float maxHealth;
    public float currentHealth;

    public GameObject healthBarPrefab; // pode deixar vazio se não quiser barra flutuante (ex: no player)
    public float healthBarOffset = 1.5f;

    public GameObject deathEffectPrefab;

    // Avisa quem estiver inscrito que esta entidade morreu.
    public event System.Action OnDeath;

    // Avisa toda vez que a vida muda (dano ou reset) — usado por UI fixa, tipo o HUD do player.
    public event System.Action<float, float> OnHealthChanged;

    healthBarBehavior myHealthBar;

    void OnEnable()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (healthBarPrefab != null && references.Canvas != null && myHealthBar == null)
        {
            GameObject healthBarObject = Instantiate(healthBarPrefab, references.Canvas.transform);
            myHealthBar = healthBarObject.GetComponent<healthBarBehavior>();
        }

        if (myHealthBar != null)
        {
            myHealthBar.gameObject.SetActive(true);
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, transform.rotation);
            }

            OnDeath?.Invoke();
            PoolManager.Instance.Return(gameObject);
        }
    }

    void OnDisable()
    {
        if (myHealthBar != null)
        {
            myHealthBar.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (myHealthBar == null) return;

        myHealthBar.ShowHealthFraction(currentHealth / maxHealth);
        myHealthBar.transform.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * healthBarOffset);
    }
}
