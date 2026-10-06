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
    [Tooltip("Animação de tiro (tocada na track abaixo).")]
    public string shootAnimationName;
    public int shootAnimationTrack = 1;

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

    public void PlayShootAnimation()
    {
        if (string.IsNullOrEmpty(shootAnimationName)) return;
        skeletonAnimation.AnimationState.SetAnimation(shootAnimationTrack, shootAnimationName, false);
    }
}
