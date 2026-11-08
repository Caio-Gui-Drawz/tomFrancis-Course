using UnityEngine;

// Adicionado automaticamente pelo ParallaxLayerSpawner em cada instância que ele spawna.
public class ParallaxMover : MonoBehaviour
{
    float layerSpeed;
    Vector3 spinDegreesPerSecond;
    float spinFollowSpeed;

    public void Initialize(float speed)
    {
        Initialize(speed, Vector3.zero);
    }

    public void Initialize(float speed, Vector3 spin)
    {
        Initialize(speed, spin, 1f);
    }

    // spinFollowSpeed: 0 = giro sempre na velocidade própria; 1 = acompanha a velocidade do parallax (loot, aceleração).
    public void Initialize(float speed, Vector3 spin, float spinFollowSpeed)
    {
        layerSpeed = speed;
        spinDegreesPerSecond = spin;
        this.spinFollowSpeed = Mathf.Clamp01(spinFollowSpeed);
    }

    void Update()
    {
        float globalSpeed = ParallaxSpeedController.Instance != null
            ? ParallaxSpeedController.Instance.EffectiveSpeed
            : 1f;

        transform.position += Vector3.left * layerSpeed * globalSpeed * Time.deltaTime;

        // O giro acompanha a velocidade do parallax só na proporção configurada no spawner (0 = nunca).
        if (spinDegreesPerSecond != Vector3.zero)
        {
            float spinSpeed = Mathf.Lerp(1f, globalSpeed, spinFollowSpeed);
            transform.Rotate(spinDegreesPerSecond * spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
