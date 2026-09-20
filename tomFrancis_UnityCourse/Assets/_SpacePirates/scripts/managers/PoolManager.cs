using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance;

    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        public int initialSize;
    }

    public PoolConfig[] poolsToPrewarm;

    Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    void Awake()
    {
        Instance = this;

        foreach (var config in poolsToPrewarm)
        {
            Prewarm(config.prefab, config.initialSize);
        }
    }

    void Prewarm(GameObject prefab, int amount)
    {
        Queue<GameObject> queue = GetOrCreateQueue(prefab);
        for (int i = 0; i < amount; i++)
        {
            GameObject instance = CreateNewInstance(prefab);
            instance.SetActive(false);
            queue.Enqueue(instance);
        }
    }

    Queue<GameObject> GetOrCreateQueue(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
        {
            pools[prefab] = new Queue<GameObject>();
        }
        return pools[prefab];
    }

    GameObject CreateNewInstance(GameObject prefab)
    {
        // Instantiate() ativa o objeto IMEDIATAMENTE (disparando Awake/OnEnable) se o prefab
        // estiver ativo — ou seja, os scripts de movimento já capturam "origin" na posição
        // ERRADA (perto do próprio PoolManager) antes da gente corrigir a posição embaixo.
        GameObject instance = Instantiate(prefab, transform);

        PooledObject pooledObject = instance.GetComponent<PooledObject>();
        if (pooledObject == null)
        {
            pooledObject = instance.AddComponent<PooledObject>();
        }
        pooledObject.sourcePrefab = prefab;

        return instance;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Queue<GameObject> queue = GetOrCreateQueue(prefab);

        GameObject instance = null;
        while (queue.Count > 0 && instance == null)
        {
            instance = queue.Dequeue();
        }

        bool isBrandNew = false;
        if (instance == null)
        {
            instance = CreateNewInstance(prefab);
            isBrandNew = true;
        }

        if (isBrandNew)
        {
            // Desliga e liga de novo de propósito: isso força o OnEnable a rodar UMA SEGUNDA VEZ,
            // agora com a posição já corrigida — corrigindo o "origin" errado capturado acima.
            instance.SetActive(false);
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Return(GameObject instance)
    {
        PooledObject pooledObject = instance.GetComponent<PooledObject>();
        if (pooledObject == null)
        {
            Destroy(instance); // não veio do pool, não tem pra onde devolver
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform);
        GetOrCreateQueue(pooledObject.sourcePrefab).Enqueue(instance);
    }
}