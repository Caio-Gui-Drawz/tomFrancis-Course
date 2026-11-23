using System.Collections;
using DG.Tweening;
using UnityEngine;

// Camada preta em tela cheia que escurece / clareia (alpha) quando você quiser.
//
// Montagem: crie uma Image preta esticada na tela inteira, como ÚLTIMO filho do Canvas (ou num Canvas próprio com
// Sort Order alto), e coloque este componente nela (ele cria o CanvasGroup sozinho). Deixe o alpha da cor da Image em 1.
//
// Ao carregar a cena ele já começa PRETO e faz o fade in sozinho ("Fade In On Start") — isso cobre abrir o jogo e
// reiniciar a cena, sem piscar a tela. Para sair da cena, use o SceneTransition (ele chama FadeToBlack antes de recarregar).
//
// Também dá para ligar por UnityEvent (WaveEventController, botões...): FadeToBlack(segundos) e FadeFromBlack(segundos).
// Usa tempo real: funciona com o jogo pausado (timeScale 0).
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Ao carregar a cena")]
    [Tooltip("Ligado = a tela começa totalmente preta.")]
    public bool startBlack = true;
    [Tooltip("Ligado = depois de começar preta, faz o fade in sozinho.")]
    public bool fadeInOnStart = true;
    [Min(0f)] public float startHoldSeconds = 0.3f;
    [Min(0f)] public float startFadeSeconds = 1.2f;

    [Header("Curva")]
    public Ease ease = Ease.InOutSine;

    CanvasGroup group;
    Tween tween;

    public bool IsFading { get { return tween != null && tween.IsActive() && tween.IsPlaying(); } }
    public bool IsBlack { get { return group != null && group.alpha >= 0.999f && !IsFading; } }

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        if (startBlack) SetBlack(); else SetClear();
    }

    void Start()
    {
        if (startBlack && fadeInOnStart) FadeFromBlack(startFadeSeconds, startHoldSeconds);
    }

    void OnDestroy()
    {
        if (tween != null) tween.Kill();
        if (Instance == this) Instance = null;
    }

    // Instantâneo.
    public void SetBlack() { Kill(); group.alpha = 1f; group.blocksRaycasts = true; }
    public void SetClear() { Kill(); group.alpha = 0f; group.blocksRaycasts = false; }

    // Escurece até ficar preto. Bloqueia cliques assim que começa.
    public void FadeToBlack(float seconds) { Fade(1f, seconds, 0f); }

    // Clareia até sumir. Libera os cliques só no final.
    public void FadeFromBlack(float seconds) { Fade(0f, seconds, 0f); }

    public void FadeFromBlack(float seconds, float delaySeconds) { Fade(0f, seconds, delaySeconds); }

    // Para esperar o fim do fade em coroutine: yield return fader.WaitForFade();
    public IEnumerator WaitForFade()
    {
        while (IsFading) yield return null;
    }

    void Fade(float target, float seconds, float delay)
    {
        Kill();
        if (target > 0f) group.blocksRaycasts = true;

        if (seconds <= 0f && delay <= 0f)
        {
            group.alpha = target;
            group.blocksRaycasts = target > 0f;
            return;
        }

        tween = DOTween.To(() => group.alpha, x => group.alpha = x, target, Mathf.Max(0.0001f, seconds))
            .SetEase(ease)
            .SetDelay(delay)
            .SetUpdate(true)
            .OnComplete(() => { group.blocksRaycasts = target > 0f; });
    }

    void Kill()
    {
        if (tween != null) { tween.Kill(); tween = null; }
    }
}
