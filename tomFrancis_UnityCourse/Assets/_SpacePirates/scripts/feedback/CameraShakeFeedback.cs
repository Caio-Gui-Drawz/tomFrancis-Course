using Unity.Cinemachine;
using UnityEngine;

// Screenshake via Cinemachine Impulse. Precisa de:
//  - CinemachineImpulseSource neste objeto (adicionado automaticamente)
//  - CinemachineImpulseListener na CinemachineCamera (Add Extension > Cinemachine Impulse Listener)
// Chame Play() de um UnityEvent, ou Play(multiplicador) pelo código.
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShakeFeedback : MonoBehaviour
{
    [Tooltip("Força do impulso (multiplica o Amplitude Gain do Impulse Source).")]
    public float force = 1f;

    CinemachineImpulseSource source;

    void Awake()
    {
        source = GetComponent<CinemachineImpulseSource>();
    }

    public void Play()
    {
        Play(1f);
    }

    public void Play(float multiplier)
    {
        if (source == null) return;
        source.GenerateImpulseWithForce(force * multiplier);
    }
}
