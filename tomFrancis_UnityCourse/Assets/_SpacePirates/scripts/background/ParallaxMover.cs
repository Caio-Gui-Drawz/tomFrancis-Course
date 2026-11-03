using UnityEngine;

// Adicionado automaticamente pelo ParallaxLayerSpawner em cada instância que ele spawna.
public class ParallaxMover : MonoBehaviour
{
    float layerSpeed;
    Vector3 spinDegreesPerSecond;

    public void Initialize(float speed)
    {
        Initialize(speed, Vector3.zero);
    }

    public void Initialize(float speed, Vector3 spin)
    {
        layerSpeed = speed;
        spinDegreesPerSecond = spin;
    }

    void Update()
    {
        float globalSpeed = ParallaxSpeedController.Instance != null
            ? ParallaxSpeedController.Instance.EffectiveSpeed
            : 1f;

        transform.position += Vector3.left * layerSpeed * globalSpeed * Time.deltaTime;

        // O giro acompanha a velocidade do parallax (desacelera junto na fase de loot).
        if (spinDegreesPerSecond != Vector3.zero)
        {
            transform.Rotate(spinDegreesPerSecond * globalSpeed * Time.deltaTime, Space.Self);
        }
    }
}
