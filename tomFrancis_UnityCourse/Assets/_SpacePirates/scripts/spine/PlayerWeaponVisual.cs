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

    public void PlayShootAnimation()
    {
        if (string.IsNullOrEmpty(shootAnimationName)) return;
        skeletonAnimation.AnimationState.SetAnimation(shootAnimationTrack, shootAnimationName, false);
    }
}
