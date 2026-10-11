using UnityEngine;
using Spine;
using Spine.Unity;

// Fica no GameObject do Spine (o mesmo do SkeletonAnimation), filho do player.
// Cuida das animações de CORPO INTEIRO (track 0) ligadas à vida do player:
//  - tomar dano: toca FullBody_Hurt uma vez e volta pra animação que estava rodando antes;
//  - morrer: toca Fullbody_Death uma vez e fica parada na pose final.
public class PlayerBodyAnimations : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [SpineAnimation(dataField: "skeletonAnimation")]
    public string hurtAnimationName = "FullBody_Hurt";

    [SpineAnimation(dataField: "skeletonAnimation")]
    public string deathAnimationName = "Fullbody_Death";

    public int bodyTrack = 0;

    [Tooltip("Duração (segundos) da transição para as animações de dano/morte.")]
    public float mixDuration = 0.05f;

    healthSystem health;
    float lastHealth;
    bool isDead;
    string baseAnimationName; // a animação de corpo "normal" (idle/moto), pra voltar depois do dano

    void Start()
    {
        if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();

        health = GetComponentInParent<healthSystem>();
        if (health != null)
        {
            lastHealth = health.currentHealth;
            health.OnHealthChanged += HandleHealthChanged;
        }
        else
        {
            Debug.LogWarning("PlayerBodyAnimations: nenhum healthSystem encontrado nos pais — dano não vai tocar animação.");
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
    }

    void HandleHealthChanged(float current, float max)
    {
        bool tookDamage = current < lastHealth;
        lastHealth = current;

        // current <= 0 é morte: quem toca a animação é o PlayerDeath, via PlayDeath().
        if (tookDamage && current > 0f && !isDead)
        {
            PlayHurt();
        }
    }

    public void PlayHurt()
    {
        if (string.IsNullOrEmpty(hurtAnimationName)) return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        // Guarda qual animação de corpo estava tocando (a não ser que já seja o próprio dano),
        // pra voltar pra ela quando o dano terminar.
        TrackEntry current = state.GetCurrent(bodyTrack);
        if (current != null && current.Animation.Name != hurtAnimationName)
        {
            baseAnimationName = current.Animation.Name;
        }

        TrackEntry hurt = state.SetAnimation(bodyTrack, hurtAnimationName, false);
        hurt.MixDuration = mixDuration;

        if (!string.IsNullOrEmpty(baseAnimationName))
        {
            state.AddAnimation(bodyTrack, baseAnimationName, true, 0f);
        }
    }

    // Toca a morte (uma vez, fica na pose final) e devolve a duração da animação em segundos.
    public float PlayDeath()
    {
        isDead = true;
        if (string.IsNullOrEmpty(deathAnimationName)) return 0f;

        TrackEntry death = skeletonAnimation.AnimationState.SetAnimation(bodyTrack, deathAnimationName, false);
        death.MixDuration = mixDuration;

        Spine.Animation anim = skeletonAnimation.Skeleton.Data.FindAnimation(deathAnimationName);
        return anim != null ? anim.Duration : 0f;
    }
}
