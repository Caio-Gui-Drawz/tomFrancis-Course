/*using UnityEngine;

// Colocar nos prefabs de ARMA voadora (variação do inimigo/tesouro), junto com o healthSystem.
public class WeaponPickupReward : MonoBehaviour
{
    public WeaponData weaponToGive;

    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        if (references.thePlayer == null) return;

        Player2D player = references.thePlayer.GetComponent<Player2D>();
        if (player != null)
        {
            player.SwapWeapon(weaponToGive);
        }
    }
}
*/