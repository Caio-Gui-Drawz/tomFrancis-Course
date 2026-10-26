using DG.Tweening;
using UnityEngine;

// Faz um Transform "tremer": bounce de escala e/ou chacoalhada de rotação, usando DOTween.
// Funciona em qualquer coisa que tenha Transform: inimigo, tesouro, botão/painel de UI (RectTransform)...
// Chame Play() de código ou de um UnityEvent no Inspector (ex.: HealthFeedbackEvents, Button.onClick).
//
// Dica: se o objeto raiz já tem scripts mexendo em escala/rotação, aponte "Target" para um filho
// (o visual/sprite/Spine) pra não brigar com eles.
public class ShakeFeedback : MonoBehaviour
{
    public enum RotationStyle
    {
        Punch, // vai e volta de forma elástica, sempre pro mesmo lado (sensação de "pancada")
        Shake  // chacoalha aleatoriamente pros dois lados
    }

    [Tooltip("Quem treme. Vazio = este próprio objeto.")]
    public Transform target;

    [Header("Duração")]
    public float duration = 0.25f;
    [Tooltip("Ligado = continua tocando mesmo com Time.timeScale = 0 (pausa, hit-stop). Bom pra UI.")]
    public bool useUnscaledTime = false;

    [Header("Bounce de escala")]
    public bool useScaleBounce = true;
    [Tooltip("Quanto o objeto 'estufa' no pico. 0.2 = +20%. Negativo = encolhe.")]
    public float scaleAmount = 0.2f;
    [Tooltip("Quantas oscilações o bounce faz até assentar (DOTween 'vibrato'). Mais = mais vibração.")]
    public int scaleVibrato = 8;
    [Tooltip("0 = amortecido, 1 = bem elástico (passa do ponto antes de voltar).")]
    [Range(0f, 1f)] public float scaleElasticity = 1f;

    [Header("Chacoalhada de rotação")]
    public bool useRotationShake = true;
    public RotationStyle rotationStyle = RotationStyle.Punch;
    [Tooltip("Ângulo máximo, em graus.")]
    public float rotationAngle = 12f;
    [Tooltip("Quantas oscilações a rotação faz (vibrato).")]
    public int rotationVibrato = 10;
    [Tooltip("Punch: elasticidade. Shake: o quão aleatória é a direção (0..90 no DOTween; aqui 0..1).")]
    [Range(0f, 1f)] public float rotationRandomnessOrElasticity = 0.5f;

    [Header("Intensidade")]
    [Tooltip("Multiplicador geral (1 = como configurado). Play(float) multiplica por cima disso.")]
    public float baseIntensity = 1f;

    Sequence sequence;      // guarda a animação em andamento (pra poder reiniciar/cancelar)
    Vector3 baseScale;
    Quaternion baseRotation;

    Transform Target { get { return target != null ? target : transform; } }

    public void Play()
    {
        Play(1f);
    }

    public void Play(float intensityMultiplier)
    {
        if (!isActiveAndEnabled) return;

        // Se já estava tremendo, cancela e devolve o objeto ao estado original ANTES de recomeçar,
        // senão a base ficaria "suja" (já meio estufada) e o objeto derivaria a cada hit seguido.
        StopAndRestore();

        Transform t = Target;
        baseScale = t.localScale;
        baseRotation = t.localRotation;

        float intensity = baseIntensity * intensityMultiplier;

        // Sequence = "linha do tempo" que roda vários tweens juntos (Join) ou um depois do outro (Append).
        sequence = DOTween.Sequence();

        if (useScaleBounce)
        {
            // DOPunchScale: empurra a escala por "punch" e volta ao valor original sozinho.
            sequence.Join(t.DOPunchScale(Vector3.one * (scaleAmount * intensity), duration, scaleVibrato, scaleElasticity));
        }

        if (useRotationShake)
        {
            Vector3 strength = new Vector3(0f, 0f, rotationAngle * intensity); // 2D: só gira no eixo Z
            if (rotationStyle == RotationStyle.Punch)
            {
                sequence.Join(t.DOPunchRotation(strength, duration, rotationVibrato, rotationRandomnessOrElasticity));
            }
            else
            {
                sequence.Join(t.DOShakeRotation(duration, strength, rotationVibrato, rotationRandomnessOrElasticity * 90f, true));
            }
        }

        sequence.SetUpdate(useUnscaledTime);   // true = ignora Time.timeScale
        sequence.SetLink(gameObject);          // se o objeto for destruído, o tween morre junto (sem erros)
        sequence.OnComplete(OnFinished);
    }

    void OnFinished()
    {
        Restore(); // garante o valor exato, sem sobra de arredondamento
        sequence = null;
    }

    void StopAndRestore()
    {
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill();
            Restore();
        }
        sequence = null;
    }

    void Restore()
    {
        Transform t = Target;
        if (t == null) return;
        t.localScale = baseScale;
        t.localRotation = baseRotation;
    }

    // Objetos do pool são desligados no meio do tremor: cancela e devolve escala/rotação originais.
    void OnDisable()
    {
        StopAndRestore();
    }
}
