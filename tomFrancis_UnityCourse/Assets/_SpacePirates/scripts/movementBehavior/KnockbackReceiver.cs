using System.Collections.Generic;
using UnityEngine;

// Deixa este inimigo ser EMPURRADO (por bala ou explosão) sem quebrar os movimentos dele.
//
// Como funciona: ao receber o empurrão, pausa os movimentos (IMovementBehavior) do inimigo, empurra o
// Rigidbody2D com uma velocidade que vai caindo até zero e, no fim, religa os movimentos. Como ligar
// de novo roda o OnEnable de cada movimento, o inimigo volta a andar a partir de onde parou
// (os movimentos que guardam a posição de origem passam a usar o ponto novo).
// Funciona com todos os movimentos, inclusive os que mexem direto no transform (Sine, Circular, Parabolic, Path).
//
// Coloque no prefab do inimigo (precisa de Rigidbody2D). Quem não tiver este componente não é empurrado.
[RequireComponent(typeof(Rigidbody2D))]
public class KnockbackReceiver : MonoBehaviour
{
    [Tooltip("0 = empurrão normal. 1 = imune (ex.: inimigo pesado). Valores intermediários reduzem a força.")]
    [Range(0f, 1f)] public float resistance = 0f;

    [Tooltip("Multiplicador da velocidade ao longo do empurrão (eixo X = 0 início, 1 fim). Padrão: começa forte e vai a zero.")]
    public AnimationCurve decay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("Ligado = se o corpo estiver com a posição travada (ex.: bonecos parados da cena Gym), destrava só durante o empurrão e trava de novo no fim.")]
    public bool ignoreFrozenConstraints = true;

    Rigidbody2D body;
    readonly List<Behaviour> pausedMovements = new List<Behaviour>();
    Vector2 velocity;
    float duration;
    float elapsed;
    bool active;
    RigidbodyConstraints2D savedConstraints;
    bool constraintsOverridden;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    // direction = para onde empurrar; force = velocidade inicial (unidades/segundo); time = duração em segundos.
    public void Apply(Vector2 direction, float force, float time)
    {
        if (!isActiveAndEnabled || resistance >= 1f || force <= 0f || time <= 0f) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        velocity = direction.normalized * force * (1f - resistance);
        duration = time;
        elapsed = 0f;

        if (!active) Begin();
    }

    void Begin()
    {
        active = true;

        pausedMovements.Clear();
        foreach (IMovementBehavior movement in GetComponentsInChildren<IMovementBehavior>())
        {
            Behaviour behaviour = movement as Behaviour;
            if (behaviour != null && behaviour.enabled)
            {
                behaviour.enabled = false;
                pausedMovements.Add(behaviour);
            }
        }

        constraintsOverridden = false;
        if (ignoreFrozenConstraints && (body.constraints & (RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY)) != 0)
        {
            savedConstraints = body.constraints;
            body.constraints = savedConstraints & RigidbodyConstraints2D.FreezeRotation;
            constraintsOverridden = true;
        }
    }

    void FixedUpdate()
    {
        if (!active) return;

        elapsed += Time.fixedDeltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            End();
            return;
        }

        body.linearVelocity = velocity * Mathf.Max(0f, decay.Evaluate(t));
    }

    void End()
    {
        active = false;
        body.linearVelocity = Vector2.zero;

        if (constraintsOverridden)
        {
            body.constraints = savedConstraints;
            constraintsOverridden = false;
        }

        // Religa os movimentos: o OnEnable de cada um recomeça a partir da posição atual.
        foreach (Behaviour behaviour in pausedMovements)
        {
            if (behaviour != null) behaviour.enabled = true;
        }
        pausedMovements.Clear();
    }

    // Objeto desativado/devolvido ao pool no meio do empurrão: restaura tudo, senão o próximo uso
    // do mesmo objeto nasceria com os movimentos desligados.
    void OnDisable()
    {
        if (active) End();
    }
}
