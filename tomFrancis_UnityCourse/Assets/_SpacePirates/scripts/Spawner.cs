using UnityEngine;

// Um Spawner por prefab. Só sabe: "meu prefab nasce em um destes pontos".
public class Spawner : MonoBehaviour
{
    public GameObject prefab;
    public Transform[] allowedSpawnPoints;

    [Tooltip("Cada spawn recebe um deslocamento ao acaso (em X e Y) entre estes dois valores (mínimo, máximo). 0,0 = sem deslocamento.")]
    public Vector2 offsetRange;

    [Tooltip("Quantos dos últimos pontos usados este spawner evita repetir (0 = sorteio puro). Limitado automaticamente a (nº de pontos - 1).")]
    [Min(0)] public int avoidRepeatLastPoints = 1;

    readonly System.Collections.Generic.List<Transform> recentPoints = new System.Collections.Generic.List<Transform>();

    [Header("Teste (cena de balanceamento)")]
    [Tooltip("Ligado = este spawner fica spawnando sozinho, sem WaveManager. Serve para uma cena só de teste de movimento/curva. Deixe DESLIGADO nas cenas do jogo.")]
    public bool autoSpawn = false;
    [Min(0.05f)] public float autoSpawnInterval = 1.5f;
    [Tooltip("Ligado = só spawna enquanto o jogo está em Playing (não spamma no menu). Desligue para spawnar desde o Play.")]
    public bool autoSpawnOnlyWhenPlaying = true;

    float autoTimer;


    void Update()
    {
        if (!autoSpawn) return;
        if (autoSpawnOnlyWhenPlaying && GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;

        autoTimer += Time.deltaTime;
        if (autoTimer < autoSpawnInterval) return;
        autoTimer = 0f;
        SpawnOne();
    }

    // Ponto de spawn usado pela última chamada (a fase de loot usa isso para não repetir o mesmo ponto em seguida).
    public Transform LastSpawnPoint { get; private set; }

    public GameObject SpawnOne()
    {
        return SpawnOne(null);
    }

    // Igual a SpawnOne, mas evita 'avoid' quando houver outro ponto disponível.
    public GameObject SpawnOne(Transform avoid)
    {
        if (allowedSpawnPoints == null || allowedSpawnPoints.Length == 0) return null;

        int memory = Mathf.Clamp(avoidRepeatLastPoints, 0, allowedSpawnPoints.Length - 1);
        while (recentPoints.Count > memory) recentPoints.RemoveAt(0);

        // Candidatos: pontos que não foram usados há pouco e (se der) diferentes de 'avoid'.
        System.Collections.Generic.List<Transform> candidates = new System.Collections.Generic.List<Transform>();
        foreach (Transform p in allowedSpawnPoints)
        {
            if (p == null || recentPoints.Contains(p)) continue;
            if (avoid != null && p == avoid && allowedSpawnPoints.Length > 1) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0)
        {
            foreach (Transform p in allowedSpawnPoints)
            {
                if (p != null) candidates.Add(p);
            }
            if (candidates.Count == 0) return null;
        }

        Transform spawnPoint = candidates[Random.Range(0, candidates.Count)];
        LastSpawnPoint = spawnPoint;

        if (memory > 0)
        {
            recentPoints.Add(spawnPoint);
            while (recentPoints.Count > memory) recentPoints.RemoveAt(0);
        }

        Vector3 offset = new Vector3(Random.Range(offsetRange.x, offsetRange.y), Random.Range(offsetRange.x, offsetRange.y), 0f);
        return PoolManager.Instance.Get(prefab, spawnPoint.position + offset, Quaternion.identity);
    }
}
