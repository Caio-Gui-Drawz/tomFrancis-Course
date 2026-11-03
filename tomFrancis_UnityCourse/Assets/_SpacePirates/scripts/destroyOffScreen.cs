using UnityEngine;

public class DestroyOffScreen : MonoBehaviour
{
    [Tooltip("Folga (fração da tela) além da borda, contada a partir da parte do objeto que sai por último (o objeto só volta ao pool quando está TODO fora da tela). Serve de garantia caso o OnBecameInvisible demore (no Editor ele também espera a Scene view).")]
    public float offscreenSlack = 0.02f;

    [Tooltip("Se o objeto passar desta distância (em telas) da câmera, volta ao pool mesmo que nunca tenha aparecido. Pega itens que caem pela gravidade ou são lançados para fora da tela.")]
    public float farViewportMargin = 1.5f;

    Renderer[] renderers;
    bool hasBeenVisible;
    bool seenInViewport;
    bool pendingReturn;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

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

    // Plano B: depois de ter aparecido na câmera principal, devolve ao pool quando o objeto INTEIRO (todos os
    // renderers, não só o pivô) estiver fora da tela. E, de qualquer jeito, se ficar longe demais da tela.
    void CheckViewport()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 center = cam.WorldToViewportPoint(transform.position);
        if (center.x < -farViewportMargin || center.x > 1f + farViewportMargin || center.y < -farViewportMargin || center.y > 1f + farViewportMargin)
        {
            pendingReturn = true;
            return;
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;
                if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 vmin = center;
        Vector3 vmax = center;
        if (hasBounds)
        {
            vmin = cam.WorldToViewportPoint(bounds.min);
            vmax = cam.WorldToViewportPoint(bounds.max);
        }

        bool anyInside = vmax.x >= 0f && vmin.x <= 1f && vmax.y >= 0f && vmin.y <= 1f;
        if (anyInside)
        {
            seenInViewport = true;
            return;
        }

        bool fullyOutside = vmax.x < -offscreenSlack || vmin.x > 1f + offscreenSlack || vmax.y < -offscreenSlack || vmin.y > 1f + offscreenSlack;
        if (seenInViewport && fullyOutside) pendingReturn = true;
    }

    void OnDisable()
    {
        // Reseta para a próxima vez que este objeto for reaproveitado do pool.
        hasBeenVisible = false;
        seenInViewport = false;
        pendingReturn = false;
    }
}
