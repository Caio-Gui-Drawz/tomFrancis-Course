using System;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;

// Entrada e saída do player por script (sem animar o pai do prefab):
//   - O player fica ESCONDIDO fora da tela (à esquerda) até o jogo entrar em Playing.
//   - Ao dar Play: depois de 'Intro Delay Seconds', ele voa até a posição inicial em 'Intro Seconds'. Durante isso
//     o input fica travado e a câmera parada. Quando termina, tudo é liberado.
//   - PlayOutro() (chame por UnityEvent, ex.: no On Begin do evento All Waves Completed): trava o input, vai ao
//     centro vertical (de qualquer altura), e acelera para a direita até sair da tela. A câmera fica parada.
//   - A wave 1 só começa depois da entrada terminar (o WaveManager espera).
//
// Coloque no MESMO objeto do Player2D / PlayerMovement2D (o do Rigidbody2D).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerEntrance : MonoBehaviour
{
    public static PlayerEntrance Instance { get; private set; }

    enum Phase { Hidden, Intro, Playing, Outro, Gone }

    [Header("Entrada (ao dar Play)")]
    [Tooltip("Espera (s) depois de entrar em Playing, antes de o player começar a entrar (ex.: deixar o fade da tela terminar).")]
    [Min(0f)] public float introDelaySeconds = 0.5f;
    [Tooltip("Duração (s) da entrada: do lado de fora até a posição inicial.")]
    [Min(0.05f)] public float introSeconds = 2f;
    public Ease introEase = Ease.OutCubic;
    [Tooltip("Altura extra de onde ele começa a entrada (0 = entra reto pela esquerda; positivo = vem de cima; negativo = de baixo).")]
    public float introStartYOffset = 0f;

    [Header("Saída (PlayOutro)")]
    [Tooltip("Tempo (s) para voltar ao centro vertical antes de sair. 0 = não centraliza, sai na altura em que estiver.")]
    [Min(0f)] public float centerSeconds = 0.5f;
    public Ease centerEase = Ease.InOutSine;
    [Tooltip("Duração (s) da saída para a direita.")]
    [Min(0.05f)] public float outroSeconds = 1.5f;
    public Ease outroEase = Ease.InCubic;

    [Header("Geral")]
    [Tooltip("Ligado = a câmera fica parada durante a entrada e a saída.")]
    public bool freezeCamera = true;
    [Tooltip("Folga extra (unidades) além da borda da tela para considerar o player 'fora'.")]
    [Min(0f)] public float offscreenMargin = 1f;
    public bool log = true;

    [Header("Eventos")]
    [Tooltip("Dispara quando o player começa a entrar (depois do delay). Ex.: trigger de propulsão no Animator.")]
    public UnityEvent onIntroStart;
    public UnityEvent onIntroFinished;
    public UnityEvent onOutroStart;
    public UnityEvent onOutroFinished;

    // true enquanto o player ainda não terminou de entrar (inclui esperar o Play). O WaveManager espera isso.
    public bool IntroPending { get { return phase == Phase.Hidden || phase == Phase.Intro; } }
    public bool OutroDone { get { return phase == Phase.Gone; } }

    Phase phase = Phase.Hidden;
    Rigidbody2D body;
    PlayerMovement2D movement;
    Player2D player;
    Vector2 restPosition;
    Sequence running;

    // câmera parada
    PositionConstraint frozenConstraint;
    bool constraintWasActive;

    void Awake()
    {
        Instance = this;
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement2D>();
        player = GetComponent<Player2D>();
    }

    void Start()
    {
        restPosition = body.position;
        Hide();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            // O GameManager pode já ter entrado em Playing antes de eu começar a ouvir (reiniciou a cena jogando).
            if (GameManager.Instance.CurrentState == GameState.Playing) BeginIntro();
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        KillRunning();
        if (Instance == this) Instance = null;
    }

    void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing && phase == Phase.Hidden) BeginIntro();

        // Morreu / voltou ao menu: para qualquer movimento do script (a morte cuida da câmera e do corpo).
        if (state == GameState.GameOver || state == GameState.Menu) KillRunning();
    }

    // ------------------------------------------------------------------ entrada

    void Hide()
    {
        phase = Phase.Hidden;
        FreezeCamera();
        Lock(true);
        Place(OffscreenLeft(restPosition.y + introStartYOffset));
    }

    void BeginIntro()
    {
        if (phase != Phase.Hidden) return;
        phase = Phase.Intro;
        KillRunning();

        running = DOTween.Sequence().SetUpdate(UpdateType.Fixed, false);
        running.AppendInterval(introDelaySeconds);
        running.AppendCallback(() =>
        {
            if (log) Debug.Log("[PlayerEntrance] entrando (" + introSeconds + "s)", this);
            onIntroStart?.Invoke();
        });
        running.Append(Segment(() => OffscreenLeft(restPosition.y + introStartYOffset), () => restPosition, introSeconds, introEase));
        running.OnComplete(FinishIntro);
    }

    void FinishIntro()
    {
        running = null;
        phase = Phase.Playing;
        Place(restPosition);
        Lock(false);
        UnfreezeCamera();
        if (log) Debug.Log("[PlayerEntrance] entrada terminou", this);
        onIntroFinished?.Invoke();
    }

    // Pula a entrada: o player aparece na posição inicial e fica livre (usado pelo salto de wave de debug).
    public void SkipIntro()
    {
        if (phase != Phase.Hidden && phase != Phase.Intro) return;
        KillRunning();
        FinishIntro();
    }

    // ------------------------------------------------------------------ saída

    public void PlayOutro()
    {
        if (phase == Phase.Outro || phase == Phase.Gone) return;
        KillRunning();
        phase = Phase.Outro;

        Lock(true);
        if (player != null) player.SetCombatStance(false);
        FreezeCamera();

        if (log) Debug.Log("[PlayerEntrance] saindo", this);
        onOutroStart?.Invoke();

        running = DOTween.Sequence().SetUpdate(UpdateType.Fixed, false);

        if (centerSeconds > 0f && Mathf.Abs(body.position.y - restPosition.y) > 0.05f)
        {
            running.Append(Segment(() => body.position, () => new Vector2(body.position.x, restPosition.y), centerSeconds, centerEase));
        }
        running.Append(Segment(() => body.position, () => OffscreenRight(body.position.y), outroSeconds, outroEase));
        running.OnComplete(() =>
        {
            running = null;
            phase = Phase.Gone;
            if (log) Debug.Log("[PlayerEntrance] saída terminou", this);
            onOutroFinished?.Invoke();
        });
    }

    // ------------------------------------------------------------------ testes (clique direito no componente, em Play)

    [ContextMenu("Testar entrada (Play mode)")]
    void TestIntro()
    {
        KillRunning();
        UnfreezeCamera();
        Hide();
        BeginIntro();
    }

    [ContextMenu("Testar saída (Play mode)")]
    void TestOutro()
    {
        if (phase == Phase.Hidden || phase == Phase.Intro) SkipIntro();
        phase = Phase.Playing;
        PlayOutro();
    }

    // ------------------------------------------------------------------ movimento

    // Um trecho de movimento: os pontos de partida/chegada são lidos só quando o trecho COMEÇA.
    Tween Segment(Func<Vector2> from, Func<Vector2> to, float seconds, Ease ease)
    {
        Vector2 a = Vector2.zero, b = Vector2.zero;
        float t = 0f;
        return DOTween.To(() => t, v => { t = v; Place(Vector2.LerpUnclamped(a, b, t)); }, 1f, Mathf.Max(0.0001f, seconds))
            .SetEase(ease)
            .OnStart(() => { a = from(); b = to(); });
    }

    void Place(Vector2 p)
    {
        body.position = p;
        body.linearVelocity = Vector2.zero;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    void Lock(bool locked)
    {
        if (movement != null) movement.ExternalControl = locked;
        if (player != null) player.InputLocked = locked;
    }

    void KillRunning()
    {
        if (running != null && running.IsActive()) running.Kill();
        running = null;
    }

    // ------------------------------------------------------------------ fora da tela

    Vector2 OffscreenLeft(float y)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2(body.position.x - 30f, y);
        float left = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        float rightExtent = ExtentRight();
        return new Vector2(left - rightExtent - offscreenMargin, y);
    }

    Vector2 OffscreenRight(float y)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2(body.position.x + 30f, y);
        float right = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        float leftExtent = ExtentLeft();
        return new Vector2(right + leftExtent + offscreenMargin, y);
    }

    // Quanto o visual do player se estende à direita / à esquerda do ponto do corpo.
    float ExtentRight()
    {
        Bounds b;
        return TryGetVisualBounds(out b) ? Mathf.Max(0f, b.max.x - body.position.x) : 0f;
    }

    float ExtentLeft()
    {
        Bounds b;
        return TryGetVisualBounds(out b) ? Mathf.Max(0f, body.position.x - b.min.x) : 0f;
    }

    bool TryGetVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return has;
    }

    // ------------------------------------------------------------------ câmera

    // Igual à morte: o alvo da câmera costuma ter um PositionConstraint seguindo o player.
    void FreezeCamera()
    {
        if (!freezeCamera || frozenConstraint != null) return;

        CinemachineCamera cm = FindFirstObjectByType<CinemachineCamera>();
        if (cm == null || cm.Follow == null) return;

        PositionConstraint constraint = cm.Follow.GetComponent<PositionConstraint>();
        if (constraint == null || cm.Follow == transform) return;

        frozenConstraint = constraint;
        constraintWasActive = constraint.constraintActive;
        constraint.constraintActive = false;
    }

    void UnfreezeCamera()
    {
        if (frozenConstraint == null) return;
        frozenConstraint.constraintActive = constraintWasActive;
        frozenConstraint = null;
    }
}
