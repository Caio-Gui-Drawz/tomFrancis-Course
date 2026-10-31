using System.Collections;
using UnityEngine;

// CENA DE GYM: pula o menu e liga o modo de combate logo de cara (sem WaveManager não existe "wave 1"
// pra fazer isso). Coloque num objeto qualquer da cena e arraste o player.
public class GymSceneSetup : MonoBehaviour
{
    public Player2D player;

    IEnumerator Start()
    {
        yield return null; // espera os outros Start() (GameManager, Player2D) rodarem primeiro

        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.Playing);
        if (player != null) player.SetCombatStance(true);
    }
}
