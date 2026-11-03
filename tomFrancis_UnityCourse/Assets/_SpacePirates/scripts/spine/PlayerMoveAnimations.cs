using UnityEngine;
using Spine;
using Spine.Unity;

// Fica no GameObject do Spine (o mesmo do SkeletonAnimation e do PlayerBodyAnimations).
// Mistura 3 poses conforme o movimento vertical do player:
//   parado  -> FullBody_Idle (track 0, em loop)
//   subindo -> bike_up   (track própria, peso = quão rápido está subindo)
//   descendo-> bike_Down (track própria, peso = quão rápido está descendo)
// As poses de subir/descer são entradas de 1 frame com peso (Alpha) de 0 a 1 por cima do idle, então a
// transição é contínua: devagar = meia inclinação, velocidade máxima = pose completa.
public class PlayerMoveAnimations : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [Tooltip("De onde vem a velocidade vertical. Vazio = procura no pai (PlayerMovement2D).")]
    public PlayerMovement2D movement;

    [Header("Animações")]
    [SpineAnimation(dataField: "skeletonAnimation")]
    public string idleAnimationName = "FullBody_Idle";
    [SpineAnimation(dataField: "skeletonAnimation")]
    public string upAnimationName = "bike_up";
    [SpineAnimation(dataField: "skeletonAnimation")]
    public string downAnimationName = "bike_Down";

    [Header("Tracks")]
    [Tooltip("Track do corpo (idle). Deve ser a mesma do PlayerBodyAnimations.")]
    public int bodyTrack = 0;
    [Tooltip("Track da pose de subir. Precisa ser livre (o jogo usa 0, 1, 3 e 4).")]
    public int upTrack = 5;
    [Tooltip("Track da pose de descer. Precisa ser livre.")]
    public int downTrack = 6;

    [Header("Mistura")]
    [Tooltip("Quão rápido o peso das poses acompanha o movimento (peso por segundo). Maior = resposta mais seca.")]
    public float blendInSpeed = 8f;
    [Tooltip("Quão rápido as poses voltam ao idle quando para ou quando o jogo sai de Playing.")]
    public float blendOutSpeed = 6f;
    [Tooltip("Curva da resposta: 1 = linear; maior que 1 = só inclina de verdade perto da velocidade máxima.")]
    [Range(0.5f, 3f)] public float responseExponent = 1f;
    [Tooltip("Peso máximo das poses (1 = pose completa na velocidade máxima).")]
    [Range(0f, 1f)] public float maxPoseWeight = 1f;

    TrackEntry upEntry;
    TrackEntry downEntry;
    float upWeight;
    float downWeight;
    Rigidbody2D fallbackBody;

    void Awake()
    {
        if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
        if (movement == null) movement = GetComponentInParent<PlayerMovement2D>();
        if (movement == null) fallbackBody = GetComponentInParent<Rigidbody2D>();
    }

    void Update()
    {
        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null) return;

        EnsureEntries();

        float velocity = CurrentNormalizedVelocity();
        float upTarget = Mathf.Pow(Mathf.Clamp01(velocity), responseExponent) * maxPoseWeight;
        float downTarget = Mathf.Pow(Mathf.Clamp01(-velocity), responseExponent) * maxPoseWeight;

        upWeight = Approach(upWeight, upTarget);
        downWeight = Approach(downWeight, downTarget);

        if (upEntry != null) upEntry.Alpha = upWeight;
        if (downEntry != null) downEntry.Alpha = downWeight;
    }

    float Approach(float current, float target)
    {
        float speed = target > current ? blendInSpeed : blendOutSpeed;
        return Mathf.MoveTowards(current, target, speed * Time.deltaTime);
    }

    float CurrentNormalizedVelocity()
    {
        if (movement != null) return movement.NormalizedVelocityY;
        if (fallbackBody != null) return Mathf.Clamp(fallbackBody.linearVelocity.y / 14f, -1f, 1f);
        return 0f;
    }

    // Garante idle no corpo e as 2 poses nas tracks de mistura (recria se algo as removeu).
    void EnsureEntries()
    {
        Spine.AnimationState state = skeletonAnimation.AnimationState;
        SkeletonData data = skeletonAnimation.Skeleton.Data;

        // Só põe o idle se nada estiver tocando no corpo (não atropela hurt/morte).
        if (state.GetCurrent(bodyTrack) == null && !string.IsNullOrEmpty(idleAnimationName) && data.FindAnimation(idleAnimationName) != null)
        {
            state.SetAnimation(bodyTrack, idleAnimationName, true);
        }

        upEntry = EnsurePose(state, data, upTrack, upAnimationName, upEntry);
        downEntry = EnsurePose(state, data, downTrack, downAnimationName, downEntry);
    }

    TrackEntry EnsurePose(Spine.AnimationState state, SkeletonData data, int track, string animationName, TrackEntry entry)
    {
        if (string.IsNullOrEmpty(animationName) || data.FindAnimation(animationName) == null) return null;
        if (entry != null && state.GetCurrent(track) == entry) return entry;

        TrackEntry created = state.SetAnimation(track, animationName, true);
        created.Alpha = 0f;
        created.MixDuration = 0f;
        return created;
    }
}
