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

    public float minY = -2f;
    public float maxY = 2f;

    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    float secondsUntilNextSpawn;

    void OnEnable()
    {
        ScheduleNextSpawn();
    }

    void Update()
    {
        secondsUntilNextSpawn -= Time.deltaTime;
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

        GameObject prefab = spritePrefabs[Random.Range(0, spritePrefabs.Length)];

        // Assume câmera ortográfica (setup 2D padrão) — nesse caso, X/Y não dependem do valor de
        // profundidade usado aqui, então qualquer distância positiva serve.
        Camera cam = Camera.main;
        Vector3 worldSpawnPos = cam.ViewportToWorldPoint(new Vector3(1.05f, 0.5f, 10f));
        worldSpawnPos.y = Random.Range(minY, maxY);
        worldSpawnPos.z = 0f; // a ordem visual é resolvida por Sorting Layer, não por profundidade Z

        GameObject instance = PoolManager.Instance.Get(prefab, worldSpawnPos, Quaternion.identity);

        float scale = Random.Range(minScale, maxScale);
        instance.transform.localScale = Vector3.one * scale;

        ParallaxMover mover = instance.GetComponent<ParallaxMover>();
        if (mover == null)
        {
            mover = instance.AddComponent<ParallaxMover>();
        }

        float jitter = 1f + Random.Range(-speedJitter, speedJitter);
        mover.Initialize(layerSpeed * jitter);
    }
}
