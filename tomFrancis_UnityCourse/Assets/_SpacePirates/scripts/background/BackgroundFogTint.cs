using UnityEngine;

// Colocado automaticamente pelo ParallaxLayerSpawner em cada prop. Empurra as cores do material toon
// para a cor da BackgroundFog usando MaterialPropertyBlock (não cria materiais novos e funciona com pool).
public class BackgroundFogTint : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int Shade1Id = Shader.PropertyToID("_1st_ShadeColor");
    static readonly int Shade2Id = Shader.PropertyToID("_2nd_ShadeColor");
    static readonly int RimId = Shader.PropertyToID("_RimLightColor");
    static readonly int OutlineId = Shader.PropertyToID("_Outline_Color");
    static readonly int EmissiveId = Shader.PropertyToID("_Emissive_Color");

    static MaterialPropertyBlock block;
    static bool warnedNoFog, warnedNoProps;

    Renderer[] renderers;
    Material[][] materials;
    float amount;
    int appliedVersion = -1;

    public void SetAmount(float value)
    {
        amount = Mathf.Clamp01(value);
        Apply();
    }

    void LateUpdate()
    {
        if (appliedVersion != BackgroundFog.Version) Apply();
    }

    void Cache()
    {
        if (renderers != null) return;
        renderers = GetComponentsInChildren<Renderer>(true);
        materials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].sharedMaterials;
    }

    void Apply()
    {
        Cache();
        appliedVersion = BackgroundFog.Version;
        if (block == null) block = new MaterialPropertyBlock();

        BackgroundFog fog = BackgroundFog.Instance;
        float a = fog != null ? Mathf.Clamp01(amount * fog.Intensity) : 0f;

        if (fog == null && !warnedNoFog)
        {
            warnedNoFog = true;
            Debug.LogWarning("[Fog] Nenhum BackgroundFog ativo na cena: os props ficam com a cor original. Adicione o componente BackgroundFog em um GameObject.", this);
        }
        else if (fog != null && amount <= 0f && !warnedNoFog)
        {
            warnedNoFog = true;
            Debug.LogWarning("[Fog] Este prop ('" + name + "') nasceu com Fog Amount = 0 no ParallaxLayerSpawner da camada dele, então nenhuma cor muda. Aumente o Fog Amount (ex.: 0.5).", this);
        }

        bool applied = false;

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer rend = renderers[r];
            if (rend == null) continue;
            Material[] mats = materials[r];
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                if (a <= 0f) { rend.SetPropertyBlock(null, i); continue; }

                block.Clear();
                bool any = false;
                Color fc = fog.FogColor;
                any |= Tint(m, BaseColorId, fc, a);
                any |= Tint(m, ColorId, fc, a);
                any |= Tint(m, Shade1Id, fc, a);
                any |= Tint(m, Shade2Id, fc, a);
                if (fog.tintRimLight) any |= Tint(m, RimId, fc, a);
                if (fog.tintOutline) any |= Tint(m, OutlineId, fc, a);
                if (fog.fadeEmission) any |= Tint(m, EmissiveId, Color.black, a);

                rend.SetPropertyBlock(any ? block : null, i);
                applied |= any;
            }
        }

        if (a > 0f && !applied && !warnedNoProps)
        {
            warnedNoProps = true;
            Debug.LogWarning("[Fog] Nenhum material de '" + name + "' tem as propriedades de cor do toon shader (_BaseColor/_1st_ShadeColor...). Confira o shader do material.", this);
        }
    }

    static bool Tint(Material m, int id, Color target, float a)
    {
        if (!m.HasProperty(id)) return false;
        Color original = m.GetColor(id);
        Color result = Color.Lerp(original, target, a);
        result.a = original.a;
        block.SetColor(id, result);
        return true;
    }
}
