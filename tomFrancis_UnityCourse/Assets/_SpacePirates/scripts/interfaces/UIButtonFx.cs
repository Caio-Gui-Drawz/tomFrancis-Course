using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Efeitos de botão de UI: cresce no hover (mouse) / seleção (teclado, controle), encolhe um pouco ao apertar e dá um
// "punch" ao clicar. Opcionalmente troca a cor do texto. Roda em tempo real, então funciona no menu de pause.
//
// Coloque no objeto do Button (ele precisa ter um Graphic com Raycast Target ligado para receber o mouse).
// Os sons de hover e clique já vêm do ButtonSounds (adicionado automaticamente), não precisa ligar aqui.
[RequireComponent(typeof(Selectable))]
public class UIButtonFx : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Tooltip("O que muda de escala. Vazio = este próprio objeto.")]
    public RectTransform target;

    [Header("Escala")]
    [Tooltip("Escala no hover / selecionado (1 = igual).")]
    [Min(0.01f)] public float hoverScale = 1.08f;
    [Tooltip("Escala enquanto o botão está apertado.")]
    [Min(0.01f)] public float pressedScale = 0.96f;
    [Min(0f)] public float scaleSeconds = 0.12f;
    public Ease ease = Ease.OutQuad;

    [Header("Clique")]
    [Tooltip("Força do 'punch' de escala ao clicar (0 = sem punch).")]
    [Min(0f)] public float clickPunch = 0.08f;
    [Min(0.01f)] public float clickPunchSeconds = 0.25f;

    [Header("Cor do texto (opcional)")]
    public bool changeTextColor = false;
    public TMP_Text label;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;

    [Header("Teclado / controle")]
    [Tooltip("Ligado = o botão selecionado (setas / controle) também cresce como no hover. Selecionar com um clique do mouse não conta.")]
    public bool selectActsAsHover = true;

    Selectable selectable;
    Button button;
    Vector3 baseScale = Vector3.one;
    Tween scaleTween;
    Tween colorTween;

    bool hovered;
    bool pressed;
    bool selected;
    float lastPointerTime = -10f;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        button = GetComponent<Button>();
        if (target == null) target = (RectTransform)transform;
        if (label == null && changeTextColor) label = GetComponentInChildren<TMP_Text>();
        baseScale = target.localScale;
    }

    void OnEnable()
    {
        // Sempre volta ao estado normal ao aparecer (painéis são ligados e desligados o tempo todo).
        hovered = pressed = selected = false;
        Kill();
        target.localScale = baseScale;
        if (changeTextColor && label != null) label.color = normalColor;
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
        Kill();
        if (target != null) target.localScale = baseScale;
        if (changeTextColor && label != null) label.color = normalColor;
    }

    // ------------------------------------------------------------------ ponteiro

    public void OnPointerEnter(PointerEventData e) { Touched(); hovered = true; Refresh(); }

    public void OnPointerExit(PointerEventData e) { Touched(); hovered = false; pressed = false; Refresh(); }

    public void OnPointerDown(PointerEventData e) { Touched(); pressed = true; Refresh(); }

    public void OnPointerUp(PointerEventData e)
    {
        Touched();
        pressed = false;
        // Toque no celular não tem "sair do botão": ao soltar o dedo o hover acaba.
        if (e.pointerId >= 0) hovered = false;
        Refresh();
    }

    // ------------------------------------------------------------------ seleção (teclado / controle)

    public void OnSelect(BaseEventData e)
    {
        // Um clique do mouse também seleciona o botão; isso não deve deixá-lo "preso" crescido.
        if (Time.unscaledTime - lastPointerTime < 0.1f) return;
        selected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }

    void Touched() { lastPointerTime = Time.unscaledTime; }

    // ------------------------------------------------------------------ animação

    void Refresh()
    {
        if (!isActiveAndEnabled) return;

        bool interactable = selectable == null || selectable.interactable;
        bool active = interactable && (hovered || (selectActsAsHover && selected));

        float scale = 1f;
        if (interactable)
        {
            if (pressed) scale = pressedScale;
            else if (active) scale = hoverScale;
        }

        if (scaleTween != null) scaleTween.Kill();
        scaleTween = target.DOScale(baseScale * scale, scaleSeconds).SetEase(ease).SetUpdate(true);

        if (changeTextColor && label != null)
        {
            if (colorTween != null) colorTween.Kill();
            Color to = active ? hoverColor : normalColor;
            colorTween = DOTween.To(() => label.color, c => label.color = c, to, scaleSeconds).SetUpdate(true);
        }
    }

    void OnClicked()
    {
        if (clickPunch <= 0f || !isActiveAndEnabled) return;

        if (scaleTween != null) scaleTween.Kill();
        target.localScale = baseScale;
        scaleTween = target.DOPunchScale(baseScale * clickPunch, clickPunchSeconds, 8, 0.6f)
            .SetUpdate(true)
            .OnComplete(() => { target.localScale = baseScale; Refresh(); });
    }

    void Kill()
    {
        if (scaleTween != null) { scaleTween.Kill(); scaleTween = null; }
        if (colorTween != null) { colorTween.Kill(); colorTween = null; }
    }
}
