using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Entrada e saída animadas de partes do HUD (barra de vida, pontuação, botão de pause...): cada item desliza de uma
// borda da tela até o lugar dele e, na saída, volta pelo mesmo caminho. Roda em tempo real (funciona com o jogo pausado).
//
// Coloque no hudPanel (o objeto que o HUDController liga e desliga). Cada vez que o HUD aparece (início da partida),
// ele entra sozinho. Quando todas as waves terminam, sai sozinho. Também dá para chamar Show() / Hide() por UnityEvent.
//
// Lista 'Items': arraste os objetos do HUD (ex.: HP_BAR_HOLDER, scoreHUD, pauseButton) e escolha de qual borda cada um vem.
// A distância é calculada sozinha: o item sai da tela por completo (considerando as imagens e textos dentro dele).
public class HUDAnimator : MonoBehaviour
{
    public enum Side { Top, Bottom, Left, Right }

    [Serializable]
    public class Item
    {
        public RectTransform target;
        [Tooltip("De qual borda da tela o item vem (e para onde volta ao sair).")]
        public Side comeFrom = Side.Top;
        [Tooltip("Folga extra (pixels) além da borda, para ficar totalmente fora da tela.")]
        [Min(0f)] public float extraMargin = 20f;
        [Tooltip("Espera (s) deste item dentro da animação (para escalonar: um entra depois do outro).")]
        [Min(0f)] public float delay = 0f;
    }

    public Item[] items;

    [Header("Entrada")]
    [Tooltip("Ligado = o HUD entra sozinho toda vez que o painel aparece (início da partida).")]
    public bool autoShowOnEnable = true;
    [Tooltip("Espera (s) depois de o painel aparecer antes de entrar (ex.: esperar o fade da tela e a entrada do player).")]
    [Min(0f)] public float showDelaySeconds = 0.5f;
    [Min(0.01f)] public float showSeconds = 0.6f;
    public Ease showEase = Ease.OutBack;

    [Header("Saída")]
    [Tooltip("Ligado = o HUD sai sozinho quando todas as waves terminam.")]
    public bool hideOnAllWavesCompleted = true;
    [Min(0f)] public float hideDelaySeconds = 0f;
    [Min(0.01f)] public float hideSeconds = 0.5f;
    public Ease hideEase = Ease.InBack;

    [Header("Eventos")]
    public UnityEvent onShown;
    public UnityEvent onHidden;

    public bool IsHidden { get; private set; }

    Vector2[] restPositions; // anchoredPosition (independe de resolução / escala do Canvas)
    Sequence running;
    WaveManager waves;

    void Awake()
    {
        CaptureRest();
    }

    void OnEnable()
    {
        waves = FindFirstObjectByType<WaveManager>();
        if (waves != null) waves.OnAllWavesCompleted += HandleAllWavesCompleted;

        if (autoShowOnEnable)
        {
            HideInstant();
            Show(showDelaySeconds);
        }
    }

    void OnDisable()
    {
        if (waves != null) waves.OnAllWavesCompleted -= HandleAllWavesCompleted;
        waves = null;

        Kill();
        RestoreRest(); // o painel pode ser desligado no meio da animação: tudo volta ao lugar
    }

    void HandleAllWavesCompleted()
    {
        if (hideOnAllWavesCompleted) Hide(hideDelaySeconds);
    }

    // ------------------------------------------------------------------ API

    public void Show() { Show(0f); }
    public void Hide() { Hide(0f); }

    public void Show(float delay)
    {
        Play(true, delay);
    }

    public void Hide(float delay)
    {
        Play(false, delay);
    }

    // Esconde na hora (fora da tela), sem animar.
    public void HideInstant()
    {
        Kill();
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            Item it = items[i];
            if (it == null || it.target == null) continue;
            it.target.anchoredPosition = HiddenAnchored(i);
        }
        IsHidden = true;
    }

    // Mostra na hora, no lugar de descanso.
    public void ShowInstant()
    {
        Kill();
        RestoreRest();
        IsHidden = false;
    }

    // ------------------------------------------------------------------ interno

    void Play(bool show, float delay)
    {
        if (items == null || items.Length == 0) return;
        Kill();

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        float seconds = show ? showSeconds : hideSeconds;
        Ease ease = show ? showEase : hideEase;

        for (int i = 0; i < items.Length; i++)
        {
            Item it = items[i];
            if (it == null || it.target == null) continue;

            RectTransform t = it.target;
            Vector2 to = show ? restPositions[i] : HiddenAnchored(i);
            seq.Insert(delay + it.delay, DOTween.To(() => t.anchoredPosition, v => t.anchoredPosition = v, to, seconds).SetEase(ease));
        }

        seq.OnComplete(() =>
        {
            running = null;
            IsHidden = !show;
            if (show) onShown?.Invoke(); else onHidden?.Invoke();
        });
        running = seq;
    }

    void CaptureRest()
    {
        if (items == null) { restPositions = new Vector2[0]; return; }
        restPositions = new Vector2[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].target != null) restPositions[i] = items[i].target.anchoredPosition;
        }
    }

    void RestoreRest()
    {
        if (items == null || restPositions == null) return;
        for (int i = 0; i < items.Length && i < restPositions.Length; i++)
        {
            Item it = items[i];
            if (it != null && it.target != null) it.target.anchoredPosition = restPositions[i];
        }
    }

    void Kill()
    {
        if (running != null && running.IsActive()) running.Kill();
        running = null;
    }

    // anchoredPosition em que o item fica totalmente fora da tela, saindo pela borda escolhida a partir do lugar de descanso.
    Vector2 HiddenAnchored(int index)
    {
        Item it = items[index];
        RectTransform target = it.target;
        Vector2 rest = restPositions[index];

        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null || target.parent == null) return rest;
        RectTransform root = (RectTransform)canvas.rootCanvas.transform;

        // Mede com o item no lugar de descanso (ele pode estar no meio da animação) e depois devolve.
        Vector2 current = target.anchoredPosition;
        target.anchoredPosition = rest;
        Canvas.ForceUpdateCanvases();
        Bounds visual = VisualBounds(target);
        target.anchoredPosition = current;

        Vector3[] c = new Vector3[4];
        root.GetWorldCorners(c);
        float left = c[0].x, bottom = c[0].y, right = c[2].x, top = c[2].y;

        float margin = it.extraMargin;
        Vector3 world = Vector3.zero;
        switch (it.comeFrom)
        {
            case Side.Top: world = new Vector3(0f, top - visual.min.y + margin, 0f); break;
            case Side.Bottom: world = new Vector3(0f, -(visual.max.y - bottom + margin), 0f); break;
            case Side.Left: world = new Vector3(-(visual.max.x - left + margin), 0f, 0f); break;
            case Side.Right: world = new Vector3(right - visual.min.x + margin, 0f, 0f); break;
        }
        Vector3 local = target.parent.InverseTransformVector(world);
        return rest + new Vector2(local.x, local.y);
    }

    // Caixa que envolve tudo o que aparece dentro do item (imagens e textos visíveis).
    static Bounds VisualBounds(RectTransform target)
    {
        Vector3[] corners = new Vector3[4];
        Bounds b = new Bounds(target.position, Vector3.zero);
        bool any = false;

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(false);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (!g.enabled || g.color.a < 0.01f) continue;
            g.rectTransform.GetWorldCorners(corners);
            for (int k = 0; k < 4; k++)
            {
                if (!any) { b = new Bounds(corners[k], Vector3.zero); any = true; }
                else b.Encapsulate(corners[k]);
            }
        }

        if (!any)
        {
            target.GetWorldCorners(corners);
            b = new Bounds(corners[0], Vector3.zero);
            for (int k = 1; k < 4; k++) b.Encapsulate(corners[k]);
        }
        return b;
    }
}
