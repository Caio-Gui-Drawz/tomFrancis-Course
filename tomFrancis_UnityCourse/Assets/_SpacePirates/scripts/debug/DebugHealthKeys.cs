using UnityEngine;

// Atalhos de DEBUG pra testar a barra de vida: H cura, J dá dano no player.
// Remova (ou desative o objeto) antes de entregar a demo.
public class DebugHealthKeys : MonoBehaviour
{
    public KeyCode healKey = KeyCode.H;
    public KeyCode damageKey = KeyCode.J;
    public float healAmount = 2f;
    public float damageAmount = 1f;

    void Update()
    {
        if (references.thePlayer == null) return;

        healthSystem health = references.thePlayer.GetComponent<healthSystem>();
        if (health == null) return;

        if (Input.GetKeyDown(healKey)) health.Heal(healAmount);
        if (Input.GetKeyDown(damageKey)) health.TakeDamage(damageAmount);
    }
}
