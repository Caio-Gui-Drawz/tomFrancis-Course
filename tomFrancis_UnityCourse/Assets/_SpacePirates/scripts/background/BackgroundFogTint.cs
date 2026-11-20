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
            }
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
