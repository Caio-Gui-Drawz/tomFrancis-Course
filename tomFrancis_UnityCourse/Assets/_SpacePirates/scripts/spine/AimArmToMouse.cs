using UnityEngine;
using Spine;
using Spine.Unity;

public class AimArmToMouse : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [Tooltip("Nome exato do bone-ALVO da IK (confirme com o ListIkConstraints antes).")]
    public string ikTargetBoneName;

    [Tooltip("Nome exato da IK CONSTRAINT (pode ser igual ou diferente do bone-alvo — confirme com o ListIkConstraints).")]
    public string ikConstraintName;

    Bone targetBone;
    IkConstraint armIkConstraint;

    void Start()
    {
        targetBone = skeletonAnimation.Skeleton.FindBone(ikTargetBoneName);
        if (targetBone == null)
        {
            Debug.LogWarning("Bone '" + ikTargetBoneName + "' não encontrado no esqueleto.");
        }

        armIkConstraint = skeletonAnimation.Skeleton.FindIkConstraint(ikConstraintName);
        if (armIkConstraint == null)
        {
            Debug.LogWarning("IK constraint '" + ikConstraintName + "' não encontrada no esqueleto.");
        }
        else
        {
            // Começa desligada: o braço segue a pose da animação (mãos abaixadas), não o mouse.
            // SetAiming(true) é chamado de fora (Player2D) quando entra em modo de combate.
            armIkConstraint.Mix = 0f;
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

    // Liga/desliga a mira. Mix 0 = a IK não tem efeito nenhum (braço 100% controlado pela
    // animação tocando); Mix 1 = a IK controla o braço por completo, perseguindo o mouse.
    public void SetAiming(bool isAiming)
    {
        if (armIkConstraint != null)
        {
            armIkConstraint.Mix = isAiming ? 1f : 0f;
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
        // (Fazemos isso sempre, mesmo com Mix em 0 — é barato, e evita o bone "pular" pra
        // uma posição antiga quando a mira é reativada.)
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
