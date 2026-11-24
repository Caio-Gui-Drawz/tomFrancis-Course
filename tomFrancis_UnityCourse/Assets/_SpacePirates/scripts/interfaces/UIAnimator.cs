using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Entrada e saída animadas para qualquer elemento de UI (logo, botão, painel de pausa / morte / vitória...).
// Efeitos combináveis: deslizar de uma borda da tela, fade de opacidade e escala (de 0 até o tamanho normal).
// Roda em tempo real (funciona com o jogo pausado). O lugar final é sempre o que você deixou na cena.
//
// Alvos:
//   Self     = anima o próprio objeto (ex.: o painel inteiro, a logo).
//   Children = anima cada filho direto, um depois do outro (escalonado). Bom para uma lista de botões.
//              (Evite 'Deslizar' com filhos dentro de um Layout Group: o layout briga com a posição. Use fade/escala.)
//
// Uso típico:
//   - Painel (pausa, morte...): coloque no painel, deixe 'Auto Show On Enable' ligado. Ao ligar o painel, ele entra.
//     Para fechar com animação, chame HideAndDeactivate() (ou Hide() e depois desligue por onHidden).
//   - Logo / botão do menu: chame Hide() quando o jogador apertar Play e espere IsPlaying ficar falso (ou use onHidden).
public class UIAnimator : MonoBehaviour
{
    public enum Targets { Self, Children }
    public enum Side { Top, Bottom, Left, Right }

    [Header("Alvos")]
    public Targets targets = Targets.Self;

    [Header("Efeitos")]
    [Tooltip("Desliza de uma borda da tela até o lugar normal (e volta pela mesma borda na saída).")]
    public bool slide = true;
    public Side comeFrom = Side.Top;
    [Tooltip("Folga extra (pixels) além da borda, para ficar totalmente fora da tela.")]
    [Min(0f)] public float extraMargin = 20f;

    [Tooltip("Fade de opacidade (0 -> 1 na entrada, 1 -> 0 na saída).")]
    public bool fade = false;

    [Tooltip("Escala (de 'Scale From' até o tamanho normal, e volta na saída).")]
    public bool scale = false;
    [Range(0f, 1f)] public float scaleFrom = 0f;

    [Header("Entrada")]
    [Tooltip("Ligado = entra sozinho toda vez que o objeto é ligado.")]
    public bool autoShowOnEnable = true;
    [Min(0f)] public float showDelaySeconds = 0f;
    [Min(0.01f)] public float showSeconds = 0.5f;
    public Ease showEase = Ease.OutBack;
    [Tooltip("Intervalo (s) entre um filho e o próximo na entrada (só no alvo Children).")]
    [Min(0f)] public float showStaggerSeconds = 0.08f;

    [Header("Saída")]
    [Min(0.01f)] public float hideSeconds = 0.4f;
    public Ease hideEase = Ease.InBack;
    [Tooltip("Intervalo (s) entre um filho e o próximo na saída (só no alvo Children).")]
    [Min(0f)] public float hideStaggerSeconds = 0.05f;
    [Tooltip("Na saída, os filhos saem do último para o primeiro.")]
    public bool reverseHideOrder = false;
    [Tooltip("Ligado = quando a saída termina, o objeto é desligado (SetActive false).")]
    public bool deactivateWhenHidden = false;

    [Header("Eventos")]
    public UnityEvent onShown;
    public UnityEvent onHidden;

    public bool IsHidden { get; private set; }
    public bool IsPlaying { get { return running != null && running.IsActive(); } }

    class Element
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public Vector2 restPos;
        public Vector3 restScale;
        public float restAlpha;
        public bool restInteractable;
        public bool restBlocks;
    }

    readonly List<Element> elements = new List<Element>();
    Sequence running;
    bool initialized;
    bool lastWasShow = true;

    // ------------------------------------------------------------------ ciclo de vida

    void OnEnable()
    {
        EnsureInit();
        if (autoShowOnEnable)
        {
            HideInstant();
            Show(showDelaySeconds);
        }
    }

    void OnDisable()
    {
        Kill();
        if (initialized) RestoreRest(); // painel desligado no meio da animação: tudo volta ao normal
    }

    void OnDestroy() { Kill(); }

    void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        elements.Clear();

        if (targets == Targets.Self)
        {
            AddElement(transform as RectTransform);
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                AddElement(c as RectTransform);
            }
            if (slide && GetComponent<LayoutGroup>() != null)
                Debug.LogWarning("UIAnimator '" + name + "': 'Deslizar' com filhos dentro de um Layout Group pode ser desfeito pelo layout. Use fade/escala.", this);
        }
    }

    void AddElement(RectTransform rt)
    {
        if (rt == null) return;
        Element e = new Element();
        e.rt = rt;
        e.cg = rt.GetComponent<CanvasGroup>();
        if (e.cg == null) e.cg = rt.gameObject.AddComponent<CanvasGroup>();
        e.restPos = rt.anchoredPosition;
        e.restScale = rt.localScale;
        e.restAlpha = e.cg.alpha;
        e.restInteractable = e.cg.interactable;
        e.restBlocks = e.cg.blocksRaycasts;
        elements.Add(e);
    }

    // ------------------------------------------------------------------ API (use por UnityEvent ou por código)

    public void Show() { Show(0f); }
    public void Hide() { Hide(0f); }
    public void Show(float delay) { Play(true, delay, false); }
    public void Hide(float delay) { Play(false, delay, deactivateWhenHidden); }

    // Sai com animação e depois desliga o objeto.
    public void HideAndDeactivate() { Play(false, 0f, true); }

    // Está no meio da animação de saída.
    public bool IsHiding { get { return IsPlaying && !lastWasShow; } }

    // Liga / desliga um painel usando o UIAnimator dele (se tiver). Sem UIAnimator, é só SetActive.
    // Use nos scripts que controlam painéis por estado do jogo (pausa, morte, vitória, menu).
    public static void SetPanelVisible(GameObject panel, bool visible)
    {
        if (panel == null) return;
        UIAnimator a = panel.GetComponent<UIAnimator>();
        if (a == null) { panel.SetActive(visible); return; }

        if (visible)
        {
            if (!panel.activeSelf)
            {
                panel.SetActive(true); // OnEnable faz a entrada (se Auto Show On Enable estiver ligado)
                if (!a.autoShowOnEnable) { a.HideInstant(); a.Show(a.showDelaySeconds); }
            }
            else if (a.IsHidden || a.IsHiding)
            {
                a.Show(0f); // estava saindo (ou escondido) e voltou a ser pedido
            }
        }
        else
        {
            if (!panel.activeSelf || a.IsHiding) return;
            a.HideAndDeactivate();
        }
    }

    // Esconde na hora, sem animar.
    public void HideInstant()
    {
        EnsureInit();
        Kill();
        for (int i = 0; i < elements.Count; i++) ApplyState(elements[i], false);
        SetInteraction(false);
        IsHidden = true;
    }

    // Mostra na hora, no lugar normal.
    public void ShowInstant()
    {
        EnsureInit();
        Kill();
        RestoreRest();
        IsHidden = false;
    }

    // Para usar em corrotinas: yield return animator.WaitUntilDone();
    public IEnumerator WaitUntilDone()
    {
        while (IsPlaying) yield return null;
    }

    [ContextMenu("Testar: entrar")]
    void TestShow() { if (Application.isPlaying) { HideInstant(); Show(); } }

    [ContextMenu("Testar: sair")]
    void TestHide() { if (Application.isPlaying) Hide(); }

    // ------------------------------------------------------------------ interno

    void Play(bool show, float delay, bool deactivate)
    {
        if (!isActiveAndEnabled) return;
        EnsureInit();
        Kill();
        if (elements.Count == 0) return;

        lastWasShow = show;
        if (show) IsHidden = false;
        SetInteraction(false); // não dá para clicar enquanto entra ou sai

        float seconds = show ? showSeconds : hideSeconds;
        Ease ease = show ? showEase : hideEase;
        float stagger = targets == Targets.Children ? (show ? showStaggerSeconds : hideStaggerSeconds) : 0f;

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        int n = elements.Count;
        for (int i = 0; i < n; i++)
        {
            int order = (!show && reverseHideOrder) ? (n - 1 - i) : i;
            float at = delay + order * stagger;
            AddTweens(seq, elements[i], show, at, seconds, ease);
        }

        seq.OnComplete(() =>
        {
            running = null;
            IsHidden = !show;
            if (show) SetInteraction(true);
            if (show) onShown?.Invoke(); else onHidden?.Invoke();
            if (!show && deactivate) gameObject.SetActive(false);
        });
        running = seq;
    }

    void AddTweens(Sequence seq, Element e, bool show, float at, float seconds, Ease ease)
    {
        RectTransform rt = e.rt;
        CanvasGroup cg = e.cg;

        if (slide)
        {
            Vector2 to = show ? e.restPos : HiddenAnchored(e);
            seq.Insert(at, DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, to, seconds).SetEase(ease));
        }
        if (fade)
        {
            float to = show ? e.restAlpha : 0f;
            seq.Insert(at, DOTween.To(() => cg.alpha, v => cg.alpha = Mathf.Clamp01(v), to, seconds).SetEase(ease));
        }
        if (scale)
        {
            Vector3 to = show ? e.restScale : e.restScale * scaleFrom;
            seq.Insert(at, DOTween.To(() => rt.localScale, v => rt.localScale = v, to, seconds).SetEase(ease));
        }
    }

    // Estado "escondido" (show=false) ou "normal" (show=true) aplicado na hora.
    void ApplyState(Element e, bool show)
    {
        if (e.rt == null) return;
        if (show)
        {
            e.rt.anchoredPosition = e.restPos;
            e.rt.localScale = e.restScale;
            e.cg.alpha = e.restAlpha;
            return;
        }
        if (slide) e.rt.anchoredPosition = HiddenAnchored(e);
        if (scale) e.rt.localScale = e.restScale * scaleFrom;
        if (fade) e.cg.alpha = 0f;
    }

    void RestoreRest()
    {
        for (int i = 0; i < elements.Count; i++) ApplyState(elements[i], true);
        SetInteraction(true);
    }

    void SetInteraction(bool on)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            Element e = elements[i];
            if (e.cg == null) continue;
            e.cg.interactable = on ? e.restInteractable : false;
            e.cg.blocksRaycasts = on ? e.restBlocks : false;
        }
    }

    void Kill()
    {
        if (running != null && running.IsActive()) running.Kill();
        running = null;
    }

    // anchoredPosition em que o elemento fica totalmente fora da tela, saindo pela borda escolhida.
    Vector2 HiddenAnchored(Element e)
    {
        RectTransform rt = e.rt;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null || rt.parent == null) return e.restPos;
        RectTransform root = (RectTransform)canvas.rootCanvas.transform;

        // Mede com o elemento no lugar normal (ele pode estar no meio da animação) e depois devolve.
        Vector2 curPos = rt.anchoredPosition;
        Vector3 curScale = rt.localScale;
        rt.anchoredPosition = e.restPos;
        rt.localScale = e.restScale;
        Canvas.ForceUpdateCanvases();
        Bounds visual = VisualBounds(rt);
        rt.anchoredPosition = curPos;
        rt.localScale = curScale;

        Vector3[] c = new Vector3[4];
        root.GetWorldCorners(c);
        float left = c[0].x, bottom = c[0].y, right = c[2].x, top = c[2].y;

        Vector3 world = Vector3.zero;
        switch (comeFrom)
        {
            case Side.Top: world = new Vector3(0f, top - visual.min.y + extraMargin, 0f); break;
            case Side.Bottom: world = new Vector3(0f, -(visual.max.y - bottom + extraMargin), 0f); break;
            case Side.Left: world = new Vector3(-(visual.max.x - left + extraMargin), 0f, 0f); break;
            case Side.Right: world = new Vector3(right - visual.min.x + extraMargin, 0f, 0f); break;
        }
        Vector3 local = rt.parent.InverseTransformVector(world);
        return e.restPos + new Vector2(local.x, local.y);
    }

    // Caixa que envolve tudo o que aparece dentro do elemento (imagens e textos visíveis).
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
