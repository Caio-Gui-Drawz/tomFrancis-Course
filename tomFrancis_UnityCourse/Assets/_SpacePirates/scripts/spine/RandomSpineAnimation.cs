using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

// Toda vez que o objeto é ativado (ex.: sai do pool), toca uma animação aleatória do Spine,
// sem repetir a última que foi escolhida. Coloque no GameObject do Spine (o que tem o SkeletonAnimation).
// As animações são descobertas sozinhas pelo prefixo do nome (ex.: "Vault_" pega Vault_1..Vault_4);
// se seu amigo exportar uma Vault_5, ela entra sem mexer em nada.
public class RandomSpineAnimation : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation; // vazio = pega do próprio objeto

    [Tooltip("Só entram no sorteio animações cujo nome começa com isso (ex.: \"Vault_\"). Vazio = todas as animações do esqueleto.")]
    public string animationNamePrefix = "Vault_";

    public int track = 0;
    public bool loop = true;

    [Tooltip("Em animações em loop, começa de um ponto aleatório do ciclo (varia ainda mais caixas lado a lado).")]
    public bool randomizeStartTime = true;

    [Tooltip("Ligado = a 'última animação' vale entre TODAS as caixas (duas caixas seguidas nunca saem iguais). Desligado = cada caixa só evita repetir a própria.")]
    public bool avoidRepeatAcrossInstances = true;

    // Última animação sorteada por esqueleto (compartilhada entre instâncias).
    static readonly Dictionary<int, string> lastBySkeleton = new Dictionary<int, string>();

    string lastOfThisInstance;
    List<string> candidates;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lastBySkeleton.Clear();
    }

    void Awake()
    {
        if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
    }

    void OnEnable()
    {
        if (skeletonAnimation == null) return;
        if (!skeletonAnimation.valid) skeletonAnimation.Initialize(false);
        if (!skeletonAnimation.valid) return;

        if (candidates == null) BuildCandidates();
        if (candidates.Count == 0) return;

        string chosen = PickAnimation();

        // Começa limpo: sem misturar com a pose da vez anterior em que esse objeto foi usado.
        skeletonAnimation.AnimationState.ClearTracks();
        skeletonAnimation.Skeleton.SetToSetupPose();

        Spine.TrackEntry entry = skeletonAnimation.AnimationState.SetAnimation(track, chosen, loop);
        if (loop && randomizeStartTime && entry != null && entry.Animation.Duration > 0f)
        {
            entry.TrackTime = Random.value * entry.Animation.Duration;
        }

        skeletonAnimation.Update(0f); // aplica a pose já neste frame
    }

    void BuildCandidates()
    {
        candidates = new List<string>();
        foreach (Spine.Animation animation in skeletonAnimation.Skeleton.Data.Animations)
        {
            if (string.IsNullOrEmpty(animationNamePrefix) || animation.Name.StartsWith(animationNamePrefix))
            {
                candidates.Add(animation.Name);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("RandomSpineAnimation: nenhuma animação começa com '" + animationNamePrefix + "' em " + name, this);
        }
    }

    string PickAnimation()
    {
        int key = skeletonAnimation.skeletonDataAsset.GetInstanceID();
        string last = null;
        if (avoidRepeatAcrossInstances) lastBySkeleton.TryGetValue(key, out last);
        else last = lastOfThisInstance;

        string chosen;
        if (candidates.Count == 1)
        {
            chosen = candidates[0];
        }
        else
        {
            // Sorteia entre as restantes (todas menos a última).
            List<string> options = new List<string>(candidates);
            options.Remove(last);
            chosen = options[Random.Range(0, options.Count)];
        }

        lastBySkeleton[key] = chosen;
        lastOfThisInstance = chosen;
        return chosen;
    }
}
