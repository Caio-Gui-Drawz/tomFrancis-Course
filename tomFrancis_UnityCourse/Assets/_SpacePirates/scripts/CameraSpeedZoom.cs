using Unity.Cinemachine;
using UnityEngine;

// Zoom out proporcional à velocidade vertical do player. Coloque na MESMA GameObject da CinemachineCamera.
// Sai rápido quando o player acelera e volta devagar quando ele para. Lê o PlayerMovement2D.NormalizedVelocityY.
[RequireComponent(typeof(CinemachineCamera))]
public class CameraSpeedZoom : MonoBehaviour
{
    [Tooltip("Vazio = procura sozinho na cena (funciona com player criado em runtime).")]
    public PlayerMovement2D movement;

    [Tooltip("Zoom out máximo, em fração do tamanho original. 0.05 = 5% maior na velocidade máxima.")]
    [Range(0f, 0.3f)] public float maxZoomOut = 0.05f;

    [Tooltip("Curva da resposta: 1 = proporcional; maior que 1 só zoom em velocidades altas; menor que 1 reage já em velocidades baixas.")]
    [Min(0.1f)] public float responseExponent = 1f;

    [Tooltip("Tempo (s) para ir ao zoom out. Menor = mais rápido.")]
    [Min(0.01f)] public float zoomOutTime = 0.25f;

    [Tooltip("Tempo (s) para voltar ao zoom normal. Maior = mais suave.")]
    [Min(0.01f)] public float zoomInTime = 0.6f;

    [Tooltip("0 = sem compensação (o player desliza na tela ao dar zoom). 1 = o player fica parado na tela durante o zoom.")]
    [Range(0f, 1f)] public float anchorStrength = 1f;

    CinemachineCamera cam;
    CinemachinePositionComposer composer;
    Vector3 baseOffset;
    float baseSize;
    float currentFactor;
    float factorVelocity;

    void OnEnable()
    {
        cam = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachinePositionComposer>();
        if (composer != null) baseOffset = composer.TargetOffset;
        baseSize = cam.Lens.OrthographicSize;
        currentFactor = 0f;
        factorVelocity = 0f;
    }

    void OnDisable()
    {
        if (cam == null) return;
        LensSettings lens = cam.Lens;
        lens.OrthographicSize = baseSize;
        cam.Lens = lens;
        if (composer != null) composer.TargetOffset = baseOffset;
    }

    void LateUpdate()
    {
        if (movement == null) movement = FindFirstObjectByType<PlayerMovement2D>();

        float target = 0f;
        if (movement != null)
        {
            target = Mathf.Pow(Mathf.Abs(movement.NormalizedVelocityY), responseExponent);
        }

        float smooth = target > currentFactor ? zoomOutTime : zoomInTime;
        currentFactor = Mathf.SmoothDamp(currentFactor, target, ref factorVelocity, smooth);

        LensSettings lens = cam.Lens;
        float ratio = 1f + maxZoomOut * currentFactor;
        lens.OrthographicSize = baseSize * ratio;
        cam.Lens = lens;

        // O zoom escala em volta do centro da câmera (o alvo). Deslocar o centro no X, no sentido contrário
        // ao do player, mantém o player no mesmo ponto da tela. O Y fica de fora de propósito.
        if (composer != null)
        {
            float offsetX = baseOffset.x;
            if (movement != null && cam.Follow != null)
            {
                float center = cam.Follow.position.x + baseOffset.x;
                offsetX += (movement.transform.position.x - center) * (1f - ratio) * anchorStrength;
            }
            composer.TargetOffset = new Vector3(offsetX, baseOffset.y, baseOffset.z);
        }
    }
}
