using UnityEngine;
using FMODUnity;
using Spine.Unity;

// Cada arma vira um asset (Assets > Create > Cosmo Treasure > Weapon Data), sem precisar
// de código novo por arma — só preencher os campos no Inspector.
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Cosmo Treasure/Weapon Data")]
public class WeaponData : ScriptableObject
{
    // Como a arma "acaba" quando é pega de uma caixa. A arma padrão (pistola) usa Unlimited.
    public enum LimitMode { Unlimited, Time, Ammo }

    public string weaponName;
    public GameObject bulletPrefab;
    public float fireRate; // segundos entre tiros

    [Header("Múltiplos projéteis e precisão")]
    public int numberOfProjectiles = 1;
    [Tooltip("Desvio angular máximo (para cada lado) aplicado a cada bala, em graus. 0 = mira perfeita.")]
    public float spreadAngleDegrees = 0f;

    [Header("Limite de uso (armas de caixa)")]
    [Tooltip("Unlimited = nunca acaba (pistola padrão). Time = acaba depois de N segundos. Ammo = acaba depois de N tiros.")]
    public LimitMode limitMode = LimitMode.Unlimited;

    [Tooltip("Segundos que a arma dura na mão (só usado em Time).")]
    public float durationSeconds = 10f;

    [Tooltip("Quantidade de tiros (disparos, não projéteis) que a arma tem (só usado em Ammo).")]
    public int ammo = 30;

    [Header("Visual (Spine)")]
    [Tooltip("O esqueleto do player (SpacePirate_Spine_SkeletonData). Só serve pra preencher a lista de animações abaixo.")]
    public SkeletonDataAsset skeletonData;

    [SpineAnimation(dataField: "skeletonData")]
    [Tooltip("Animação 'WeaponOnly_...' que mostra esta arma na mão do player. <None> = não troca o visual.")]
    public string weaponAnimationName;

    [Header("Áudio")]
    [Tooltip("Evento do FMOD tocado UMA vez a cada disparo (mesmo que a arma atire vários projéteis).")]
    public EventReference shootSound;

    [Tooltip("Evento do FMOD tocado quando o player troca PARA esta arma.")]
    public EventReference swapSound;
}
