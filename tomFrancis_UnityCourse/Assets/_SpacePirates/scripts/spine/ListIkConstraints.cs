using UnityEngine;
using Spine;
using Spine.Unity;

// DIAGNÓSTICO TEMPORÁRIO: lista no Console todas as IK constraints do esqueleto,
// junto com o nome do bone-alvo de cada uma. Remova depois de descobrir o nome certo.
public class ListIkConstraints : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    void Start()
    {
        Skeleton skeleton = skeletonAnimation.Skeleton;

        Debug.Log("=== IK Constraints encontradas no esqueleto ===");
        foreach (IkConstraint ik in skeleton.IkConstraints)
        {
            string affectedBones = "";
            foreach (Bone b in ik.Bones)
            {
                affectedBones += b.Data.Name + ", ";
            }

            Debug.Log("IK: '" + ik.Data.Name + "' | Bones afetados: " + affectedBones + " | Bone-ALVO: '" + ik.Target.Data.Name + "'");
        }

        if (skeleton.IkConstraints.Count == 0)
        {
            Debug.Log("Nenhuma IK constraint encontrada neste esqueleto.");
        }
    }
}
