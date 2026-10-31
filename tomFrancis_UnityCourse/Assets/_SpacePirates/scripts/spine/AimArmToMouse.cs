using UnityEngine;
using Spine;
using Spine.Unity;

// Mira o braço esquerdo no mouse movendo o bone de CONTROLE de mira (IK_AIM_CTRL), que é o PAI
// do alvo da IK do braço (IK_ARM_L). Assim as animações de ArmOnly_AimPose / ArmOnly_Shot, que
// mexem no IK_ARM_L (relativo ao pai), continuam funcionando por cima da mira, sem briga.
public class AimArmToMouse : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;

    [Tooltip("Bone de controle de mira, pai do alvo da IK do braço. No rig atual: IK_AIM_CTRL.")]
    public string aimControlBoneName = "IK_AIM_CTRL";

    [Tooltip("Nome exato da IK CONSTRAINT do braço (a que liga/desliga a mira). No rig atual: IK_ARM_L.")]
    public string ikConstraintName;

    [Tooltip("Distância do alvo da IK até o ombro, como múltiplo do comprimento do braço. Acima de 1 = braço sempre esticado (igual ao rig no Spine), apontando direto pro mouse.")]
    public float reachMultiplier = 1.05f;

    [Tooltip("Se ligado, o braço dobra quando o mouse está perto do ombro (alvo à distância do mouse, limitada por Min Reach Fraction). Desligado = braço sempre esticado. A direção da bala NÃO depende disso.")]
    public bool bendWhenMouseIsClose = true;

    [Tooltip("Menor distância do alvo, como fração do comprimento do braço (0.75 = dobra no máximo um pouco). Menor = dobra mais.")]
    [Range(0.2f, 1f)]
    public float minReachFraction = 0.75f;

    [Tooltip("Velocidade da suavização ao esticar/dobrar o braço (maior = mais rápido).")]
    public float reachSmoothing = 18f;

    [Tooltip("Velocidade com que a mira entra/sai do modo travado (recarga). Maior = mais rápido, menor = mais suave.")]
    public float aimLockBlendSpeed = 12f;

    float currentReach = -1f;
    float lockedUntilTime;   // Time.time até quando a mira fica travada na pose do rig
    float lockWeight;        // 0 = segue o mouse, 1 = totalmente na pose do rig

    Bone aimControlBone;
    IkConstraint armIkConstraint;
    Bone shoulderBone;   // primeiro bone da cadeia da IK do braço: a "origem" da mira
    float armLength;     // soma dos comprimentos dos bones da cadeia
    Vector2 lastDirection = Vector2.right;

    // Ângulo (graus, mundo da Unity) do ombro até o mouse — a MESMA direção pra onde o braço
    // aponta. O Player2D usa isso pra a bala sair na direção em que o braço realmente está.
    public float AimAngleDegrees { get; private set; }
    public bool HasAim { get; private set; }

    void Start()
    {
        aimControlBone = FindBoneIgnoreCase(aimControlBoneName);
        if (aimControlBone == null)
        {
            Debug.LogWarning("Bone de controle '" + aimControlBoneName + "' não encontrado no esqueleto.");
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

            var chain = armIkConstraint.Bones;
            if (chain.Count > 0)
            {
                shoulderBone = chain.Items[0];
                for (int i = 0; i < chain.Count; i++) armLength += chain.Items[i].Data.Length;
            }
        }

        skeletonAnimation.UpdateLocal += UpdateAim;
    }

    // O Spine diferencia maiúsculas de minúsculas ("IK_Aim_CTRL" != "IK_AIM_CTRL"); aqui tolera.
    Bone FindBoneIgnoreCase(string boneName)
    {
        Bone exact = skeletonAnimation.Skeleton.FindBone(boneName);
        if (exact != null) return exact;

        foreach (Bone bone in skeletonAnimation.Skeleton.Bones)
        {
            if (string.Equals(bone.Data.Name, boneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return bone;
            }
        }
        return null;
    }

    void OnDestroy()
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.UpdateLocal -= UpdateAim;
        }
    }

    // Trava a mira por "seconds": o alvo do braço volta pra posição original do rig (a pose que as
    // animações ArmOnly_* foram desenhadas pra usar), em vez de seguir o mouse. Sai suavemente no fim.
    public void LockAimFor(float seconds)
    {
        lockedUntilTime = Mathf.Max(lockedUntilTime, Time.time + seconds);
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

    // Roda depois das animações serem aplicadas e ANTES da IK resolver — ponto certo pra
    // posicionar o bone de controle sem as animações sobrescreverem (nenhuma anima ele).
    void UpdateAim(ISkeletonAnimation anim)
    {
        if (aimControlBone == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        // Unity (mundo) -> espaço do esqueleto.
        Vector3 mouseSkeleton = skeletonAnimation.transform.InverseTransformPoint(mouseWorldPos);

        // Origem da mira = o ombro (posição da última atualização do esqueleto).
        float originX, originY;
        if (shoulderBone != null)
        {
            originX = shoulderBone.WorldX;
            originY = shoulderBone.WorldY;
        }
        else
        {
            originX = aimControlBone.Parent != null ? aimControlBone.Parent.WorldX : 0f;
            originY = aimControlBone.Parent != null ? aimControlBone.Parent.WorldY : 0f;
        }

        Vector2 direction = new Vector2(mouseSkeleton.x - originX, mouseSkeleton.y - originY);
        if (direction.sqrMagnitude > 0.0001f)
        {
            lastDirection = direction.normalized;
        }
        // (mouse em cima do ombro: mantém a última direção em vez de dar NaN)

        // Alvo SEMPRE à mesma distância do ombro (braço esticado) e na direção do mouse: o braço
        // aponta pro mouse mesmo com o mouse colado no boneco, sem dobrar e sem inverter o tiro.
        float fullReach = armLength > 0.01f ? armLength * reachMultiplier : 500f;
        float desiredReach = fullReach;
        if (bendWhenMouseIsClose && armLength > 0.01f)
        {
            // Alvo na distância do mouse (assim o braço dobra quando ele está perto), mas nunca
            // menor que o mínimo (evita dobrar demais / tremer) nem maior que o braço esticado.
            float mouseDistance = direction.magnitude;
            desiredReach = Mathf.Clamp(mouseDistance, armLength * minReachFraction, fullReach);
        }

        // Suaviza pra o braço dobrar/esticar sem "pular" quando o mouse cruza o limite.
        if (currentReach < 0f) currentReach = desiredReach;
        currentReach = Mathf.Lerp(currentReach, desiredReach, 1f - Mathf.Exp(-reachSmoothing * Time.deltaTime));
        float reach = currentReach;
        float targetX = originX + lastDirection.x * reach;
        float targetY = originY + lastDirection.y * reach;

        // Mira travada (ex.: recarga): mistura o alvo do mouse com a posição original do bone no rig.
        float lockTarget = Time.time < lockedUntilTime ? 1f : 0f;
        lockWeight = Mathf.MoveTowards(lockWeight, lockTarget, aimLockBlendSpeed * Time.deltaTime);

        if (aimControlBone.Parent != null)
        {
            aimControlBone.Parent.WorldToLocal(targetX, targetY, out float localX, out float localY);
            aimControlBone.X = Mathf.Lerp(localX, aimControlBone.Data.X, lockWeight);
            aimControlBone.Y = Mathf.Lerp(localY, aimControlBone.Data.Y, lockWeight);
        }
        else
        {
            aimControlBone.X = Mathf.Lerp(targetX, aimControlBone.Data.X, lockWeight);
            aimControlBone.Y = Mathf.Lerp(targetY, aimControlBone.Data.Y, lockWeight);
        }

        // Ângulo no mundo da Unity: origem do ombro -> mouse.
        Vector3 originWorld = skeletonAnimation.transform.TransformPoint(new Vector3(originX, originY, 0f));
        Vector2 worldDir = mouseWorldPos - originWorld;
        if (worldDir.sqrMagnitude > 0.0001f)
        {
            AimAngleDegrees = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg;
            HasAim = true;
        }
    }
}
