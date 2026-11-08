using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Atalhos de DEBUG modulares. Cada comando é uma linha na lista: tecla + o que fazer. Dá para trocar a tecla,
// apagar, duplicar e criar novos no Inspector. Para algo que não existe aqui, use a ação "Custom" e ligue
// qualquer método público (de qualquer objeto) no evento "On Pressed", sem escrever código.
//   F1 abre/fecha um painel na tela com a lista de comandos (clicáveis) e o campo "Ir para a wave".
// Só funciona no Editor e em Development Build (a não ser que 'Active In Release Build' esteja ligado).
// Remova (ou desative o objeto) antes de entregar a demo.
public class DebugCommands : MonoBehaviour
{
    public enum CommandAction
    {
        ToggleImmortal,   // liga/desliga o modo imortal do player
        SpawnPrefab,      // faz nascer um dos prefabs da lista pelo Spawner dele na cena (caixa, inimigo, medkit...)
        NextWave,         // pula para a próxima wave
        PreviousWave,     // volta uma wave
        GoToWave,         // vai para a wave "Wave Number"
        Custom,           // executa o evento "On Pressed"
        RestartScene,     // reinicia a cena já jogando (o antigo DebugReset)
        HealPlayer,       // cura o player em "Amount" (o antigo DebugHealthKeys)
        DamagePlayer      // dá dano no player em "Amount"
    }

    [System.Serializable]
    public class Command
    {
        public string label = "Comando";
        public KeyCode key = KeyCode.None;
        public CommandAction action = CommandAction.Custom;

        [Tooltip("SpawnPrefab: prefabs que podem nascer (com mais de um, sorteia um). Cada um precisa ter um Spawner na cena.")]
        public GameObject[] prefabs;

        [Tooltip("GoToWave: número da wave de destino.")]
        [Min(1)] public int waveNumber = 1;

        [Tooltip("HealPlayer / DamagePlayer: quanto de vida.")]
        [Min(0f)] public float amount = 1f;

        [Tooltip("Custom: o que acontece ao apertar a tecla. Ligue qualquer método público aqui.")]
        public UnityEvent onPressed;

        public Command() { }

        public Command(string label, KeyCode key, CommandAction action, float amount = 1f)
        {
            this.label = label;
            this.key = key;
            this.action = action;
            this.amount = amount;
        }
    }

    [Tooltip("Procura sozinho na cena se estiver vazio.")]
    public WaveManager waveManager;

    public KeyCode toggleOverlayKey = KeyCode.F1;
    public bool showOverlayOnStart = false;

    [Tooltip("Ligado = os comandos também funcionam numa build final (não de desenvolvimento). Deixe DESLIGADO para a demo.")]
    public bool activeInReleaseBuild = false;

    [Tooltip("Dica: ARRASTE os prefabs nos comandos de nascer (caixa, inimigo, medkit). Menu do componente (⋮) > 'Adicionar comandos padrão que faltam' completa a lista sem apagar o que você já configurou.")]
    public List<Command> commands = DefaultCommands();

    static List<Command> DefaultCommands()
    {
        return new List<Command>
        {
            new Command("Reiniciar cena", KeyCode.R, CommandAction.RestartScene),
            new Command("Curar player", KeyCode.H, CommandAction.HealPlayer, 2f),
            new Command("Dano no player", KeyCode.J, CommandAction.DamagePlayer, 1f),
            new Command("Imortal", KeyCode.I, CommandAction.ToggleImmortal),
            new Command("Spawnar caixa", KeyCode.B, CommandAction.SpawnPrefab),
            new Command("Spawnar inimigo", KeyCode.E, CommandAction.SpawnPrefab),
            new Command("Spawnar medkit", KeyCode.M, CommandAction.SpawnPrefab),
            new Command("Próxima wave", KeyCode.N, CommandAction.NextWave),
        };
    }

    // Completa a lista com os comandos padrão cuja AÇÃO ainda não existe (não mexe nos que você já tem).
    [ContextMenu("Adicionar comandos padrão que faltam")]
    void AddMissingDefaults()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Adicionar comandos de debug");
#endif
        foreach (Command d in DefaultCommands())
        {
            bool exists = false;
            foreach (Command c in commands)
            {
                if (c != null && c.action == d.action && d.action != CommandAction.SpawnPrefab) { exists = true; break; }
                if (c != null && c.action == d.action && c.label == d.label) { exists = true; break; }
            }
            if (!exists) commands.Add(d);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Estático: sobrevive ao reiniciar a cena com R (continua imortal).
    static bool immortal;

    bool overlayOpen;
    string waveInput = "1";
    healthSystem playerHealth;
    string lastMessage = "";
    float lastMessageTime = -10f;

    bool Allowed { get { return Application.isEditor || Debug.isDebugBuild || activeInReleaseBuild; } }

    void Start()
    {
        overlayOpen = showOverlayOnStart;
        if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
    }

    void Update()
    {
        if (!Allowed) return;

        ApplyImmortal();

        if (Input.GetKeyDown(toggleOverlayKey)) overlayOpen = !overlayOpen;

        foreach (Command c in commands)
        {
            if (c != null && c.key != KeyCode.None && Input.GetKeyDown(c.key)) Execute(c);
        }
    }

    // O player pode nascer de novo (reiniciar cena): reaplica a flag sempre que o healthSystem muda.
    void ApplyImmortal()
    {
        if (playerHealth == null && references.thePlayer != null) playerHealth = references.thePlayer.GetComponent<healthSystem>();
        if (playerHealth != null && playerHealth.invulnerable != immortal) playerHealth.invulnerable = immortal;
    }

    // ---------------- ações (também podem ser ligadas no evento de um comando Custom) ----------------

    public void Execute(Command c)
    {
        switch (c.action)
        {
            case CommandAction.ToggleImmortal: ToggleImmortal(); break;
            case CommandAction.SpawnPrefab: SpawnFrom(c.prefabs, c.label); break;
            case CommandAction.NextWave: GoToWave(CurrentWave() + 1); break;
            case CommandAction.PreviousWave: GoToWave(CurrentWave() - 1); break;
            case CommandAction.GoToWave: GoToWave(c.waveNumber); break;
            case CommandAction.Custom: if (c.onPressed != null) c.onPressed.Invoke(); break;
            case CommandAction.RestartScene: RestartScene(); break;
            case CommandAction.HealPlayer: HealPlayer(c.amount); break;
            case CommandAction.DamagePlayer: DamagePlayer(c.amount); break;
        }
    }

    public void RestartScene()
    {
        GameManager.startPlayingOnLoad = true; // reinicia já jogando, sem passar pelo menu
        Time.timeScale = 1f; // por segurança, caso o reset aconteça com o jogo pausado
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void HealPlayer(float amount)
    {
        healthSystem health = PlayerHealth();
        if (health != null) health.Heal(amount);
    }

    public void DamagePlayer(float amount)
    {
        healthSystem health = PlayerHealth();
        if (health != null) health.TakeDamage(amount);
    }

    healthSystem PlayerHealth()
    {
        if (references.thePlayer == null) return null;
        return references.thePlayer.GetComponent<healthSystem>();
    }

    public void ToggleImmortal()
    {
        immortal = !immortal;
        ApplyImmortal();
        Say("Imortal " + (immortal ? "LIGADO" : "desligado"));
    }

    public void GoToWave(int wave)
    {
        if (!CanPlay()) return;
        waveManager.DebugGoToWave(wave);
        Say("Wave " + waveManager.CurrentWave);
    }

    void SpawnFrom(GameObject[] prefabs, string label)
    {
        if (!CanPlay()) return;

        List<GameObject> valid = new List<GameObject>();
        if (prefabs != null)
        {
            foreach (GameObject p in prefabs) if (p != null) valid.Add(p);
        }
        if (valid.Count == 0)
        {
            Say("'" + label + "': arraste um prefab na lista do comando");
            return;
        }

        GameObject prefab = valid[Random.Range(0, valid.Count)];
        GameObject spawned = waveManager.DebugSpawn(prefab);
        Say(spawned != null ? "Nasceu: " + prefab.name : "Sem Spawner na cena para '" + prefab.name + "'");
    }

    int CurrentWave()
    {
        return waveManager != null ? waveManager.CurrentWave : 1;
    }

    bool CanPlay()
    {
        if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager == null) { Say("Não há WaveManager na cena"); return false; }
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
        {
            Say("Só funciona durante o jogo (Playing)");
            return false;
        }
        return true;
    }

    void Say(string message)
    {
        lastMessage = message;
        lastMessageTime = Time.unscaledTime;
        Debug.Log("[Debug] " + message);
    }

    // ---------------- painel ----------------

    void OnGUI()
    {
        if (!Allowed) return;

        // Mensagem curta (aparece mesmo com o painel fechado).
        if (Time.unscaledTime - lastMessageTime < 2.5f)
        {
            GUI.Label(new Rect(10f, Screen.height - 30f, 600f, 24f), "[Debug] " + lastMessage);
        }

        if (!overlayOpen) return;

        GUILayout.BeginArea(new Rect(10f, 10f, 260f, Screen.height - 20f));
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("DEBUG  (" + toggleOverlayKey + " fecha)   wave " + CurrentWave() + (immortal ? "   IMORTAL" : ""));

        foreach (Command c in commands)
        {
            if (c == null) continue;
            string keyText = c.key == KeyCode.None ? "" : "[" + c.key + "] ";
            if (GUILayout.Button(keyText + c.label)) Execute(c);
        }

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ir para a wave", GUILayout.Width(95f));
        waveInput = GUILayout.TextField(waveInput, 3, GUILayout.Width(40f));
        int wave;
        if (GUILayout.Button("Ir") && int.TryParse(waveInput, out wave)) GoToWave(wave);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
