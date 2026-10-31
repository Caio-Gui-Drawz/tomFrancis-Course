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

    [Tooltip("Se ligado, a caixa também não sorteia a última arma ESPECIAL que uma caixa deu, mesmo que ela já tenha acabado e o player tenha voltado pra pistola.")]
    public bool avoidLastBoxWeapon = true;

    void Start()
    {
        GetComponent<healthSystem>().OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        if (references.thePlayer == null) return;

        Player2D player = references.thePlayer.GetComponent<Player2D>();
        if (player == null) return;

        WeaponData chosen = PickRandomWeapon(player.currentWeapon, player.LastBoxWeapon);
        if (chosen != null)
        {
            player.LastBoxWeapon = chosen;
            player.SwapWeapon(chosen);
        }
    }

    // Tenta respeitar as exclusões na ordem de importância; se sobrar zero opções, vai afrouxando:
    //   1) sem a atual E sem a última da caixa  ->  2) sem a atual  ->  3) a lista inteira.
    WeaponData PickRandomWeapon(WeaponData current, WeaponData lastFromBox)
    {
        if (possibleWeapons == null || possibleWeapons.Length == 0) return null;

        List<WeaponData> candidates = Collect(avoidCurrentWeapon ? current : null, avoidLastBoxWeapon ? lastFromBox : null);
        if (candidates.Count == 0) candidates = Collect(avoidCurrentWeapon ? current : null, null);
        if (candidates.Count == 0) candidates = Collect(null, null);
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    List<WeaponData> Collect(WeaponData excludeA, WeaponData excludeB)
    {
        List<WeaponData> result = new List<WeaponData>();
        foreach (WeaponData weapon in possibleWeapons)
        {
            if (weapon == null) continue;
            if (excludeA != null && weapon == excludeA) continue;
            if (excludeB != null && weapon == excludeB) continue;
            result.Add(weapon);
        }
        return result;
    }
}
