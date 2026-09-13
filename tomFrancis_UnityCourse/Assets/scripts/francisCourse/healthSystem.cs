using UnityEngine;

public class healthSystem : MonoBehaviour
{
    public float maxHealth;
    public float currentHealth;

    public GameObject healthBarPrefab; // pode deixar vazio se não quiser barra visível
    public float healthBarOffset = 1.5f;

    public GameObject deathEffectPrefab;

    // Avisa quem estiver inscrito que esta entidade morreu, sem decidir sozinho o que isso significa.
    public event System.Action OnDeath;

    healthBarBehavior myHealthBar;

    void OnEnable()
    {
        currentHealth = maxHealth;

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
        if (currentHealth <= 0) return; // já está morto, ignora dano extra

        currentHealth -= damageAmount;

        if (currentHealth <= 0) // acabou de morrer agora
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
        // Esconde a barra em vez de destruir — evita instanciar/destruir de novo a cada reuso do pool.
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
