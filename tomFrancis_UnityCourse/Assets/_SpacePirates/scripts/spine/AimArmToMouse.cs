using UnityEngine;
using Spine;
using Spine.Unity;

public class AimArmToMouse : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [Tooltip("Nome exato do bone-ALVO da IK (confirme com o ListIkConstraints antes).")]
    public string ikTargetBoneName;

    Bone targetBone;

    void Start()
    {
        targetBone = skeletonAnimation.Skeleton.FindBone(ikTargetBoneName);
        if (targetBone == null)
        {
            Debug.LogWarning("Bone '" + ikTargetBoneName + "' não encontrado no esqueleto.");
        }

        skeletonAnimation.UpdateLocal += UpdateAim;
    }

    void OnDestroy()
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.UpdateLocal -= UpdateAim;
        }
    }

    void UpdateAim(ISkeletonAnimation anim)
    {
        if (targetBone == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        // Passo 1: converte de espaço de mundo da Unity para o espaço "de mundo" do esqueleto
        // (relativo à raiz do SkeletonAnimation).
        Vector3 skeletonSpacePos = skeletonAnimation.transform.InverseTransformPoint(mouseWorldPos);

        // Passo 2: converte do espaço do esqueleto para o espaço LOCAL do PAI do bone-alvo —
        // que é o sistema de referência que targetBone.X/Y realmente espera. Isso funciona
        // corretamente não importa quantos bones existam entre o alvo e a raiz.
        if (targetBone.Parent != null)
        {
            targetBone.Parent.WorldToLocal(skeletonSpacePos.x, skeletonSpacePos.y, out float localX, out float localY);
            targetBone.X = localX;
            targetBone.Y = localY;
        }
        else
        {
            // Bone-alvo é a própria raiz (sem pai) — espaço do esqueleto já é o espaço local.
            targetBone.X = skeletonSpacePos.x;
            targetBone.Y = skeletonSpacePos.y;
        }
    }
}

