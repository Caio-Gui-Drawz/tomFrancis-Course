using UnityEngine;

// Spawna um prefab de partículas/VFX na posição deste objeto. Chame Play() de um UnityEvent
// (ex.: HealthFeedbackEvents) ou pelo código.
public class VfxFeedback : MonoBehaviour
{
    public GameObject vfxPrefab;
    public Vector3 offset;

    [Tooltip("Segundos até destruir o efeito. 0 = não destrói (use se o próprio prefab se destrói).")]
    public float lifetime = 2f;

    public void Play()
    {
        if (vfxPrefab == null) return;

        GameObject fx = Instantiate(vfxPrefab, transform.position + offset, Quaternion.identity);
        if (lifetime > 0f) Destroy(fx, lifetime);
    }
}
