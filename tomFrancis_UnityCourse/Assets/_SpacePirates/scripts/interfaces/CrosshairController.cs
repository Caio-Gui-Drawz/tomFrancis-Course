using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Crosshair no HUD. Coloque no objeto "raiz" do crosshair, dentro de um Canvas (Screen Space - Overlay):
//
//   Canvas
//   └─ Crosshair            <- este script (segue o mouse / o dedo que atira)
//      ├─ Visual            <- Image do crosshair (é este que recebe o pulso de escala e a troca de sprite/cor)
//      └─ AmmoArc           <- (opcional) AmmoBarHUD em modo Arco; não escala com o pulso
//
// O visual vem do CrosshairProfile da arma atual (WeaponData > Crosshair). Momentos: Normal, Recarregando, Acertou e
// Munição baixa. No toque, só aparece enquanto há um dedo atirando.
[RequireComponent(typeof(RectTransform))]
public class CrosshairController : MonoBehaviour
{
    public static CrosshairController Instance { get; private set; }

    // Chamado pela bala quando acerta um inimigo (seguro chamar sem crosshair na cena).
    public static void NotifyHit()
    {
        if (Instance != null) Instance.RegisterHit();
    }

    [Tooltip("Vazio = procura o Player2D na cena sozinho.")]
    public Player2D player;

    [Tooltip("A Image do crosshair (de preferência um FILHO deste objeto).")]
    public Image image;

    [Tooltip("Usado quando a arma não tem perfil de crosshair. Vazio = sem crosshair para essas armas.")]
    public CrosshairProfile defaultProfile;

    [Tooltip("Opcional: o objeto do arco de munição (AmmoBarHUD). É ligado/desligado conforme a arma e o 'Show Ammo Arc' do perfil.")]
    public GameObject ammoArc;

    [Tooltip("Esconde o cursor do sistema enquanto o crosshair está visível (desktop).")]
    public bool hideSystemCursor = true;

    [Tooltip("Só mostra o crosshair depois que a wave 1 começa (quando o player realmente pode atirar).")]
    public bool onlyInCombatStance = true;

    [Tooltip("Velocidade do fade ao aparecer/sumir.")]
    [Min(1f)] public float fadeSpeed = 20f;

    enum Moment { Normal, Reload, Hit, LowAmmo }

    RectTransform rt;
    RectTransform canvasRect;
    Canvas rootCanvas;
    CanvasGroup group;

    Player2D bound;
    CrosshairProfile profile;
    bool stateSubscribed;
    bool playing;
    Moment current = (Moment)(-1);
    float reloadUntil;
    float hitUntil;
    bool cursorHidden;

    void Awake()
    {
        Instance = this;
        rt = (RectTransform)transform;

        Canvas c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        if (image == null) image = GetComponentInChildren<Image>(true);
        if (image != null) image.raycastTarget = false;
    }

    void OnEnable()
    {
        Instance = this;
        TrySubscribeState();
        TryBindPlayer();
    }

    void OnDisable()
    {
        if (stateSubscribed && GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        stateSubscribed = false;
        Unbind();
        SetCursorHidden(false);
        if (Instance == this) Instance = null;
    }

    void OnDestroy()
    {
        if (image != null) image.rectTransform.DOKill();
        if (image != null) image.DOKill();
    }

    void TrySubscribeState()
    {
        if (stateSubscribed || GameManager.Instance == null) return;
        GameManager.Instance.OnStateChanged += HandleStateChanged;
        stateSubscribed = true;
        playing = GameManager.Instance.CurrentState == GameState.Playing;
    }

    void HandleStateChanged(GameState state)
    {
        playing = state == GameState.Playing;
    }

    void TryBindPlayer()
    {
        if (player == null) player = FindFirstObjectByType<Player2D>();
        if (player == null || bound == player) return;

        Unbind();
        bound = player;
        bound.OnWeaponChanged += HandleWeaponChanged;
        bound.OnFired += HandleFired;
        bound.OnReloadStarted += HandleReloadStarted;
        ApplyProfile(bound.currentWeapon);
    }

    void Unbind()
    {
        if (bound != null)
        {
            bound.OnWeaponChanged -= HandleWeaponChanged;
            bound.OnFired -= HandleFired;
            bound.OnReloadStarted -= HandleReloadStarted;
        }
        bound = null;
    }

    void Update()
    {
        TrySubscribeState();
        if (bound == null) TryBindPlayer();

        bool wantVisible = playing && profile != null && image != null;
        if (wantVisible && onlyInCombatStance && bound != null && !bound.InCombatStance) wantVisible = false;
        if (wantVisible && TouchInput.TouchMode && !TouchInput.FireHeld) wantVisible = false; // toque: só com um dedo atirando

        group.alpha = Mathf.MoveTowards(group.alpha, wantVisible ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        SetCursorHidden(hideSystemCursor && wantVisible && !TouchInput.TouchMode);

        FollowPointer();

        if (profile != null && image != null)
        {
            Moment now = Resolve();
            if (now != current) ApplyLook(now, false);
        }
    }

    void FollowPointer()
    {
        if (canvasRect == null) return;

        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector3 world;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, TouchInput.PointerPosition, cam, out world))
        {
            rt.position = world;
        }
    }

    Moment Resolve()
    {
        float now = Time.time;
        if (now < reloadUntil) return Moment.Reload;
        if (now < hitUntil) return Moment.Hit;

        if (bound != null && bound.HasLimitedAmmo && bound.AmmoMax > 0 && bound.AmmoRemaining > 0)
        {
            int threshold = Mathf.Max(1, Mathf.CeilToInt(bound.AmmoMax * profile.lowAmmoFraction));
            if (bound.AmmoRemaining <= threshold) return Moment.LowAmmo;
        }
        return Moment.Normal;
    }

    void ApplyProfile(WeaponData weapon)
    {
        profile = weapon != null && weapon.crosshair != null ? weapon.crosshair : defaultProfile;

        if (image != null)
        {
            image.rectTransform.DOKill();
            image.rectTransform.localScale = Vector3.one;
            image.preserveAspect = true;
        }

        if (ammoArc != null) ammoArc.SetActive(profile != null && profile.showAmmoArc);

        current = (Moment)(-1);
        if (profile != null && image != null) ApplyLook(Resolve(), true);
    }

    void ApplyLook(Moment moment, bool instant)
    {
        current = moment;

        CrosshairProfile.Look look = null;
        switch (moment)
        {
            case Moment.Reload: look = profile.reload; break;
            case Moment.Hit: look = profile.hit; break;
            case Moment.LowAmmo: look = profile.lowAmmo; break;
        }

        Sprite sprite = look != null && look.sprite != null ? look.sprite : profile.sprite;
        Color color = look != null && look.changeColor ? look.color : profile.color;
        float multiplier = look != null ? look.sizeMultiplier : 1f;

        image.sprite = sprite;
        image.rectTransform.sizeDelta = new Vector2(profile.size, profile.size) * multiplier;

        image.DOKill();
        if (instant || profile.colorFadeTime <= 0f) image.color = color;
        else image.DOColor(color, profile.colorFadeTime).SetUpdate(true);
    }

    void HandleWeaponChanged(WeaponData weapon)
    {
        ApplyProfile(weapon);
    }

    void HandleReloadStarted(float duration)
    {
        reloadUntil = Time.time + Mathf.Max(0f, duration);
    }

    void HandleFired(WeaponData weapon)
    {
        if (profile == null || image == null || profile.firePunchScale <= 0f) return;

        RectTransform visual = image.rectTransform;
        visual.DOKill(true); // termina o pulso anterior (armas automáticas atiram mais rápido que a duração do pulso)
        visual.localScale = Vector3.one;
        visual.DOPunchScale(Vector3.one * profile.firePunchScale, profile.firePunchDuration, 8, 0.6f).SetUpdate(true);
    }

    void RegisterHit()
    {
        float duration = profile != null ? profile.hitDuration : 0.1f;
        hitUntil = Time.time + duration;
    }

    void SetCursorHidden(bool hidden)
    {
        if (cursorHidden == hidden) return;
        cursorHidden = hidden;
        Cursor.visible = !hidden;
    }
}
