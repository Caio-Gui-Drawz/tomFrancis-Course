using UnityEngine;

public class Player2D : MonoBehaviour
{
    [Tooltip("Arma padrão (pistola). Volta pra ela quando a arma de caixa acaba. Se vazio, usa a arma inicial.")]
    public WeaponData defaultWeapon;
    public WeaponData currentWeapon;
    public PlayerWeaponVisual weaponVisual; // opcional — se vazio, usa o spawn antigo
    [Tooltip("Toca o muzzle flash da arma a cada disparo. Se vazio, procura um MuzzleFlashPlayer nos filhos do player.")]
    public MuzzleFlashPlayer muzzleFlash;
    public WaveManager waveManager; // arraste o GameObject do WaveManager aqui

    AimArmToMouse aimArmToMouse; // pego automaticamente do mesmo objeto do weaponVisual

    float secondsSinceLastShot;

    [Tooltip("Armas de um tiro por clique: um clique dado até este tempo ANTES da arma estar pronta (cadência ou recarga) ainda conta e sai assim que ela liberar. 0 = o clique só vale se a arma já estiver pronta.")]
    [Min(0f)] public float clickBufferSeconds = 0.12f;
    float lastClickTime = -999f;

    // Ângulo (graus) na direção do mouse, recalculado todo frame — usado só para mirar o
    // tiro. NÃO aplicamos isso em transform.rotation porque isso girava o player inteiro
    // e, com ele, todos os filhos do prefab (incluindo o Spine), causando rotação esquisita.
    float currentAimAngle;

    [Tooltip("Ligado = não dá pra atirar enquanto a animação de recarga (troca de arma) está tocando.")]
    public bool blockShootingDuringReload = true;

    // Última arma ESPECIAL que uma caixa deu (mesmo que já tenha acabado e voltado pra pistola).
    // Usada pelas caixas pra não repetir a mesma arma duas vezes seguidas.
    public WeaponData LastBoxWeapon { get; set; }

    float reloadUnlockTime; // Time.time em que o tiro volta a ser liberado

    float weaponTimeRemaining;
    int weaponAmmoRemaining;

    // Pra HUD futura: 1 = cheia / sem limite, 0 = acabando.
    public float WeaponRemainingFraction
    {
        get
        {
            if (currentWeapon == null) return 1f;
            switch (currentWeapon.limitMode)
            {
                case WeaponData.LimitMode.Time:
                    return currentWeapon.durationSeconds > 0f ? Mathf.Clamp01(weaponTimeRemaining / currentWeapon.durationSeconds) : 0f;
                case WeaponData.LimitMode.Ammo:
                    return currentWeapon.ammo > 0 ? Mathf.Clamp01((float)weaponAmmoRemaining / currentWeapon.ammo) : 0f;
                default:
                    return 1f;
            }
        }
    }

    // Avisa quando a arma muda (pegou de caixa ou voltou pra padrão).
    public event System.Action<WeaponData> OnWeaponChanged;

    bool canAct;
    bool inCombatStance; // só pode atirar depois que a wave 1 começa (antes disso: só voando)

    void Start()
    {
        references.thePlayer = gameObject;

        if (defaultWeapon == null) defaultWeapon = currentWeapon;
        ResetWeaponLimit();

        if (muzzleFlash == null) muzzleFlash = GetComponentInChildren<MuzzleFlashPlayer>(true);

        if (weaponVisual != null)
        {
            aimArmToMouse = weaponVisual.GetComponent<AimArmToMouse>();
        }

        // Garante que começa com as mãos abaixadas (sem mirar), até a wave 1 começar.
        SetCombatStance(false);

        ShowCurrentWeapon();
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
        if (TouchInput.UseAimAngle)
        {
            currentAimAngle = TouchInput.AimAngleDegrees; // toque: mira por ângulo
        }
        else
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(TouchInput.PointerPosition);
            mousePosition.z = 0;
            Vector2 direction = mousePosition - transform.position;
            currentAimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        // Arma de caixa com limite de tempo: o relógio corre enquanto o jogo está rodando.
        if (currentWeapon != null && currentWeapon.limitMode == WeaponData.LimitMode.Time)
        {
            weaponTimeRemaining -= Time.deltaTime;
            if (weaponTimeRemaining <= 0f) RevertToDefaultWeapon();
        }

        if (TouchInput.FireDown) lastClickTime = Time.time;

        // Atirar (usando a arma atual)
        if (currentWeapon != null && inCombatStance)
        {
            secondsSinceLastShot += Time.deltaTime;
            bool reloading = blockShootingDuringReload && Time.time < reloadUnlockTime;
            // Automática: segurar atira. Semi-automática: 1 tiro por clique (com um pequeno buffer do clique).
            bool wantsToFire = currentWeapon.automaticFire
                ? TouchInput.FireHeld
                : Time.time - lastClickTime <= clickBufferSeconds;

            if (!reloading && secondsSinceLastShot >= currentWeapon.fireRate && wantsToFire)
            {
                lastClickTime = -999f; // consome o clique
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

            // Cada projétil pode nascer um pouco deslocado do cano (jitter) e com velocidade levemente diferente.
            Vector3 pelletPosition = spawnPosition;
            if (currentWeapon.pelletPositionJitter > 0f)
            {
                pelletPosition += (Vector3)(Random.insideUnitCircle * currentWeapon.pelletPositionJitter);
            }

            GameObject pellet = PoolManager.Instance.Get(currentWeapon.bulletPrefab, pelletPosition, bulletRotation);

            if (currentWeapon.pelletSpeedVariation > 0f && pellet != null)
            {
                Rigidbody2D pelletBody = pellet.GetComponent<Rigidbody2D>();
                if (pelletBody != null)
                {
                    pelletBody.linearVelocity *= 1f + Random.Range(-currentWeapon.pelletSpeedVariation, currentWeapon.pelletSpeedVariation);
                }
            }
        }

        // Som do tiro: um por disparo (não por projétil), definido na própria arma.
        AudioEvents.Play(currentWeapon.shootSound);

        // Muzzle flash: um por disparo (não por projétil), escolhido no WeaponData.
        if (muzzleFlash != null) muzzleFlash.Play(currentWeapon.muzzleFlash, aimAngle);

        if (weaponVisual != null)
        {
            weaponVisual.PlayShootAnimation(currentWeapon.fireRate);
        }

        // Arma de caixa com munição: gasta 1 por disparo; ao zerar, volta pra padrão.
        if (currentWeapon.limitMode == WeaponData.LimitMode.Ammo)
        {
            weaponAmmoRemaining--;
            if (weaponAmmoRemaining <= 0) RevertToDefaultWeapon();
        }
    }

    // Mostra na mão do player a arma atual (animação WeaponOnly_* definida no WeaponData).
    void ShowCurrentWeapon()
    {
        if (weaponVisual != null && currentWeapon != null)
        {
            weaponVisual.SetWeaponAnimation(currentWeapon.weaponAnimationName);
        }
    }

    void ResetWeaponLimit()
    {
        if (currentWeapon == null) return;
        weaponTimeRemaining = currentWeapon.durationSeconds;
        weaponAmmoRemaining = currentWeapon.ammo;
    }

    void RevertToDefaultWeapon()
    {
        if (defaultWeapon == null || currentWeapon == defaultWeapon) return;
        SwapWeapon(defaultWeapon);
    }

    // Chamado de fora (ex: WeaponPickupReward) quando o player atira numa arma voadora.
    public void SwapWeapon(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;
        if (newWeapon != null) AudioEvents.Play(newWeapon.swapSound);
        secondsSinceLastShot = 0f; // evita herdar o timing de cooldown da arma anterior
        ShowCurrentWeapon();
        if (inCombatStance && weaponVisual != null)
        {
            // Recarga só com o braço em posição de combate; trava o tiro pelo tempo dela.
            weaponVisual.PlayReload();
            reloadUnlockTime = Time.time + weaponVisual.ReloadDuration;
        }
        ResetWeaponLimit(); // pegar outra caixa (ou a mesma arma) reinicia o tempo/munição
        OnWeaponChanged?.Invoke(currentWeapon);
    }
}
