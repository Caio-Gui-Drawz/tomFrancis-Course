using System.Collections.Generic;
using UnityEngine;

// Um ParallaxLayerSpawner por camada de profundidade (nuvens, montanhas próximas, distantes...).
// Coloque na cena, configure os valores no Inspector — adicionar uma camada nova é duplicar
// este objeto e ajustar os números, sem precisar de código novo.
public class ParallaxLayerSpawner : MonoBehaviour
{
    public GameObject[] spritePrefabs; // variantes desta camada (ex: nuvem 1, nuvem 2, nuvem 3)

    public float layerSpeed = 1f; // velocidade relativa desta camada (multiplicada pela global depois)
    [Range(0f, 0.5f)] public float speedJitter = 0.1f; // variação individual por instância (± essa fração)

    public float minSecondsBetweenSpawns = 1f;
    public float maxSecondsBetweenSpawns = 3f;

    [Tooltip("Folga além da borda direita da tela. O objeto só nasce depois de ficar TODO fora da tela (considerando o tamanho e a rotação dele), então ninguém aparece 'do nada' no canto.")]
    [Min(0f)] public float spawnMargin = 0.5f;

    public float minY = -2f;
    public float maxY = 2f;

    // Variação em torno da escala do PRÓPRIO PREFAB (1 = tamanho do prefab). Ex.: 0.8–1.2 = de 80% a 120% do que
    // você ajustou no prefab/variant. A escala do prefab não é mais sobrescrita.
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Ordem de desenho (atrás de tudo que é interagível)")]
    [Tooltip("Sorting Layer desta camada de parallax. Use uma layer ABAIXO da que o gameplay usa (ex.: 'Parallax' entre 'Background' e 'Default').")]
    [SortingLayerSelect] public int sortingLayerId;
    [Tooltip("Dentro da Sorting Layer: número MAIOR = desenhado na frente. Camada distante = menor, camada próxima = maior.")]
    public int sortingOrder = 0;
    [Tooltip("Profundidade (eixo Z) da camada. Maior = mais longe da câmera. Precisa seguir a mesma ordem do Sorting Order (distante = Z maior) e ficar atrás do gameplay (Z 0).")]
    public float depthZ = 20f;
    [Tooltip("Cada objeto nasce numa 'fatia' de Z diferente dentro da camada, para dois objetos não ocuparem o mesmo plano (evita z-fighting).")]
    [Min(1)] public int zSlots = 6;
    [Tooltip("Distância em Z entre as fatias. Use pelo menos o tamanho do maior modelo da camada (os modelos giram em 3D).")]
    [Min(0.01f)] public float zSlotSpacing = 2f;

    [Header("Variedade")]
    [Tooltip("Não repete nenhuma das últimas N variantes sorteadas (limitado a 'quantidade de prefabs - 1'). 0 = sorteio livre.")]
    [Min(0)] public int avoidRepeatCount = 2;

    [Header("Rotação 3D")]
    [Tooltip("Ligado = cada objeto nasce com uma rotação aleatória e continua girando durante o trajeto.")]
    public bool randomRotation = true;
    [Tooltip("Quais eixos giram (1 = gira, 0 = fica parado naquele eixo). Valores intermediários reduzem o giro naquele eixo.")]
    public Vector3 rotationAxes = Vector3.one;
    [Tooltip("Velocidade de giro sorteada por eixo (graus/s), com sentido aleatório.")]
    public float minSpinDegreesPerSecond = 5f;
    public float maxSpinDegreesPerSecond = 40f;
    [Tooltip("Quanto o giro acompanha a velocidade do parallax. 0 = gira sempre na mesma velocidade (não desacelera na fase de loot nem acelera ao começar a corrida); 1 = acompanha totalmente; valores no meio misturam.")]
    [Range(0f, 1f)] public float spinFollowsParallaxSpeed = 0f;

    readonly List<int> recentPicks = new List<int>();
    int zSlotCounter;
    float secondsUntilNextSpawn;

    void OnEnable()
    {
        ScheduleNextSpawn();
    }

    void Update()
    {
        // Na fase de loot a velocidade cai; o relógio de spawn cai junto, senão os props ficariam
        // amontoados (mesma quantidade andando mais devagar).
        float extra = ParallaxSpeedController.Instance != null ? ParallaxSpeedController.Instance.ExtraMultiplier : 1f;
        secondsUntilNextSpawn -= Time.deltaTime * extra;
        if (secondsUntilNextSpawn <= 0f)
        {
            SpawnOne();
            ScheduleNextSpawn();
        }
    }

    void ScheduleNextSpawn()
    {
        secondsUntilNextSpawn = Random.Range(minSecondsBetweenSpawns, maxSecondsBetweenSpawns);
    }

    void SpawnOne()
    {
        if (spritePrefabs == null || spritePrefabs.Length == 0) return;

        GameObject prefab = spritePrefabs[PickIndex()];

        // Assume câmera ortográfica (setup 2D padrão) — nesse caso, X/Y não dependem do valor de
        // profundidade usado aqui, então qualquer distância positiva serve.
        Camera cam = Camera.main;
        Vector3 worldSpawnPos = cam.ViewportToWorldPoint(new Vector3(1.05f, 0.5f, 10f));
        worldSpawnPos.y = Random.Range(minY, maxY);
        // Ordem visual = Sorting Layer/Order (abaixo) E profundidade Z coerentes entre si. Cada objeto usa uma fatia de Z.
        worldSpawnPos.z = depthZ + (zSlotCounter % Mathf.Max(1, zSlots)) * zSlotSpacing;
        zSlotCounter++;

        GameObject instance = PoolManager.Instance.Get(prefab, worldSpawnPos, Quaternion.identity);

        ApplySorting(instance);

        float scale = Random.Range(minScale, maxScale);
        // Usa a escala do prefab (asset), não a da instância, para não acumular quando o objeto volta do pool.
        instance.transform.localScale = prefab.transform.localScale * scale;

        ParallaxMover mover = instance.GetComponent<ParallaxMover>();
        if (mover == null)
        {
            mover = instance.AddComponent<ParallaxMover>();
        }

        Vector3 spin = Vector3.zero;
        if (randomRotation)
        {
            instance.transform.rotation = Quaternion.Euler(
                Random.Range(0f, 360f) * rotationAxes.x,
                Random.Range(0f, 360f) * rotationAxes.y,
                Random.Range(0f, 360f) * rotationAxes.z) * prefab.transform.rotation;

            spin = new Vector3(RandomSpin() * rotationAxes.x, RandomSpin() * rotationAxes.y, RandomSpin() * rotationAxes.z);
        }

        PushOffRightEdge(instance, cam);

        float jitter = 1f + Random.Range(-speedJitter, speedJitter);
        mover.Initialize(layerSpeed * jitter, spin, spinFollowsParallaxSpeed);
    }

    float RandomSpin()
    {
        float speed = Random.Range(minSpinDegreesPerSecond, maxSpinDegreesPerSecond);
        return Random.value < 0.5f ? -speed : speed;
    }

    // Sorteia uma variante evitando repetir as últimas 'avoidRepeatCount'.
    int PickIndex()
    {
        int count = spritePrefabs.Length;
        int avoid = Mathf.Min(avoidRepeatCount, count - 1);
        if (avoid <= 0) return Random.Range(0, count);

        List<int> candidates = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (!recentPicks.Contains(i)) candidates.Add(i);
        }

        int pick = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : Random.Range(0, count);

        recentPicks.Add(pick);
        while (recentPicks.Count > avoid) recentPicks.RemoveAt(0);
        return pick;
    }

    // Aplica a Sorting Layer / Order desta camada em TODOS os renderers do objeto (modelo 3D, sprites, partículas).
    void ApplySorting(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerID = sortingLayerId;
            renderers[i].sortingOrder = sortingOrder;
        }
    }

    // Depois de aplicar escala e rotação, empurra o objeto para a direita até ele ficar totalmente fora da tela.
    // Usa o raio do objeto (a partir do pivô) para valer em qualquer rotação, já que ele gira durante o trajeto.
    void PushOffRightEdge(GameObject instance, Camera cam)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled) continue;
            if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }
        if (!hasBounds) return;

        Vector3 position = instance.transform.position;
        float reach = (bounds.center - position).magnitude + bounds.extents.magnitude;
        float rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 10f)).x;
        position.x = rightEdge + spawnMargin + reach;
        instance.transform.position = position;
    }
}
