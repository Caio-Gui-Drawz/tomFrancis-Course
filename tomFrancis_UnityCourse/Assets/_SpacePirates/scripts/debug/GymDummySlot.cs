using UnityEngine;

// CENA DE GYM: marca um "ponto de teste". Cria o prefab aqui, parado, e quando ele morre (ou some)
// cria outro no MESMO lugar depois de um tempo. Um slot por inimigo/tesouro de teste.
//
// Não usa o PoolManager de propósito: o healthSystem devolve o objeto ao pool quando ele morre, e um
// inimigo do pool poderia reaparecer em OUTRO slot. Aqui cada slot cria e é dono do seu inimigo.
public class GymDummySlot : MonoBehaviour
{
    [Tooltip("Prefab que fica neste ponto (o mesmo prefab da gameplay).")]
    public GameObject prefab;

    [Tooltip("Segundos depois que ele morre até aparecer de novo.")]
    public float respawnDelay = 1.5f;

    [Tooltip("Ligado = desliga todos os movimentos (IMovementBehavior) e trava o corpo: ele fica parado. Desligado = se move normal (útil pra testar um movimento).")]
    public bool frozen = true;

    GameObject instance;
    GameObject inactiveHolder;
    float respawnAt = -1f;

    void Start()
    {
        // Pai desativado: o inimigo nasce "dormindo", dá tempo de desligar os movimentos antes
        // de qualquer OnEnable rodar (senão o movimento já teria dado o impulso inicial).
        inactiveHolder = new GameObject("GymInactiveHolder");
        inactiveHolder.transform.SetParent(transform, false);
        inactiveHolder.SetActive(false);

        Spawn();
    }

    void Update()
    {
        if (instance != null && instance.activeInHierarchy)
        {
            respawnAt = -1f;
            return;
        }

        // Morreu (o healthSystem destrói), sumiu ou foi só desativado (ex.: bateu no player).
        if (instance != null) Destroy(instance);
        instance = null;

        if (respawnAt < 0f) respawnAt = Time.time + respawnDelay;
        if (Time.time >= respawnAt)
        {
            Spawn();
            respawnAt = -1f;
        }
    }

    void Spawn()
    {
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, inactiveHolder.transform);
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);

        if (frozen)
        {
            foreach (IMovementBehavior movement in go.GetComponentsInChildren<IMovementBehavior>(true))
            {
                Behaviour behaviour = movement as Behaviour;
                if (behaviour != null) behaviour.enabled = false;
            }

            // Dinâmico mas imóvel: continua recebendo tiro/colisão (gatilhos), só não sai do lugar.
            Rigidbody2D body = go.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }

        go.transform.SetParent(null, true); // sai do pai desativado: ativa agora, já parado
        instance = go;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, prefab != null ? prefab.name : "(sem prefab)");
    }
#endif
}
