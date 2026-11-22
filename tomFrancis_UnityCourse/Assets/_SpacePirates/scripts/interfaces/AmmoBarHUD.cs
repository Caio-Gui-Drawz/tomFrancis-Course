using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Barra de munição feita de quadradinhos. A barra tem SEMPRE o mesmo tamanho (o do Container): quanto mais balas a
// arma tem, menores ficam os quadrados. Cada tiro escurece um quadrado. Armas sem munição limitada (pistola) escondem
// os quadrados e mostram o "Infinite Indicator" (ex.: um ícone de infinito).
//
// Como usar: crie um RectTransform no Canvas com o tamanho que a barra deve ter e coloque este script nele
// (ou arraste outro RectTransform em "Container"). O script não depende de onde a barra está: serve igual no HUD
// ou colado no cursor/crosshair depois.
public class AmmoBarHUD : MonoBehaviour
{
    [Tooltip("Vazio = procura o Player2D na cena sozinho (funciona com player criado em runtime).")]
    public Player2D player;

    [Tooltip("Área onde os quadradinhos são desenhados. Vazio = o próprio objeto. O tamanho dele é o tamanho da barra.")]
    public RectTransform container;

    [Tooltip("Opcional: um Image usado como modelo de cada quadradinho (sprite, borda...). Pode ser um prefab ou um objeto da cena (ele é escondido). Vazio = quadrado de cor sólida.")]
    public Image segmentPrefab;

    [Tooltip("Mostrado quando a arma tem munição infinita (ex.: ícone de infinito). Opcional.")]
    public GameObject infiniteIndicator;

    [Header("Cores")]
    public Color availableColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("Cor dos quadrados de balas já gastas (mais escura).")]
    public Color usedColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);

    public enum LayoutMode { Line, Arc }

    [Header("Layout")]
    [Tooltip("Line = fileira reta que ocupa a largura do Container. Arc = quadradinhos ao longo de um arco em volta do centro do Container (ex.: meia-lua embaixo do crosshair).")]
    public LayoutMode layoutMode = LayoutMode.Line;
    [Min(0f)] public float spacing = 3f;

    [Header("Arco (só no modo Arc)")]
    [Tooltip("Raio do arco em pixels do Canvas, a partir do centro do Container.")]
    [Min(1f)] public float arcRadius = 50f;
    [Tooltip("Direção do meio do arco em graus: -90 = embaixo (meia-lua embaixo), 90 = em cima, 0 = direita, 180 = esquerda.")]
    public float arcCenterAngle = -90f;
    [Tooltip("Abertura total do arco em graus. 120 = um terço de círculo; 180 = meia-lua.")]
    [Range(10f, 360f)] public float arcSpanDegrees = 120f;
    [Tooltip("Espaço entre quadrados, em graus.")]
    [Min(0f)] public float arcSpacingDegrees = 3f;
    [Tooltip("Espessura de cada quadrado (no sentido do raio), em pixels.")]
    [Min(1f)] public float arcThickness = 8f;
    [Tooltip("Ligado = as balas somem pela direita (os quadrados da esquerda ficam por último). Desligado = somem pela esquerda.")]
    public bool consumeFromRight = true;

    [Header("Feedback")]
    [Tooltip("Dá um pulinho no quadrado que acabou de ser gasto.")]
    public bool punchOnShot = true;
    [Min(0f)] public float punchStrength = 0.35f;
    [Min(0.01f)] public float punchDuration = 0.18f;

    readonly List<Image> segments = new List<Image>();
    Player2D bound;
    int count;      // quantos quadrados estão ativos agora (munição máxima da arma atual)
    int shown;      // munição restante que está desenhada
    Vector2 lastSize;

    void Awake()
    {
        if (container == null) container = transform as RectTransform;
        if (segmentPrefab != null && segmentPrefab.gameObject.scene.IsValid()) segmentPrefab.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        TryBind();
    }

    void OnDisable()
    {
        Unbind();
    }

    void Update()
    {
        if (bound == null) TryBind(); // player ainda não existe / foi recriado
    }

    void LateUpdate()
    {
        if (container == null || count == 0 || layoutMode == LayoutMode.Arc) return;
        Vector2 size = container.rect.size;
        if (size != lastSize) Layout();
    }

    void TryBind()
    {
        if (player == null) player = FindFirstObjectByType<Player2D>();
        if (player == null || bound == player) return;

        Unbind();
        bound = player;
        bound.OnWeaponChanged += HandleWeaponChanged;
        bound.OnAmmoChanged += HandleAmmoChanged;
        Refresh();
    }

    void Unbind()
    {
        if (bound != null)
        {
            bound.OnWeaponChanged -= HandleWeaponChanged;
            bound.OnAmmoChanged -= HandleAmmoChanged;
        }
        bound = null;
    }

    void HandleWeaponChanged(WeaponData weapon)
    {
        Refresh();
    }

    void HandleAmmoChanged(int remaining, int max)
    {
        if (max != count) { Refresh(); return; }
        ApplyRemaining(Mathf.Clamp(remaining, 0, count), true);
    }

    // Redesenha tudo para a arma atual (sem animação).
    void Refresh()
    {
        if (bound == null) return;

        bool limited = bound.HasLimitedAmmo && bound.AmmoMax > 0;
        if (infiniteIndicator != null) infiniteIndicator.SetActive(!limited && bound.currentWeapon != null);

        if (!limited)
        {
            SetSegmentCount(0);
            return;
        }

        SetSegmentCount(bound.AmmoMax);
        shown = count; // começa "cheio" para o ApplyRemaining não animar
        ApplyRemaining(Mathf.Clamp(bound.AmmoRemaining, 0, count), false);
    }

    void SetSegmentCount(int n)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].rectTransform.DOKill();
            segments[i].rectTransform.localScale = Vector3.one;
        }

        while (segments.Count < n) segments.Add(CreateSegment());

        for (int i = 0; i < segments.Count; i++) segments[i].gameObject.SetActive(i < n);

        count = n;
        Layout();
    }

    Image CreateSegment()
    {
        Image img;
        if (segmentPrefab != null)
        {
            img = Instantiate(segmentPrefab, container);
            img.gameObject.SetActive(true);
        }
        else
        {
            GameObject go = new GameObject("Segment", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(container, false);
            img = go.GetComponent<Image>();
        }

        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        return img;
    }

    // Distribui os quadrados na largura do container: largura de cada um = (total - espaços) / quantidade.
    void OnValidate()
    {
        if (Application.isPlaying && count > 0) Layout();
    }

    void Layout()
    {
        if (layoutMode == LayoutMode.Arc) LayoutArc();
        else LayoutLine();
    }

    // Quadrados ao longo de um arco: cada um é rotacionado na tangente. O índice 0 fica na ponta esquerda do arco.
    void LayoutArc()
    {
        if (container == null || count <= 0) return;
        lastSize = container.rect.size;

        float step = arcSpanDegrees / count;
        float gap = Mathf.Min(arcSpacingDegrees, step * 0.9f);
        float segmentAngle = step - gap;
        float segmentLength = Mathf.Max(0.5f, arcRadius * segmentAngle * Mathf.Deg2Rad);
        float firstAngle = arcCenterAngle - arcSpanDegrees * 0.5f + step * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = firstAngle + i * step;
            float rad = angle * Mathf.Deg2Rad;

            RectTransform rt = segments[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(segmentLength, arcThickness);
            rt.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * arcRadius;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle + 90f);
        }
    }

    void LayoutLine()
    {
        if (container == null) return;
        Rect rect = container.rect;
        lastSize = rect.size;
        if (count <= 0) return;

        float width = Mathf.Max(0.5f, (rect.width - spacing * (count - 1)) / count);
        float height = rect.height;

        for (int i = 0; i < count; i++)
        {
            RectTransform rt = segments[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(i * (width + spacing) + width * 0.5f, 0f); // âncora na borda esquerda do container
        }
    }

    bool IsAvailable(int index, int remaining)
    {
        return consumeFromRight ? index < remaining : index >= count - remaining;
    }

    void ApplyRemaining(int remaining, bool animate)
    {
        for (int i = 0; i < count; i++)
        {
            bool now = IsAvailable(i, remaining);
            bool before = IsAvailable(i, shown);
            segments[i].color = now ? availableColor : usedColor;

            if (animate && punchOnShot && before && !now)
            {
                RectTransform rt = segments[i].rectTransform;
                rt.DOKill(true);
                rt.DOPunchScale(Vector3.one * punchStrength, punchDuration, 6, 0.5f).SetUpdate(true);
            }
        }
        shown = remaining;
    }
}
