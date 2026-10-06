using UnityEngine;

public class Player2D : MonoBehaviour
{
    public WeaponData currentWeapon;
    public PlayerWeaponVisual weaponVisual; // opcional — se vazio, usa o spawn antigo

    float secondsSinceLastShot;

    // Ângulo (graus) na direção do mouse, recalculado todo frame — usado só para mirar o
    // tiro. NÃO aplicamos isso em transform.rotation porque isso girava o player inteiro
    // e, com ele, todos os filhos do prefab (incluindo o Spine), causando rotação esquisita.
    float currentAimAngle;

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

        // Calcula o ângulo até o mouse (mas não gira o player com isso — ver comentário
        // em currentAimAngle acima). O braço quem se vira sozinho, via IK (AimArmToMouse).
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = mousePosition - transform.position;
        currentAimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

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
    }

    void FireCurrentWeapon()
    {
        Vector3 spawnPosition = weaponVisual != null
            ? weaponVisual.GetMuzzleWorldPosition()
            : transform.position + transform.right;

        // Prioriza o ângulo real do cano (pós-IK) — só cai pro cálculo cru pela posição do
        // mouse se não houver weaponVisual configurado (ex.: player sem Spine ainda).
        float aimAngle = weaponVisual != null ? weaponVisual.GetMuzzleWorldAngle() : currentAimAngle;
        Quaternion aimRotation = Quaternion.Euler(0, 0, aimAngle);

        for (int i = 0; i < currentWeapon.numberOfProjectiles; i++)
        {
            // Desvio aleatório em graus, simétrico para os dois lados — 0 = mira perfeita.
            float spreadOffset = Random.Range(-currentWeapon.spreadAngleDegrees, currentWeapon.spreadAngleDegrees);
            Quaternion bulletRotation = aimRotation * Quaternion.Euler(0, 0, spreadOffset);

            PoolManager.Instance.Get(currentWeapon.bulletPrefab, spawnPosition, bulletRotation);
        }

        if (weaponVisual != null)
        {
            weaponVisual.PlayShootAnimation();
        }
    }

    // Chamado de fora (ex: WeaponPickupReward) quando o player atira numa arma voadora.
    public void SwapWeapon(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;
        secondsSinceLastShot = 0f; // evita herdar o timing de cooldown da arma anterior
    }
}
