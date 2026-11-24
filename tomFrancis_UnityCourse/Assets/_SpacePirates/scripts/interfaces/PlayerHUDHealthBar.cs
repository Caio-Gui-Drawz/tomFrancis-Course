using UnityEngine;
using UnityEngine.UI;

// Barra de vida do HUD com "rastro atrasado": a barra principal desce na hora; o rastro (uma segunda barra por trás)
// espera um instante e desce devagar até alcançar. Ao curar, o rastro acompanha na hora.
//
// Montagem (dentro da máscara, de cima para baixo na hierarquia = de trás para frente):
//   Barra_MASK (Image + Mask)
//     ├ Fundo      (Image estática, opcional)
//     ├ Rastro     (Image Filled Horizontal, cor clara/vermelha)  -> campo Trail Image
//     └ BarraVerde (Image Filled Horizontal)                      -> campo Fill Image
// Coloque este componente na BarraVerde (ou em qualquer objeto sempre ativo do HUD).
public class PlayerHUDHealthBar : MonoBehaviour
{
    public healthSystem playerHealth; // arraste aqui o healthSystem do player (vazio = tenta achar o Player2D sozinho)
    public Image fillImage;           // barra principal (Image Type = Filled, Horizontal)

    [Header("Rastro (opcional)")]
    [Tooltip("Image Filled Horizontal por TRÁS da barra principal. Vazio = sem rastro.")]
    public Image trailImage;
    [Tooltip("Espera (s) depois de levar dano antes de o rastro começar a descer.")]
    [Min(0f)] public float trailDelaySeconds = 0.4f;
    [Tooltip("Velocidade do rastro descendo, em 'barra inteira' por segundo (0.5 = esvazia a barra toda em 2s).")]
    [Min(0.01f)] public float trailSpeed = 0.5f;

    [Header("Calibração (se a arte tiver margem transparente)")]
    [Tooltip("Valor de Fill Amount em que a barra aparece VAZIA. Normalmente 0.")]
    [Range(0f, 1f)] public float fillAtEmpty = 0f;
    [Tooltip("Valor de Fill Amount em que a barra aparece CHEIA. Se a barra já parece cheia em 0.6, coloque 0.6.")]
    [Range(0f, 1f)] public float fillAtFull = 1f;

    [Header("Debug")]
    public bool log = true;

    float targetFill = 1f;
    float trailFill = 1f;
    float trailTimer;
    bool bound;

    void OnEnable()
    {
        Bind();
    }

    void Start()
    {
        // Se não foi arrastado, tenta achar o healthSystem do player.
        if (playerHealth == null)
        {
            // O HUD fica num prefab, que não consegue guardar uma referência para o player da cena: acha sozinho.
            PlayerDeath death = FindFirstObjectByType<PlayerDeath>();
            if (death != null) playerHealth = death.GetComponent<healthSystem>();
            if (playerHealth == null)
            {
                Player2D p = FindFirstObjectByType<Player2D>();
                if (p != null) playerHealth = p.GetComponentInParent<healthSystem>();
            }
            Bind();
        }

        if (playerHealth == null)
            Debug.LogWarning("PlayerHUDHealthBar: sem healthSystem do player (arraste em Player Health). A barra não vai mudar.", this);
        if (fillImage == null)
            Debug.LogWarning("PlayerHUDHealthBar: arraste a barra verde em Fill Image.", this);

        CheckFit(fillImage, "Fill Image");
        CheckFit(trailImage, "Trail Image");
    }

    // A barra "Filled" corta pelo retângulo da PRÓPRIA imagem. Se ela for maior ou deslocada em relação à máscara (o pai),
    // a vida real não bate com o que aparece. Avisa quando os retângulos não coincidem.
    void CheckFit(Image image, string label)
    {
        if (image == null) return;
        RectTransform parent = image.rectTransform.parent as RectTransform;
        if (parent == null) return;

        Vector3[] a = new Vector3[4];
        Vector3[] b = new Vector3[4];
        image.rectTransform.GetWorldCorners(a);
        parent.GetWorldCorners(b);

        float imageWidth = a[2].x - a[0].x;
        float parentWidth = b[2].x - b[0].x;
        if (parentWidth <= 0.0001f) return;

        float ratio = imageWidth / parentWidth;
        float leftOffset = Mathf.Abs(a[0].x - b[0].x) / parentWidth;
        if (ratio < 0.95f || ratio > 1.05f || leftOffset > 0.05f)
        {
            Debug.LogWarning("PlayerHUDHealthBar: '" + image.name + "' (" + label + ") não cobre a máscara: largura = " + (ratio * 100f).ToString("0") +
                             "% da largura do pai, deslocada " + (leftOffset * 100f).ToString("0") + "%. Por isso a barra não acompanha a vida real. " +
                             "Ajuste o Rect Transform dela para stretch total (âncoras 0 a 1 e offsets 0).", image);
        }
    }

    void OnDisable()
    {
        if (playerHealth != null && bound) playerHealth.OnHealthChanged -= HandleHealthChanged;
        bound = false;
    }

    void Bind()
    {
        if (playerHealth == null || bound) return;
        playerHealth.OnHealthChanged += HandleHealthChanged;
        bound = true;
        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth); // estado inicial
        if (log) Debug.Log("[HPBar] ligada ao healthSystem '" + playerHealth.name + "' (" + playerHealth.currentHealth + "/" + playerHealth.maxHealth + ")", this);
    }

    void HandleHealthChanged(float current, float max)
    {
        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (log) Debug.Log("[HPBar] vida " + current + "/" + max + " -> fill " + fraction.ToString("0.00") + " (Image: " + Map(fraction).ToString("0.00") + ")", this);
        bool tookDamage = fraction < targetFill;
        targetFill = fraction;

        if (fillImage != null) fillImage.fillAmount = Map(fraction);

        if (trailImage != null)
        {
            if (tookDamage) trailTimer = trailDelaySeconds;   // dano: rastro espera e depois desce
            else trailFill = fraction;                         // cura / reset: rastro acompanha na hora
            if (!tookDamage) trailImage.fillAmount = Map(trailFill);
        }
    }

    void Update()
    {
        if (trailImage == null) return;

        if (trailFill > targetFill)
        {
            if (trailTimer > 0f) trailTimer -= Time.deltaTime;
            else trailFill = Mathf.MoveTowards(trailFill, targetFill, trailSpeed * Time.deltaTime);
        }
        else trailFill = targetFill;

        trailImage.fillAmount = Map(trailFill);
    }

    // Converte a fração de vida (0 a 1) no Fill Amount real da Image, usando a calibração.
    float Map(float fraction)
    {
        return Mathf.Lerp(fillAtEmpty, fillAtFull, fraction);
    }
}
