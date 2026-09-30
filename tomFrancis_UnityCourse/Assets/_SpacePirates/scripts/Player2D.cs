using UnityEngine;

public class Player2D : MonoBehaviour
{
    public WeaponData currentWeapon;
    float secondsSinceLastShot;

    bool canAct;

    void Start()
    {
        references.thePlayer = gameObject;
    }

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(GameState newState)
    {
        canAct = (newState == GameState.Playing);
    }

    void Update()
    {
        if (!canAct) return;

        // Atirar (usando a arma atual)
        if (currentWeapon != null)
        {
            secondsSinceLastShot += Time.deltaTime;
            if (secondsSinceLastShot >= currentWeapon.fireRate && Input.GetButton("Fire1"))
            {
                FireCurrentWeapon();
                secondsSinceLastShot = 0;
            }
        }

        // Virar de frente pro mouse
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = mousePosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void FireCurrentWeapon()
    {
        Debug.Log("Atirando com '" + currentWeapon.name + "' | Projéteis: " + currentWeapon.numberOfProjectiles + " | Spread: " + currentWeapon.spreadAngleDegrees);

        for (int i = 0; i < currentWeapon.numberOfProjectiles; i++)
        {
            // Desvio aleatório em graus, simétrico para os dois lados — 0 = mira perfeita.
            float spreadOffset = Random.Range(-currentWeapon.spreadAngleDegrees, currentWeapon.spreadAngleDegrees);
            Quaternion bulletRotation = transform.rotation * Quaternion.Euler(0, 0, spreadOffset);

            Instantiate(currentWeapon.bulletPrefab, transform.position + transform.right, bulletRotation);
        }
    }

    // Chamado de fora (ex: WeaponPickupReward) quando o player atira numa arma voadora.
    public void SwapWeapon(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;
        secondsSinceLastShot = 0f; // evita herdar o timing de cooldown da arma anterior
    }
}