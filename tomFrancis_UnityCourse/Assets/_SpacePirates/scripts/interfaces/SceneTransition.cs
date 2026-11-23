using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using FMODUnity;

// Sai de uma tela final (Game Over, vitória...) para uma nova partida ou para o menu:
//   1) dispara 'On Begin Leave' (ligue aqui o evento de fade out da música: StudioEventEmitter.Play, etc);
//   2) espera 'Wait Seconds' (a duração do fade) ;
//   3) recarrega a cena.
// Cliques repetidos durante a espera são ignorados.
//
// Coloque em um objeto sempre ativo (ex.: o Canvas). Qualquer tela final pode usar o mesmo componente:
// ligue o OnClick dos botões em PlayAgain() / BackToMenu(), ou arraste este componente no GameOverMenu.
public class SceneTransition : MonoBehaviour
{
    [Tooltip("Dispara assim que o jogador sai da tela final, antes da espera. Ligue o evento de fade out do FMOD aqui.")]
    public UnityEvent onBeginLeave;
    [Tooltip("Tempo (s) entre o On Begin Leave e o recarregamento da cena. Use a duração do fade out. 0 = recarrega na hora.")]
    [Min(0f)] public float waitSeconds = 2f;

    [Tooltip("Para TODAS as instâncias do FMOD (músicas e snapshots, como o do pause) logo antes de recarregar a cena. Evita snapshot/música presos de uma partida para a outra.")]
    public bool stopAllFmodBeforeReload = true;
    public bool log = true;

    bool leaving;

    public bool IsLeaving { get { return leaving; } }

    public void PlayAgain()
    {
        Leave(true);
    }

    public void BackToMenu()
    {
        Leave(false);
    }

    public void Leave(bool startPlaying)
    {
        if (leaving)
        {
            if (log) Debug.Log("[SceneTransition] clique ignorado: já está saindo.", this);
            return;
        }
        leaving = true;
        if (log) Debug.Log("[SceneTransition] saindo (" + (startPlaying ? "jogar de novo" : "menu") + "), timeScale=" + Time.timeScale + ", espera " + waitSeconds + "s", this);
        StartCoroutine(LeaveRoutine(startPlaying));
    }

    IEnumerator LeaveRoutine(bool startPlaying)
    {
        onBeginLeave?.Invoke();

        // Tempo real: funciona mesmo se o jogo estiver com timeScale 0.
        if (waitSeconds > 0f) yield return new WaitForSecondsRealtime(waitSeconds);

        if (log) Debug.Log("[SceneTransition] espera acabou, recarregando a cena.", this);
        if (stopAllFmodBeforeReload) StopAllFmodInstances();
        GameManager.startPlayingOnLoad = startPlaying;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Para (imediatamente) toda instância de evento e snapshot que estiver tocando.
    void StopAllFmodInstances()
    {
        try
        {
            FMOD.Studio.System system = RuntimeManager.StudioSystem;
            FMOD.Studio.Bank[] banks;
            if (system.getBankList(out banks) != FMOD.RESULT.OK) return;

            int stopped = 0;
            foreach (FMOD.Studio.Bank bank in banks)
            {
                FMOD.Studio.EventDescription[] events;
                if (bank.getEventList(out events) != FMOD.RESULT.OK) continue;

                foreach (FMOD.Studio.EventDescription description in events)
                {
                    FMOD.Studio.EventInstance[] instances;
                    if (description.getInstanceList(out instances) != FMOD.RESULT.OK) continue;
                    foreach (FMOD.Studio.EventInstance instance in instances)
                    {
                        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                        stopped++;
                    }
                }
            }
            if (log) Debug.Log("[SceneTransition] " + stopped + " instância(s) do FMOD paradas antes de recarregar.", this);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SceneTransition] não consegui parar as instâncias do FMOD: " + e.Message, this);
        }
    }
}
