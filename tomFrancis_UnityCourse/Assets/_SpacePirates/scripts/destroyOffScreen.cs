using UnityEngine;

public class DestroyOffScreen : MonoBehaviour
{
    [Tooltip("Folga (fração da tela) além da borda antes de devolver ao pool. Serve de garantia caso o OnBecameInvisible demore (no Editor ele também espera a Scene view).")]
    public float viewportMargin = 0.15f;

    [Tooltip("Se o objeto passar desta distância (em telas) da câmera, volta ao pool mesmo que nunca tenha aparecido. Pega itens que caem pela gravidade ou são lançados para fora da tela.")]
    public float farViewportMargin = 1.5f;

    bool hasBeenVisible;
    bool seenInViewport;
    bool pendingReturn;

    void OnBecameVisible()
    {
        hasBeenVisible = true;
    }

    void OnBecameInvisible()
    {
        if (hasBeenVisible)
        {
            pendingReturn = true; // só marca a intenção, não age agora
        }
    }

    void LateUpdate()
    {
        if (!pendingReturn) CheckViewport();
        if (!pendingReturn) return;

        pendingReturn = false;
        PoolManager.Instance.Return(gameObject);
    }

    // Plano B: depois de ter aparecido na câmera principal, devolve ao pool assim que passar da borda com folga.
    void CheckViewport()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f)
        {
            seenInViewport = true;
            return;
        }

        if (v.x < -farViewportMargin || v.x > 1f + farViewportMargin || v.y < -farViewportMargin || v.y > 1f + farViewportMargin)
        {
            pendingReturn = true;
            return;
        }

        if (seenInViewport && (v.x < -viewportMargin || v.x > 1f + viewportMargin || v.y < -viewportMargin || v.y > 1f + viewportMargin))
        {
            pendingReturn = true;
        }
    }

    void OnDisable()
    {
        // Reseta para a próxima vez que este objeto for reaproveitado do pool.
        hasBeenVisible = false;
        seenInViewport = false;
        pendingReturn = false;
    }
}
