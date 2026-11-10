using UnityEngine;

// Quando este inimigo MORRE (vida chegou a zero), faz nascer outros inimigos no lugar dele.
// Ex.: a Jamanta explode em 2 a 3 peixes. Vá em cada linha da lista "Spawns" e escolha o prefab e quantos nascem.
//
// Coloque no prefab do inimigo (precisa do healthSystem). Não dispara se o inimigo só sai da tela ou bate no
// player (isso não é "morrer"). Os filhos entram no pool como qualquer inimigo, então a wave só termina depois
// que eles também sumirem (o WaveManager espera todos os inimigos ativos).
[RequireComponent(typeof(healthSystem))]
public class SpawnOnDeath : MonoBehaviour
{
    [System.Serializable]
    public class Spawn
    {
        [Tooltip("Inimigo que nasce (o prefab que o pool conhece, o mesmo usado nos Spawners).")]
        public GameObject prefab;
        [Tooltip("Quantidade mínima que nasce.")]
        [Min(0)] public int minCount = 2;
        [Tooltip("Quantidade máxima. A cada morte sorteia um número entre mínimo e máximo (iguais = sempre a mesma quantidade).")]
        [Min(0)] public int maxCount = 3;
    }

    [Tooltip("O que nasce. Cada linha tem o seu prefab e a sua faixa de quantidade, então dá para misturar (ex.: 2 a 3 peixes + 1 mina).")]
    public Spawn[] spawns;

    [Header("Posição e direção")]
    [Tooltip("Distância (unidades de mundo) do ponto onde o inimigo morreu em que os filhos nascem. 0 = todos exatamente no mesmo ponto.")]
    [Min(0f)] public float spawnRadius = 0.8f;
    [Tooltip("Abertura do leque, em graus, voltado para o ALVO (o player; se não achar, para onde o inimigo estava indo). 90 = leque de 45° para cada lado. 360 = em volta inteira, como uma explosão completa.")]
    [Range(0f, 360f)] public float coneAngle = 90f;
    [Tooltip("Ligado = espalha os filhos igualmente dentro do leque. Desligado = ângulo aleatório dentro do leque.")]
    public bool spreadEvenly = true;

    [Header("Explosão")]
    [Tooltip("Velocidade EXTRA inicial (unidades/segundo), somada ao movimento normal do filho (perseguir o player etc.), na direção do seu próprio ângulo. Ela vai sumindo pela curva, e o filho nunca para: só volta a ter a velocidade dele. 0 = sem explosão.")]
    [Min(0f)] public float burstForce = 12f;
    [Tooltip("Em quantos segundos a velocidade extra some por completo. Mais tempo = a desaceleração fica mais longa e menos perceptível.")]
    [Min(0f)] public float burstSeconds = 0.5f;
    [Tooltip("Velocidade extra ao longo da explosão (eixo X: 0 = início, 1 = fim; Y: fração da velocidade inicial). Padrão: sai forte e vai sumindo devagar, como um atrito. Para uma saída seca, deixe a curva cair rápido no começo; para sumir mais devagar, deixe mais reta.")]
    public AnimationCurve burstCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -1.6f), new Keyframe(1f, 0f, 0f, 0f));

    [Header("Regras")]
    [Tooltip("Ligado = só faz nascer enquanto o jogo está em Playing (não gera inimigos na tela de game over ou no menu).")]
    public bool onlyWhilePlaying = true;

    healthSystem health;

    void Awake()
    {
        health = GetComponent<healthSystem>();
    }

    void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        health.OnDeath -= HandleDeath;
    }

    // Ângulo (graus) do "para frente" do leque: em direção ao player; senão a velocidade do inimigo; senão para a esquerda.
    float GetForwardAngle(Vector3 from)
    {
        Vector2 forward = Vector2.left;

        if (references.thePlayer != null)
        {
            Vector2 toPlayer = references.thePlayer.transform.position - from;
            if (toPlayer.sqrMagnitude > 0.0001f) forward = toPlayer;
        }
        else
        {
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f) forward = body.linearVelocity;
        }

        return Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
    }

    // Mostra o raio de nascimento no Scene view quando o inimigo está selecionado.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }

    void HandleDeath()
    {
        if (onlyWhilePlaying && GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;
        SpawnChildren();
    }

    [ContextMenu("Testar: spawnar filhos agora (Play mode)")]
    void SpawnChildren()
    {
        if (spawns == null || PoolManager.Instance == null) return;

        // Sorteia a quantidade de cada linha primeiro, para espalhar o total em volta do círculo.
        int[] counts = new int[spawns.Length];
        int total = 0;
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i] == null || spawns[i].prefab == null) continue;
            int min = Mathf.Max(0, spawns[i].minCount);
            int max = Mathf.Max(min, spawns[i].maxCount);
            counts[i] = Random.Range(min, max + 1);
            total += counts[i];
        }
        if (total == 0) return;

        Vector3 center = transform.position;
        float forwardAngle = GetForwardAngle(center);
        float startAngle = Random.Range(0f, 360f);
        bool fullCircle = coneAngle >= 359.9f;
        float half = coneAngle * 0.5f;
        int index = 0;

        for (int i = 0; i < spawns.Length; i++)
        {
            for (int c = 0; c < counts[i]; c++)
            {
                // Cada filho tem o seu ângulo: é a posição de nascimento E a direção da explosão.
                float angle;
                if (fullCircle)
                {
                    angle = spreadEvenly ? startAngle + 360f / total * index + Random.Range(-15f, 15f) : Random.Range(0f, 360f);
                }
                else if (spreadEvenly)
                {
                    float share = total > 1 ? (index + 0.5f) / total : 0.5f;
                    float jitter = total > 1 ? coneAngle / total * 0.25f : 0f;
                    angle = forwardAngle + Mathf.Lerp(-half, half, share) + Random.Range(-jitter, jitter);
                }
                else
                {
                    angle = forwardAngle + Random.Range(-half, half);
                }

                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                float radius = spreadEvenly ? spawnRadius * Random.Range(0.6f, 1f) : spawnRadius * Mathf.Sqrt(Random.value);
                Vector2 offset = direction * radius;
                index++;

                GameObject prefab = spawns[i].prefab;
                GameObject child = PoolManager.Instance.Get(prefab, center + (Vector3)offset, prefab.transform.rotation);
                if (child == null) continue;

                if (burstForce > 0f && burstSeconds > 0f)
                {
                    ExplosionBurst.Launch(child, direction, burstForce, burstSeconds, burstCurve);
                }
            }
        }
    }
}
