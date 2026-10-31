using UnityEngine;

// Gira o VISUAL do objeto para acompanhar a direção em que ele está se movendo (a "barriga" do arco:
// sobe inclinado, passa reto no topo, desce inclinado). Funciona com Rigidbody2D (usa a velocidade)
// ou com movimento por transform, como o ParabolicMovement (usa o deslocamento entre frames).
//
// Estrutura recomendada no prefab:
//   inimigo (Rigidbody2D, collider, movimento, este script)
//     └─ VisualPivot  <- arraste aqui em "Visual" (é ele que gira)
//          └─ Spine   <- ShakeFeedback pode usar este como Target sem brigar com a rotação
public class FaceVelocity : MonoBehaviour
{
    [Tooltip("Quem gira. Vazio = este objeto. Prefira um filho (pivô do visual), e não o root com o Rigidbody.")]
    public Transform visual;

    [Tooltip("Ângulo extra, em graus, pra compensar pra onde o desenho olha. 0 = o desenho olha pra direita; 180 = olha pra esquerda.")]
    public float angleOffset = 180f;

    [Tooltip("Velocidade máxima de giro, em graus por segundo. 0 = acompanha instantaneamente. Valor alto = acompanha rápido mas sem tranco.")]
    public float turnSpeed = 360f;

    [Tooltip("Abaixo dessa velocidade o objeto para de girar (evita tremer quando está quase parado).")]
    public float minSpeed = 0.1f;

    Rigidbody2D body;
    Vector3 lastPosition;
    bool snapNextFrame;

    void OnEnable()
    {
        body = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;
        snapNextFrame = true; // objetos do pool: o primeiro frame já nasce virado pro lado certo
    }

    void LateUpdate()
    {
        Transform target = visual != null ? visual : transform;

        Vector2 velocity = Vector2.zero;
        if (body != null) velocity = body.linearVelocity;

        // Sem Rigidbody (ou ele parado, ex.: movimento feito por transform): usa o deslocamento real.
        if (velocity.sqrMagnitude < 0.0001f && Time.deltaTime > 0f)
        {
            velocity = (transform.position - lastPosition) / Time.deltaTime;
        }
        lastPosition = transform.position;

        if (velocity.sqrMagnitude < minSpeed * minSpeed) return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + angleOffset;
        Quaternion desired = Quaternion.Euler(0f, 0f, angle);

        if (snapNextFrame || turnSpeed <= 0f)
        {
            target.rotation = desired;
            snapNextFrame = false;
        }
        else
        {
            target.rotation = Quaternion.RotateTowards(target.rotation, desired, turnSpeed * Time.deltaTime);
        }
    }
}
