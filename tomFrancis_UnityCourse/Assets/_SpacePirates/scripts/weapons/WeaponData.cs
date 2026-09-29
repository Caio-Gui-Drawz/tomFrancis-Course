using UnityEngine;

// Cada arma vira um asset (Assets > Create > Cosmo Treasure > Weapon Data), sem precisar
// de código novo por arma — só preencher os campos no Inspector.
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Cosmo Treasure/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public GameObject bulletPrefab;
    public float fireRate; // segundos entre tiros

    [Header("Múltiplos projéteis e precisão")]
    public int numberOfProjectiles = 1;
    [Tooltip("Desvio angular máximo (para cada lado) aplicado a cada bala, em graus. 0 = mira perfeita.")]
    public float spreadAngleDegrees = 0f;
}
