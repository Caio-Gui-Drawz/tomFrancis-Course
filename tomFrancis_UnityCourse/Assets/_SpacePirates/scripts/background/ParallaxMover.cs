using UnityEngine;

// Adicionado automaticamente pelo ParallaxLayerSpawner em cada instância que ele spawna.
public class ParallaxMover : MonoBehaviour
{
    float layerSpeed;
    Vector3 spinDegreesPerSecond;
    float spinFollowSpeed;

    // Devolve ao pool quando o prop sai INTEIRO pela esquerda da tela (sem isso eles andariam para sempre).
    const float ReturnCheckInterval = 0.25f;
    const float ReturnMargin = 1f;
    Renderer[] renderers;
    float returnCheckTimer;

    void OnEnable()
    {
        returnCheckTimer = ReturnCheckInterval;
    }

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

        returnCheckTimer -= Time.deltaTime;
        if (returnCheckTimer <= 0f)
        {
            returnCheckTimer = ReturnCheckInterval;
            if (IsFullyLeftOfScreen())
            {
                PoolManager.Instance.Return(gameObject);
            }
        }
    }

    bool IsFullyLeftOfScreen()
    {
        Camera cam = Camera.main;
        if (cam == null || PoolManager.Instance == null) return false;

        // Câmera ortográfica: o X da borda esquerda não depende da profundidade usada aqui.
        float leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 10f)).x;

        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled) continue;
            if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }

        // Sem renderer: usa só o pivô com uma folga grande.
        float rightMost = hasBounds ? bounds.max.x : transform.position.x + 20f;
        return rightMost < leftEdge - ReturnMargin;
    }
}
