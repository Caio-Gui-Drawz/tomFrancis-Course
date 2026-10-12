using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

// Qual "conjunto de sons" uma entidade com healthSystem usa quando leva dano / morre.
public enum SoundProfile { None, Player, Enemy, Treasure }

// Central de referências dos eventos do FMOD que NÃO tocam direto de um objeto.
// Coloque num GameObject da cena (ex.: "AudioEvents") e escolha cada evento no Inspector.
//
// Ficam FORA daqui (tocam direto nos objetos, via StudioEventEmitter): enemyIdle, treasureIdle,
// hovering (moto). Sons de tiro e de troca de arma ficam no WeaponData de cada arma.
public class AudioEvents : MonoBehaviour
{
    public static AudioEvents Instance { get; private set; }

    [Header("Player")]
    public EventReference playerTakeDamage;
    public EventReference playerDeath;

    [Header("Inimigos")]
    public EventReference enemyTakeDamage;
    public EventReference enemyDeath;

    [Header("Tesouros")]
    public EventReference treasureTakeDamage;
    public EventReference treasureCollected;

    [Header("Jogo")]
    public EventReference waveStarted;

    [Header("Ambiente (loop, começa junto com a cena)")]
    public EventReference ambience;

    [Header("Interface")]
    public EventReference buttonClick;
    public EventReference buttonHover;
    [Tooltip("Som da tela de morte (só da interface). Diferente do playerDeath, que toca quando o player morre.")]
    public EventReference deathScreen;

    FMOD.Studio.EventInstance ambienceInstance;
    bool ambienceStarted;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (!ambience.IsNull)
        {
            ambienceInstance = RuntimeManager.CreateInstance(ambience);
            ambienceInstance.start();
            ambienceStarted = true;
        }

        // Dá som de hover/clique pra TODO botão da cena (inclusive os de painéis desligados,
        // como pausa e game over), sem precisar colocar componente em cada um à mão.
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.GetComponent<ButtonSounds>() == null)
            {
                button.gameObject.AddComponent<ButtonSounds>();
            }
        }
    }

    void OnDestroy()
    {
        if (ambienceStarted)
        {
            ambienceInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            ambienceInstance.release();
        }
        if (Instance == this) Instance = null;
    }

    // Toca um evento "tiro único" (uma instância nova a cada chamada, então sons rápidos e
    // repetidos se sobrepõem em vez de se cortarem). Ignora referências vazias em silêncio,
    // pra dar pra deixar eventos sem preencher enquanto o áudio ainda está sendo feito.
    public static void Play(EventReference eventReference)
    {
        if (eventReference.IsNull) return;
        RuntimeManager.PlayOneShot(eventReference);
    }

    // Som de dano ou de morte, conforme o "perfil" da entidade (player, inimigo, tesouro).
    public static void PlayDamage(SoundProfile profile, bool lethal)
    {
        if (Instance == null) return;

        switch (profile)
        {
            case SoundProfile.Player:
                Play(lethal ? Instance.playerDeath : Instance.playerTakeDamage);
                break;
            case SoundProfile.Enemy:
                Play(lethal ? Instance.enemyDeath : Instance.enemyTakeDamage);
                break;
            case SoundProfile.Treasure:
                Play(lethal ? Instance.treasureCollected : Instance.treasureTakeDamage);
                break;
        }
    }

    public static void PlayWaveStarted() { if (Instance != null) Play(Instance.waveStarted); }
    public static void PlayDeathScreen() { if (Instance != null) Play(Instance.deathScreen); }
    public static void PlayButtonClick() { if (Instance != null) Play(Instance.buttonClick); }
    public static void PlayButtonHover() { if (Instance != null) Play(Instance.buttonHover); }
}
