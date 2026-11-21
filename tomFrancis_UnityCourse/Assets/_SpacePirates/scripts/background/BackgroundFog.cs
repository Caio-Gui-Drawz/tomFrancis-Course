using UnityEngine;

// "Fog" improvisada para os props do parallax. Coloque UM destes na cena (qualquer GameObject).
// Define a cor da fog e a intensidade global. Quanto cada camada é afetada fica no ParallaxLayerSpawner
// (campo "Fog Amount": 0 = cor original, 1 = totalmente na cor da fog).
// Dá pra mudar a cor/intensidade em runtime (ex.: por wave) via SetFogColor / SetIntensity ou DOTween.
public class BackgroundFog : MonoBehaviour
{
    public static BackgroundFog Instance { get; private set; }

    // Incrementa a cada mudança; os props se atualizam sozinhos quando percebem que mudou.
    public static int Version { get; private set; }

    [SerializeField] Color fogColor = new Color(0.04f, 0.05f, 0.14f, 1f);
    [Tooltip("Multiplicador global. 0 = fog desligada, 1 = usa o Fog Amount de cada camada como está.")]
    [SerializeField, Range(0f, 1f)] float intensity = 1f;

    [Header("O que a fog afeta")]
    [Tooltip("Cor de rim light (borda iluminada) também vai pra cor da fog.")]
    public bool tintRimLight = true;
    [Tooltip("Cor do contorno (outline) também vai pra cor da fog.")]
    public bool tintOutline = true;
    [Tooltip("Emissão (brilho próprio) some conforme a fog aumenta.")]
    public bool fadeEmission = true;

    public Color FogColor => fogColor;
    public float Intensity => intensity;

    void OnEnable() { Instance = this; Version++; }
    void OnDisable() { if (Instance == this) Instance = null; Version++; }
    void OnValidate() { Version++; }

    public void SetFogColor(Color c) { fogColor = c; Version++; }
    public void SetIntensity(float v) { intensity = Mathf.Clamp01(v); Version++; }
}
