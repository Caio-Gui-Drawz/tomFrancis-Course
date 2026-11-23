using UnityEngine;

// Spawna um prefab de partículas/VFX na posição deste objeto. Chame Play() de um UnityEvent
// (ex.: HealthFeedbackEvents) ou pelo código.
public class VfxFeedback : MonoBehaviour
{
    public GameObject vfxPrefab;
    public Vector3 offset;

    [Tooltip("Segundos até o efeito sumir. Com o pool ligado, 0 = some quando as partículas terminam. Sem o pool, 0 = não destrói (use se o próprio prefab se destrói).")]
    public float lifetime = 2f;

    [Tooltip("Ligado = o efeito vem do PoolManager e volta pra ele quando acaba (sem criar/destruir objeto a cada vez). O prefab do efeito NÃO deve se destruir sozinho.")]
    public bool usePool = true;

    public void Play()
    {
        if (vfxPrefab == null) return;

        Vector3 position = transform.position + offset;

        if (usePool && PoolManager.Instance != null)
        {
            GameObject pooled = PoolManager.Instance.Get(vfxPrefab, position, Quaternion.identity);
            PooledVfx handler = pooled.GetComponent<PooledVfx>();
            if (handler == null) handler = pooled.AddComponent<PooledVfx>();
            handler.lifetime = lifetime;
            return;
        }

        GameObject fx = Instantiate(vfxPrefab, position, Quaternion.identity);
        if (lifetime > 0f) Destroy(fx, lifetime);
    }
}
