using System.Collections;
using UnityEngine;
using FMODUnity;

// DEBUG: coloque no MESMO objeto do StudioEventEmitter (ex.: o pausePanel) para ver no Console
// se o evento/snapshot realmente começa e para de tocar. Remova depois de resolver.
[RequireComponent(typeof(StudioEventEmitter))]
public class FmodEmitterDebug : MonoBehaviour
{
    StudioEventEmitter emitter;

    void Awake()
    {
        emitter = GetComponent<StudioEventEmitter>();
    }

    void OnEnable()
    {
        Debug.Log("[FmodDebug] " + name + " OnEnable | " + Describe("antes de checar") + " | timeScale=" + Time.timeScale, this);
        StartCoroutine(CheckLater());
    }

    void OnDisable()
    {
        if (emitter == null) return;
        Debug.Log("[FmodDebug] " + name + " OnDisable | " + Describe("no disable"), this);
    }

    IEnumerator CheckLater()
    {
        yield return null;
        Debug.Log("[FmodDebug] " + name + " +1 frame | " + Describe("1 frame depois"), this);
        yield return new WaitForSecondsRealtime(0.5f);
        Debug.Log("[FmodDebug] " + name + " +0.5s | " + Describe("0.5s depois"), this);
    }

    string Describe(string when)
    {
        if (emitter == null) return "sem emitter";

        string text = "emitterEnabled=" + emitter.enabled + ", evento=" + (emitter.EventReference.IsNull ? "VAZIO" : emitter.EventReference.Path) +
                      ", playTrigger=" + emitter.PlayEvent + ", stopTrigger=" + emitter.StopEvent;
        try
        {
            text += ", IsPlaying=" + emitter.IsPlaying();

            FMOD.Studio.EventInstance instance = emitter.EventInstance;
            text += ", instanciaValida=" + instance.isValid();
            if (instance.isValid())
            {
                FMOD.Studio.PLAYBACK_STATE state;
                instance.getPlaybackState(out state);
                text += ", estado=" + state;
            }

            FMOD.Studio.EventDescription description = emitter.EventDescription;
            if (description.isValid())
            {
                bool snapshot;
                description.isSnapshot(out snapshot);
                int count;
                description.getInstanceCount(out count);
                text += ", ehSnapshot=" + snapshot + ", instanciasVivas=" + count;
            }
            else text += ", descricaoInvalida";
        }
        catch (System.Exception e)
        {
            text += ", erro=" + e.Message;
        }
        return text;
    }
}
