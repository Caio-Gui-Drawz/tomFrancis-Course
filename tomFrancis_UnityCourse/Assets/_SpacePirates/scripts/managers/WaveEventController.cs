using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

// Marca um campo string como "escolha um estágio do MusicController" (vira lista de seleção no Inspector).
public class MusicStageAttribute : PropertyAttribute { }

// Eventos de cenário ligados às waves: no momento certo (fim da fase de loot, por exemplo) esperam a música
// chegar no drop (marker do FMOD), tocam uma animação (PlayableDirector) e só então deixam a próxima wave começar.
//
// Exemplo (waves 1-4 -> 5-8):
//   MusicController: estágio  trigger Loot Phase Ended, wave 4, gameState = wave4_8, Wait For Marker = wave4_8
//   Aqui:            evento   When = Loot Phase Ended, Wave = 4, Wait For Music Stage = (o estágio acima),
//                             Director = animação do cenário, Hold Next Wave = ligado
//   => o loot acaba, a música muda no marker, a animação toca, a wave 5 só começa quando ela termina.
//
// Coloque em um GameObject da cena (junto do WaveManager, por exemplo).
public class WaveEventController : MonoBehaviour
{
    public enum When { WaveStarted, WaveCompleted, LootPhaseStarted, LootPhaseEnded }

    [Serializable]
    public class Beat
    {
        public string name = "Novo evento";
        public When when = When.LootPhaseEnded;
        [Tooltip("0 = qualquer wave. N = só quando for a wave N (a wave que começou / terminou).")]
        [Min(0)] public int wave = 0;

        [Tooltip("Opcional: estágio do MusicController. O evento espera esse estágio ALCANÇAR o marker (o drop da música) antes de começar. Vazio = não espera a música.")]
        [MusicStage] public string waitForMusicStage;
        [Tooltip("Espera extra (s) depois do marker, antes de começar.")]
        [Min(0f)] public float delaySeconds = 0f;

        [Tooltip("Animação/Timeline a tocar. O evento dura até ela terminar. Vazio = só dispara os UnityEvents.")]
        public PlayableDirector director;
        [Tooltip("Espera extra (s) depois da animação (ou duração fixa se não houver Director).")]
        [Min(0f)] public float extraSeconds = 0f;

        [Tooltip("Ligado = a próxima wave só começa quando este evento terminar. Só faz efeito em eventos de fim de wave / fim de loot.")]
        public bool holdNextWave = true;
        [Tooltip("Segurança: se o evento passar deste tempo (s), ele é encerrado à força para não travar o jogo.")]
        [Min(1f)] public float maxSeconds = 90f;

        [Tooltip("Dispara quando a animação vai começar.")]
        public UnityEvent onBegin;
        [Tooltip("Dispara quando o evento terminou (antes de liberar a próxima wave).")]
        public UnityEvent onEnd;
    }

    public Beat[] beats;
    public bool log = true;

    WaveManager waves;
    MusicController music;
    readonly List<Beat> running = new List<Beat>();

    void Start()
    {
        waves = FindFirstObjectByType<WaveManager>();
        music = MusicController.Instance != null ? MusicController.Instance : FindFirstObjectByType<MusicController>();

        if (waves != null)
        {
            waves.OnWaveStarted += OnWaveStarted;
            waves.OnWaveCompleted += OnWaveCompleted;
            waves.OnLootPhaseStarted += OnLootStarted;
            waves.OnLootPhaseEnded += OnLootEnded;
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

    void Fire(When when, int wave)
    {
        if (beats == null) return;
        foreach (Beat b in beats)
        {
            if (b == null || b.when != when) continue;
            if (b.wave != 0 && b.wave != wave) continue;
            if (running.Contains(b)) continue;

            // Segura a próxima wave AGORA (dentro do evento do WaveManager), antes de qualquer espera.
            if (b.holdNextWave && waves != null) waves.HoldNextWave(b);
            running.Add(b);
            StartCoroutine(RunBeat(b));
        }
    }

    IEnumerator RunBeat(Beat b)
    {
        float deadline = Time.unscaledTime + Mathf.Max(1f, b.maxSeconds);
        if (log) Debug.Log("[Evento] '" + b.name + "' disparado (" + b.when + (b.wave != 0 ? " wave " + b.wave : "") + ")", this);

        // 1 frame: dá tempo do MusicController (que escuta o mesmo evento) pedir o estágio dele.
        yield return null;

        if (!string.IsNullOrEmpty(b.waitForMusicStage))
        {
            if (music == null) music = FindFirstObjectByType<MusicController>();
            if (music == null)
            {
                Debug.LogWarning("WaveEventController: '" + b.name + "' quer esperar um estágio de música, mas não há MusicController na cena.", this);
            }
            else
            {
                while (music.IsStagePending(b.waitForMusicStage) && Time.unscaledTime < deadline) yield return null;
                if (log) Debug.Log("[Evento] '" + b.name + "': música chegou em '" + b.waitForMusicStage + "'", this);
            }
        }

        if (b.delaySeconds > 0f) yield return new WaitForSeconds(b.delaySeconds);

        b.onBegin?.Invoke();

        if (b.director != null)
        {
            b.director.time = 0;
            b.director.Play();
            yield return null; // deixa o estado virar Playing
            float length = (float)b.director.duration;
            while (Time.unscaledTime < deadline)
            {
                if (b.director.state != PlayState.Playing) break;
                if (length > 0.01f && b.director.time >= length - 0.0001) break;
                yield return null;
            }
        }

        if (b.extraSeconds > 0f) yield return new WaitForSeconds(b.extraSeconds);

        if (Time.unscaledTime >= deadline) Debug.LogWarning("WaveEventController: '" + b.name + "' passou de " + b.maxSeconds + "s e foi encerrado à força.", this);

        b.onEnd?.Invoke();
        if (waves != null) waves.ReleaseNextWave(b);
        running.Remove(b);
        if (log) Debug.Log("[Evento] '" + b.name + "' terminou" + (b.holdNextWave ? " (próxima wave liberada)" : ""), this);
    }
}
