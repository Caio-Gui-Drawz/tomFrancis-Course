using UnityEngine;

// Adicionado automaticamente pelo ParallaxLayerSpawner em cada instância que ele spawna.
public class ParallaxMover : MonoBehaviour
{
    float layerSpeed;

    public void Initialize(float speed)
    {
        layerSpeed = speed;
    }

    void Update()
    {
        float globalSpeed = ParallaxSpeedController.Instance != null
            ? ParallaxSpeedController.Instance.CurrentGlobalSpeed
            : 1f;

        transform.position += Vector3.left * layerSpeed * globalSpeed * Time.deltaTime;
    }
}
