using UnityEngine;

// Vai em cada instância criada pelo pool — carrega a "identidade" (de qual prefab veio),
// pra saber pra qual fila devolver quando for desativada.
public class PooledObject : MonoBehaviour
{
    [HideInInspector] public GameObject sourcePrefab;
}
