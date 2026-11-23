using UnityEngine;

// Faz um efeito de partículas que veio do pool (PoolManager.Get) voltar sozinho ao pool quando terminar.
// O VfxFeedback adiciona este componente automaticamente; você também pode colocá-lo no prefab do efeito.
public class PooledVfx : MonoBehaviour
{
    [Tooltip("Segundos até devolver ao pool. 0 = devolve quando todas as partículas morreram.")]
    [Min(0f)] public float lifetime = 0f;

    [Tooltip("Trava de segurança: devolve depois deste tempo (s) mesmo com partículas vivas (sistemas em loop).")]
    [Min(0.5f)] public float maxSeconds = 10f;

    ParticleSystem[] systems;
    float elapsed;

    void OnEnable()
    {
        if (systems == null) systems = GetComponentsInChildren<ParticleSystem>(true);
        elapsed = 0f;

        // Objeto reaproveitado: recomeça do zero (sem sobras da vez anterior).
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Clear(false);
            systems[i].Play(false);
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        bool done;
        if (elapsed >= maxSeconds) done = true;
        else if (lifetime > 0f) done = elapsed >= lifetime;
        else done = elapsed > 0.1f && !AnyAlive();

        if (done) ReturnToPool();
    }

    bool AnyAlive()
    {
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i].IsAlive(false)) return true;
        }
        return false;
    }

    void ReturnToPool()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.Return(gameObject);
        else Destroy(gameObject);
    }
}
