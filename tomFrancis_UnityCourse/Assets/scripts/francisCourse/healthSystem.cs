using UnityEngine;
using UnityEngine.Serialization;

public class healthSystem : MonoBehaviour
{
    [FormerlySerializedAs("health")]
    public float maxHealth;
    public float currentHealth;

    public GameObject healthBarPrefab; // pode deixar vazio se não quiser barra visível
    public float healthBarOffset = 1.5f;

    public GameObject deathEffectPrefab;

    // Avisa quem estiver inscrito que esta entidade morreu, sem decidir sozinho o que isso significa
    // (pontos, upgrade, ou nada — quem escuta decide).
    public event System.Action OnDeath;

    healthBarBehavior myHealthBar;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthBarPrefab != null && references.Canvas != null)
        {
            GameObject healthBarObject = Instantiate(healthBarPrefab, references.Canvas.transform);
            myHealthBar = healthBarObject.GetComponent<healthBarBehavior>();
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
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        // Nunca criar nada aqui, é só limpeza.
        if (myHealthBar != null)
        {
            Destroy(myHealthBar.gameObject);
        }
    }

    void Update()
    {
        if (myHealthBar == null) return; // sem barra atribuída, não faz nada

        myHealthBar.ShowHealthFraction(currentHealth / maxHealth);
        myHealthBar.transform.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * healthBarOffset);
    }
}
