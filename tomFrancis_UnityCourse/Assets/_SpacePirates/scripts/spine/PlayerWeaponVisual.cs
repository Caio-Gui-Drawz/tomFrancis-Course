using UnityEngine;
using Spine;
using Spine.Unity;

// Fica no GameObject do Spine (filho do player) — é a única parte do sistema de arma que
// sabe que Spine existe. O Player2D só conversa com isso através de métodos simples.
public class PlayerWeaponVisual : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [SpineBone(dataField: "skeletonAnimation")]
    [Tooltip("Bone na ponta do cano da arma, de onde a bala nasce.")]
    public string muzzleBoneName;

    [SpineAnimation(dataField: "skeletonAnimation")]
    [Tooltip("Animação de braço em loop (track abaixo), tocada enquanto está em modo de combate mas sem atirar.")]
    public string aimIdleAnimationName;

    [SpineAnimation(dataField: "skeletonAnimation")]
    [Tooltip("Animação de tiro (tocada na track abaixo).")]
    public string shootAnimationName;
    public int shootAnimationTrack = 1;

    [Tooltip("Duração (s) da transição PARA a animação de tiro. Curta, senão o tiro (~0,17s) nem aparece.")]
    public float shootMixDuration = 0.02f;

    [Tooltip("Velocidade da animação de tiro (1 = normal, 0.5 = metade da velocidade). Útil pra dar mais leitura a um tiro muito rápido.")]
    public float shootAnimationSpeed = 1f;

    [Tooltip("Limite de aceleração da animação de tiro em armas de cadência muito alta.")]
    public float maxShootAnimationSpeed = 4f;

    [Tooltip("Duração (s) da transição de volta pro idle de mira depois do tiro.")]
    public float returnMixDuration = 0.05f;

    Bone muzzleBone;

    void Start()
    {
        muzzleBone = skeletonAnimation.Skeleton.FindBone(muzzleBoneName);
        if (muzzleBone == null)
        {
            Debug.LogWarning("Muzzle bone '" + muzzleBoneName + "' não encontrado no esqueleto.");
        }
    }

    // Posição do cano da arma, já convertida para espaço de mundo da Unity.
    public Vector3 GetMuzzleWorldPosition()
    {
        if (muzzleBone == null) return skeletonAnimation.transform.position;

        Vector3 skeletonSpacePos = new Vector3(muzzleBone.WorldX, muzzleBone.WorldY, 0f);
        return skeletonAnimation.transform.TransformPoint(skeletonSpacePos);
    }

    // Ângulo (graus, espaço de mundo da Unity) pra onde o cano está REALMENTE apontando,
    // já depois da IK resolver (ou travar no limite de alcance do braço). Usar isso em vez
    // de recalcular a direção pelo mouse garante que a bala nunca "desalinha" do cano quando
    // a IK não consegue acompanhar o mouse até o fim (braço esticado no limite).
    public float GetMuzzleWorldAngle()
    {
        if (muzzleBone == null) return skeletonAnimation.transform.eulerAngles.z;

        // WorldRotationX = rotação (em graus) do eixo local X do bone, já em espaço do
        // esqueleto — assumindo que o bone foi desenhado com o X apontando ao longo do cano
        // (convenção padrão do Spine para bones "compridos" como esse).
        float boneAngleInSkeletonSpace = muzzleBone.WorldRotationX;

        // Soma a rotação do próprio GameObject do esqueleto, caso ele tenha alguma rotação
        // própria (hoje o player não gira mais o transform, mas isso deixa à prova de futuro).
        return boneAngleInSkeletonSpace + skeletonAnimation.transform.eulerAngles.z;
    }

    // Liga/desliga o "modo combate" do ponto de vista da animação: com a track 1 tocando o
    // loop de "pronto pra atirar", ou totalmente vazia (mãos abaixadas, só a animação
    // full-body da track 0 aparece). Não mexe na IK — isso é papel do AimArmToMouse.
    public void SetAiming(bool isAiming)
    {
        if (isAiming)
        {
            if (!string.IsNullOrEmpty(aimIdleAnimationName))
            {
                skeletonAnimation.AnimationState.SetAnimation(shootAnimationTrack, aimIdleAnimationName, true);
            }
        }
        else
        {
            skeletonAnimation.AnimationState.ClearTrack(shootAnimationTrack);
        }
    }

    // fireInterval = segundos até o próximo tiro dessa arma (WeaponData.fireRate). Se for mais curto
    // que a animação de tiro, a animação é ACELERADA pra caber inteira (senão cada tiro cortava a
    // anterior e só o começo dela aparecia, ex.: no SMG).
    public void PlayShootAnimation(float fireInterval = 0f)
    {
        if (string.IsNullOrEmpty(shootAnimationName)) return;

        Spine.AnimationState state = skeletonAnimation.AnimationState;

        // Força a mistura de entrada no tiro (em vez de usar a tabela de Mix Durations do asset,
        // onde AimPose -> Shot está em 1 segundo: o tiro dura só ~0,17s, então quase não aparecia).
        TrackEntry shot = state.SetAnimation(shootAnimationTrack, shootAnimationName, false);
        shot.MixDuration = shootMixDuration;

        Spine.Animation shotAnimation = skeletonAnimation.Skeleton.Data.FindAnimation(shootAnimationName);
        float shotDuration = shotAnimation != null ? shotAnimation.Duration : 0.2f;

        float speed = shootAnimationSpeed;
        if (fireInterval > 0f)
        {
            float speedToFit = shotDuration / fireInterval;
            speed = Mathf.Min(Mathf.Max(speed, speedToFit), maxShootAnimationSpeed);
        }
        shot.TimeScale = speed;

        // Delay explícito: a animação seguinte só entra perto do FIM do tiro. Com delay 0 o Spine
        // calcula "duração - mix" (que dá <= 0 aqui) e começava a sair do tiro imediatamente.
        float delayBeforeNext = Mathf.Max(0.01f, shotDuration / Mathf.Max(0.01f, speed) - returnMixDuration);

        if (!string.IsNullOrEmpty(aimIdleAnimationName))
        {
            // Depois que o tiro terminar, volta a tocar o loop de "pronto pra atirar".
            TrackEntry back = state.AddAnimation(shootAnimationTrack, aimIdleAnimationName, true, delayBeforeNext);
            back.MixDuration = returnMixDuration;
        }
        else
        {
            // Sem animação de idle configurada: solta a track suavemente em vez de travar no
            // último frame do tiro, devolvendo o controle desses bones pra track 0 (corpo).
            state.AddEmptyAnimation(shootAnimationTrack, returnMixDuration, delayBeforeNext);
        }
    }
}
