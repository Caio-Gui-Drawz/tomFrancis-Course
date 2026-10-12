using System.Collections;
using UnityEngine;

// Colocar no mesmo objeto que tem o healthSystem do player.
// Ao morrer: desliga o combate, toca a animação de morte (via PlayerBodyAnimations, no filho do
// Spine) e só DEPOIS dela terminar (+ uma pequena pausa) manda o jogo pro Game Over.
public class PlayerDeath : MonoBehaviour
{
    [Tooltip("Tempo (segundos) parado na pose final da morte antes de aparecer o Game Over.")]
    public float holdAfterDeathAnimation = 0.6f;

    healthSystem health;
    Player2D player;
    PlayerBodyAnimations bodyAnimations;

    void Start()
    {
        health = GetComponent<healthSystem>();
        player = GetComponent<Player2D>();
        bodyAnimations = GetComponentInChildren<PlayerBodyAnimations>();

        // O player não pode ser removido/destruído ao morrer, senão a animação nunca aparece.
        health.removeOnDeath = false;
        health.soundProfile = SoundProfile.Player; // sons de dano/morte do player (via AudioEvents)
        health.OnDeath += HandleDeath;
    }

    void HandleDeath()
    {
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        if (player != null)
        {
            player.SetCombatStance(false); // solta a mira/braço, o corpo todo vira a animação de morte
            player.enabled = false;        // para de atirar durante a morte
        }

        float wait = 0f;
        if (bodyAnimations != null)
        {
            wait = bodyAnimations.PlayDeath() + holdAfterDeathAnimation;
        }

        yield return new WaitForSecondsRealtime(wait);

        GameManager.Instance.SetState(GameState.GameOver);
    }
}
