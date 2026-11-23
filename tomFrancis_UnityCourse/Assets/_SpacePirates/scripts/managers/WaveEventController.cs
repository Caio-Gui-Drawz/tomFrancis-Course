using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Marca um campo string como "escolha um parâmetro Trigger do Animator acima" (lista de seleção no Inspector).
public class AnimatorTriggerAttribute : PropertyAttribute { }
// Marca um campo string como "escolha um estado do Animator acima" (lista de seleção no Inspector).
public class AnimatorStateAttribute : PropertyAttribute { }

// Eventos de cenário ligados às waves. No momento certo (fim da fase de loot, por exemplo), cada evento:
//   1) dispara o UnityEvent On Begin (use para o fade/música do FMOD: StudioEventEmitter.Play, etc);
//   2) espera 'Animation Delay Seconds', dispara um Trigger de um Animator e espera o estado terminar;
//   3) espera 'Extra Seconds', dispara On End e libera a próxima wave (se Hold Next Wave estiver ligado).
//
// Coloque em um GameObject da cena (junto do WaveManager, por exemplo).
public class WaveEventController : MonoBehaviour
{
    public enum When { WaveStarted, WaveCompleted, LootPhaseStarted, LootPhaseEnded, AllWavesCompleted }
    
    [Serializable]
    public class Beat
    {
        public string name = "Novo evento";
        public When when = When.LootPhaseEnded;
        [Tooltip("0 = qualquer wave. N = só quando for a wave N (a wave que começou / terminou). Ignorado em All Waves Completed.")]
        [Min(0)] public int wave = 0;

        [Tooltip("Ligado = a próxima wave só começa quando este evento terminar. Só faz efeito em eventos de fim de wave / fim de loot.")]
        public bool holdNextWave = true;
        [Tooltip("Espera (s) antes de começar o evento.")]
        [Min(0f)] public float delaySeconds = 0f;

        [Header("Animação")]
        public Animator animator;
        [Tooltip("Parâmetro Trigger do Animator que será disparado. Vazio = sem animação.")]
        [AnimatorTrigger] public string trigger;
        [Tooltip("Layer do Animator onde está o estado de espera.")]
        [Min(0)] public int animatorLayer = 0;
        [Tooltip("Estado que o Animator vai tocar. O evento espera ele começar e terminar (ou sair dele). Vazio = não espera a animação.")]
        [AnimatorState] public string waitForState;
        [Tooltip("Espera (s) entre o On Begin e o disparo do trigger (ex.: deixar um fade de música terminar antes da animação).")]
        [Min(0f)] public float animationDelaySeconds = 0f;
        [Tooltip("Espera extra (s) depois da animação.")]
        [Min(0f)] public float extraSeconds = 0f;

        [Header("Segurança / eventos")]
        [Tooltip("Se o evento passar deste tempo (s), ele é encerrado à força para não travar o jogo. 0 = padrão (90s).")]
        [Min(0f)] public float maxSeconds = 90f;
        public float EffectiveMaxSeconds { get { return maxSeconds > 0f ? maxSeconds : 90f; } }

        [Tooltip("Dispara quando o evento começa (depois do delay).")]
        public UnityEvent onBegin;
        [Tooltip("Dispara quando o evento terminou (antes de liberar a próxima wave).")]
        public UnityEvent onEnd;
    }

    public Beat[] beats;
    public bool log = true;

    WaveManager waves;
    readonly List<Beat> running = new List<Beat>();

    void Start()
    {
        waves = FindFirstObjectByType<WaveManager>();

        if (waves != null)
        {
            waves.OnWaveStarted += OnWaveStarted;
            waves.OnWaveCompleted += OnWaveCompleted;
            waves.OnLootPhaseStarted += OnLootStarted;
            waves.OnLootPhaseEnded += OnLootEnded;
            waves.OnAllWavesCompleted += OnAllCompleted;
        }
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (waves != null)
        {
            waves.OnWaveStarted -= OnWaveStarted;
            waves.OnWaveCompleted -= OnWaveCompleted;
            waves.OnLootPhaseStarted -= OnLootStarted;
            waves.OnLootPhaseEnded -= OnLootEnded;
            waves.OnAllWavesCompleted -= OnAllCompleted;
            ReleaseAll();
        }
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameState state)
    {
        // Voltou ao menu / morreu: cancela eventos em andamento e libera as waves.
        if (state == GameState.Menu || state == GameState.GameOver)
        {
            StopAllCoroutines();
            ReleaseAll();
            running.Clear();
        }
    }

    void ReleaseAll()
    {
        if (beats == null || waves == null) return;
        foreach (Beat b in beats) waves.ReleaseNextWave(b);
    }

    void OnWaveStarted(int wave) { Fire(When.WaveStarted, wave); }
    void OnWaveCompleted(int wave) { Fire(When.WaveCompleted, wave); }
    void OnLootStarted(int wave) { Fire(When.LootPhaseStarted, wave); }
    void OnLootEnded(int wave) { Fire(When.LootPhaseEnded, wave); }
    void OnAllCompleted() { Fire(When.AllWavesCompleted, 0); }

    void Fire(When when, int wave)
    {
        if (beats == null) return;
        foreach (Beat b in beats)
        {
            if (b == null || b.when != when) continue;
            if (when != When.AllWavesCompleted && b.wave != 0 && b.wave != wave) continue;
            if (running.Contains(b)) continue;

            // Segura a próxima wave AGORA (dentro do evento do WaveManager), antes de qualquer espera.
            if (b.holdNextWave && waves != null) waves.HoldNextWave(b);
            running.Add(b);
            StartCoroutine(RunBeat(b));
        }
    }

    IEnumerator RunBeat(Beat b)
    {
        float deadline = Time.unscaledTime + b.EffectiveMaxSeconds;
        if (log) Debug.Log("[Evento] '" + b.name + "' disparado (" + b.when + (b.wave != 0 ? " wave " + b.wave : "") + ")", this);

        if (b.delaySeconds > 0f) yield return new WaitForSeconds(b.delaySeconds);

        b.onBegin?.Invoke();

        if (b.animationDelaySeconds > 0f) yield return new WaitForSeconds(b.animationDelaySeconds);

        yield return StartCoroutine(RunAnimation(b, deadline));

        if (b.extraSeconds > 0f) yield return new WaitForSeconds(b.extraSeconds);

        if (Time.unscaledTime >= deadline) Debug.LogWarning("WaveEventController: '" + b.name + "' passou de " + b.EffectiveMaxSeconds + "s e foi encerrado à força.", this);

        b.onEnd?.Invoke();
        if (waves != null) waves.ReleaseNextWave(b);
        running.Remove(b);
        if (log) Debug.Log("[Evento] '" + b.name + "' terminou" + (b.holdNextWave ? " (próxima wave liberada)" : ""), this);
    }

    IEnumerator RunAnimation(Beat b, float deadline)
    {
        if (b.animator == null || string.IsNullOrEmpty(b.trigger)) yield break;

        if (!b.animator.isActiveAndEnabled)
        {
            Debug.LogWarning("WaveEventController: '" + b.name + "': o Animator está desativado, não dá para disparar '" + b.trigger + "'.", this);
            yield break;
        }

        if (log) Debug.Log("[Evento] '" + b.name + "': trigger '" + b.trigger + "'", this);
        b.animator.SetTrigger(b.trigger);

        if (string.IsNullOrEmpty(b.waitForState)) yield break;

        float enterDeadline = Time.unscaledTime + 3f;
        bool entered = false;

        while (Time.unscaledTime < deadline)
        {
            if (b.animator == null || !b.animator.isActiveAndEnabled) break;

            AnimatorStateInfo cur = b.animator.GetCurrentAnimatorStateInfo(b.animatorLayer);
            bool inTransition = b.animator.IsInTransition(b.animatorLayer);
            bool curIs = cur.IsName(b.waitForState);
            bool nextIs = inTransition && b.animator.GetNextAnimatorStateInfo(b.animatorLayer).IsName(b.waitForState);

            if (!entered)
            {
                if (curIs || nextIs) entered = true;
                else if (Time.unscaledTime > enterDeadline)
                {
                    Debug.LogWarning("WaveEventController: '" + b.name + "': o Animator não entrou no estado '" + b.waitForState +
                                     "' em 3s depois do trigger '" + b.trigger + "'. Confira a transição e a layer. Seguindo sem esperar.", this);
                    break;
                }
            }
            else
            {
                if (!curIs && !nextIs) break; // saiu do estado
                if (curIs && !cur.loop && !inTransition && cur.normalizedTime >= 1f) break; // terminou
            }
            yield return null;
        }
    }
}
