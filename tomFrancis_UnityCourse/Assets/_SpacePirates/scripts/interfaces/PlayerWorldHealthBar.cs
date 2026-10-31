using Spine.Unity;
using UnityEngine;

// Barra de vida do player DENTRO DO MUNDO (não no Canvas), pensada para ser montada na CENA:
// o fundo (Background) e o preenchimento (Fill) são objetos filhos normais com SpriteRenderer,
// então você ajusta posição, tamanho, Sorting Layer e Order in Layer direto no Inspector (até em Play mode).
//
// Como montar:
//  1) Crie um objeto vazio FILHO do player2D e adicione este componente.
//  2) Clique nos três pontinhos do componente > "Criar Background e Fill" (cria os dois filhos já ligados).
//  3) Escolha o bone em "Follow Bone Name" e ajuste o Offset olhando o gizmo amarelo.
//  4) Mexa na escala/posição/ordem dos filhos Background e Fill à vontade.
// O script só: segue o bone, encolhe o Fill conforme a vida e troca a cor dele quando a vida está baixa.
public class PlayerWorldHealthBar : MonoBehaviour
{
    [Tooltip("Vazio = pega o healthSystem do player (pai deste objeto).")]
    public healthSystem playerHealth;

    [Tooltip("Vazio = procura o SkeletonAnimation dentro do player.")]
    public SkeletonAnimation skeletonAnimation;

    [Header("Objetos da barra (filhos)")]
    public SpriteRenderer background;
    [Tooltip("O Fill encolhe na horizontal com a vida, mantendo a borda ESQUERDA parada.")]
    public SpriteRenderer fill;

    [Header("Posição")]
    [SpineBone(dataField: "skeletonAnimation")]
    [Tooltip("Bone que a barra acompanha. Vazio = a barra fica parada onde este objeto está.")]
    public string followBoneName;

    [Tooltip("Deslocamento em unidades de mundo a partir do bone.")]
    public Vector2 offset;

    [Tooltip("Ligado = gira junto com o bone.")]
    public bool followBoneRotation = false;

    [Tooltip("Rotação extra, em graus.")]
    public float extraRotation = 0f;

    [Header("Cores do Fill")]
    public Color fillColor = new Color(0.2f, 1f, 0.2f, 1f);
    public Color lowHealthColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Range(0f, 1f)] public float lowHealthThreshold = 0.3f;

    [Header("Animação")]
    [Tooltip("Fração por segundo (2 = leva 0,5 s de vazia a cheia). Muito alto = instantâneo.")]
    public float fillSpeed = 2f;

    Spine.Bone bone;
    float targetFraction = 1f;
    float displayedFraction = 1f;

    // Valores do Fill com a vida cheia (lidos no Awake).
    float fullScaleX;
    float fullPosX;
    float spriteMinX;

    void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponentInParent<healthSystem>();
        if (skeletonAnimation == null) skeletonAnimation = transform.root.GetComponentInChildren<SkeletonAnimation>();

        if (fill != null)
        {
            fullScaleX = fill.transform.localScale.x;
            fullPosX = fill.transform.localPosition.x;
            spriteMinX = fill.sprite != null ? fill.sprite.bounds.min.x : -0.5f;
        }
        else
        {
            Debug.LogWarning("PlayerWorldHealthBar: campo Fill vazio. Use o menu do componente > 'Criar Background e Fill'.", this);
        }
    }

    void Start()
    {
        if (skeletonAnimation != null && !string.IsNullOrEmpty(followBoneName) && skeletonAnimation.Skeleton != null)
        {
            bone = skeletonAnimation.Skeleton.FindBone(followBoneName);
            if (bone == null) Debug.LogWarning("PlayerWorldHealthBar: bone '" + followBoneName + "' não encontrado.", this);
        }
    }

    void OnEnable()
    {
        if (playerHealth == null) return;

        playerHealth.OnHealthChanged += HandleHealthChanged;
        HandleHealthChanged(playerHealth.currentHealth, playerHealth.maxHealth);
        displayedFraction = targetFraction;
    }

    void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    void HandleHealthChanged(float current, float max)
    {
        targetFraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    void LateUpdate()
    {
        if (bone != null && skeletonAnimation != null)
        {
            transform.position = GetBoneTargetPosition();
            float angle = extraRotation;
            if (followBoneRotation) angle += bone.WorldRotationX + skeletonAnimation.transform.eulerAngles.z;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        displayedFraction = fillSpeed > 0f
            ? Mathf.MoveTowards(displayedFraction, targetFraction, fillSpeed * Time.deltaTime)
            : targetFraction;

        if (fill == null) return;

        // Encolhe em X mantendo a borda esquerda no lugar (funciona com qualquer pivô do sprite).
        float curScaleX = fullScaleX * displayedFraction;
        float leftEdge = fullPosX + fullScaleX * spriteMinX;
        Vector3 s = fill.transform.localScale;
        s.x = curScaleX;
        fill.transform.localScale = s;
        Vector3 p = fill.transform.localPosition;
        p.x = leftEdge - curScaleX * spriteMinX;
        fill.transform.localPosition = p;

        fill.color = displayedFraction <= lowHealthThreshold ? lowHealthColor : fillColor;
    }

    Vector3 GetBoneTargetPosition()
    {
        Vector3 local = new Vector3(bone.WorldX, bone.WorldY, 0f);
        Vector3 world = skeletonAnimation.transform.TransformPoint(local);
        world += (Vector3)offset;
        world.z = skeletonAnimation.transform.position.z;
        return world;
    }

#if UNITY_EDITOR
    [ContextMenu("Criar Background e Fill")]
    void CreateBarObjects()
    {
        Sprite sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite == null) { Debug.LogWarning("Sprite padrão não encontrado: atribua um sprite manualmente.", this); }

        // Escala para a barra ficar com ~2,6 x 0,8 unidades de mundo (ajuste depois à mão).
        Vector2 worldSize = new Vector2(2.6f, 0.8f);
        Vector3 inv = new Vector3(
            Mathf.Abs(transform.lossyScale.x) > 0.0001f ? 1f / transform.lossyScale.x : 1f,
            Mathf.Abs(transform.lossyScale.y) > 0.0001f ? 1f / transform.lossyScale.y : 1f, 1f);
        float spriteW = sprite != null ? sprite.bounds.size.x : 1f;
        float spriteH = sprite != null ? sprite.bounds.size.y : 1f;
        Vector3 scale = new Vector3(worldSize.x / spriteW * inv.x, worldSize.y / spriteH * inv.y, 1f);

        if (background == null)
        {
            background = MakeChild("Background", sprite, new Color(0f, 0f, 0f, 0.85f), 44, scale, 0f);
        }
        if (fill == null)
        {
            fill = MakeChild("Fill", sprite, fillColor, 45, scale, -0.001f);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }

    SpriteRenderer MakeChild(string childName, Sprite sprite, Color color, int order, Vector3 scale, float z)
    {
        GameObject go = new GameObject(childName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Criar barra");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.transform.localScale = scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // Retângulo amarelo no Scene mostrando o ponto onde a barra será ancorada.
    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        SkeletonAnimation anim = skeletonAnimation != null ? skeletonAnimation : transform.root.GetComponentInChildren<SkeletonAnimation>();
        if (anim != null && anim.valid && !string.IsNullOrEmpty(followBoneName))
        {
            Spine.Bone previewBone = anim.Skeleton.FindBone(followBoneName);
            if (previewBone != null)
            {
                center = anim.transform.TransformPoint(new Vector3(previewBone.WorldX, previewBone.WorldY, 0f)) + (Vector3)offset;
            }
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 0.1f);
    }
#endif
}
