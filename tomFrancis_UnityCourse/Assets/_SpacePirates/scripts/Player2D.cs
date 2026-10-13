using UnityEngine;

public class Player2D : MonoBehaviour
{
    public WeaponData currentWeapon;
    public PlayerWeaponVisual weaponVisual; // opcional — se vazio, usa o spawn antigo
    public WaveManager waveManager; // arraste o GameObject do WaveManager aqui

    AimArmToMouse aimArmToMouse; // pego automaticamente do mesmo objeto do weaponVisual

    float secondsSinceLastShot;

    // Ângulo (graus) na direção do mouse, recalculado todo frame — usado só para mirar o
    // tiro. NÃO aplicamos isso em transform.rotation porque isso girava o player inteiro
    // e, com ele, todos os filhos do prefab (incluindo o Spine), causando rotação esquisita.
    float currentAimAngle;

    bool canAct;
    bool inCombatStance; // só pode atirar depois que a wave 1 começa (antes disso: só voando)

    void Start()
    {
        references.thePlayer = gameObject;

        if (weaponVisual != null)
        {
            aimArmToMouse = weaponVisual.GetComponent<AimArmToMouse>();
        }

        // Garante que começa com as mãos abaixadas (sem mirar), até a wave 1 começar.
        SetCombatStance(false);
    }

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted += HandleWaveStarted;
        }
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;

        if (waveManager != null)
        {
            waveManager.OnWaveStarted -= HandleWaveStarted;
        }
    }

    void HandleStateChanged(GameState newState)
    {
        canAct = (newState == GameState.Playing);
    }

    void HandleWaveStarted(int waveNumber)
    {
        // Só entra em modo de combate quando a primeira wave de verdade começar (depois do
        // "secondsBeforeFirstWave" do WaveManager). Futuramente dá pra chamar
        // SetCombatStance(false) de novo pra entrar no modo "desviar" com as 2 mãos na moto.
        if (waveNumber == 1)
        {
            SetCombatStance(true);
        }
    }

    // Liga/desliga o braço de combate (IK mirando + animação de "pronto pra atirar").
    public void SetCombatStance(bool active)
    {
        inCombatStance = active;
        if (weaponVisual != null) weaponVisual.SetAiming(active);
        if (aimArmToMouse != null) aimArmToMouse.SetAiming(active);
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
        if (currentWeapon != null && inCombatStance)
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

        // Direção da bala = a direção pra onde o BRAÇO aponta (ombro -> mouse), a mesma que a
        // IK usa. Assim o tiro sai sempre no sentido do braço, mesmo com o mouse colado no boneco
        // (onde "ponta do cano -> mouse" apontaria pro lado oposto). Não depende do balanço
        // da animação de recuo.
        float aimAngle;
        if (aimArmToMouse != null && aimArmToMouse.HasAim)
        {
            aimAngle = aimArmToMouse.AimAngleDegrees;
        }
        else
        {
            aimAngle = currentAimAngle; // sem Spine configurado: ângulo a partir do player
        }
        Quaternion aimRotation = Quaternion.Euler(0, 0, aimAngle);

        for (int i = 0; i < currentWeapon.numberOfProjectiles; i++)
        {
            // Desvio aleatório em graus, simétrico para os dois lados — 0 = mira perfeita.
            float spreadOffset = Random.Range(-currentWeapon.spreadAngleDegrees, currentWeapon.spreadAngleDegrees);
            Quaternion bulletRotation = aimRotation * Quaternion.Euler(0, 0, spreadOffset);

            PoolManager.Instance.Get(currentWeapon.bulletPrefab, spawnPosition, bulletRotation);
        }

        // Som do tiro: um por disparo (não por projétil), definido na própria arma.
        AudioEvents.Play(currentWeapon.shootSound);

        if (weaponVisual != null)
        {
            weaponVisual.PlayShootAnimation(currentWeapon.fireRate);
        }
    }

    // Chamado de fora (ex: WeaponPickupReward) quando o player atira numa arma voadora.
    public void SwapWeapon(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;
        if (newWeapon != null) AudioEvents.Play(newWeapon.swapSound);
        secondsSinceLastShot = 0f; // evita herdar o timing de cooldown da arma anterior
    }
}
