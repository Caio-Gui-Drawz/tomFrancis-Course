using UnityEngine;

// Cada arma vira um asset (Assets > Create > Cosmo Treasure > Weapon Data), sem precisar
// de código novo por arma — só preencher os campos no Inspector.
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Cosmo Treasure/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public GameObject bulletPrefab;
    public float fireRate; // segundos entre tiros
}
