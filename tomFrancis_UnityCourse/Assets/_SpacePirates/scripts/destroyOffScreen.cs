using UnityEngine;

public class DestroyOffScreen : MonoBehaviour
{
    bool hasBeenVisible;

    void OnBecameVisible()
    {
        hasBeenVisible = true;
    }

    void OnBecameInvisible()
    {
        if (hasBeenVisible)
        {
            Destroy(gameObject);
        }
    }
}