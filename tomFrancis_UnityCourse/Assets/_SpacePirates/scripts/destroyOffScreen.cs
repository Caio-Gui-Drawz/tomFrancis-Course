using UnityEngine;

public class DestroyOffScreen : MonoBehaviour
{
    bool hasBeenVisible;
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
        if (!pendingReturn) return;
        pendingReturn = false;
        PoolManager.Instance.Return(gameObject);
    }

    void OnDisable()
    {
        // Reseta para a próxima vez que este objeto for reaproveitado do pool.
        hasBeenVisible = false;
        pendingReturn = false;
    }
}