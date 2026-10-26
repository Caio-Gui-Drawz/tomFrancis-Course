using System.Collections.Generic;
using UnityEngine;

// Colocar nos prefabs de CAIXA DE ARMA voadora, junto com o healthSystem (o HP da caixa é o
// "Max Health" dele). Quando a caixa é destruída, troca a arma do player por uma sorteada da lista.
public class WeaponPickupReward : MonoBehaviour
{
    [Tooltip("Armas que esta caixa pode dar. Arraste os assets de WeaponData. Pra uma caixa que sempre dá a mesma arma, deixe só uma na lista.")]
    public WeaponData[] possibleWeapons;

    [Tooltip("Se ligado, a caixa não sorteia a arma que o player já está usando (a não ser que seja a única opção).")]
    public bool avoidCurrentWeapon = true;

    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        if (references.thePlayer == null) return;

        Player2D player = references.thePlayer.GetComponent<Player2D>();
        if (player == null) return;

        WeaponData chosen = PickRandomWeapon(player.currentWeapon);
        if (chosen != null)
        {
            player.SwapWeapon(chosen);
        }
    }

    WeaponData PickRandomWeapon(WeaponData current)
    {
        if (possibleWeapons == null || possibleWeapons.Length == 0) return null;

        List<WeaponData> candidates = new List<WeaponData>();
        foreach (WeaponData weapon in possibleWeapons)
        {
            if (weapon == null) continue;
            if (avoidCurrentWeapon && weapon == current) continue;
            candidates.Add(weapon);
        }

        // Só tinha a arma atual na lista (ou nenhuma válida): usa a lista inteira.
        if (candidates.Count == 0)
        {
            foreach (WeaponData weapon in possibleWeapons)
            {
                if (weapon != null) candidates.Add(weapon);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }
}
