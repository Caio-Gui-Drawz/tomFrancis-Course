using UnityEngine;

public class enemyBehavior : MonoBehaviour

{
    
    public float speed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Rigidbody ourRigidbody = GetComponent<Rigidbody>();
        Vector3 vectorToPlayer = references.thePlayer.transform.position - transform.position;
        ourRigidbody.linearVelocity = vectorToPlayer.normalized * speed; //normalizar faz o vetor ter a mesma direção mas com comprimento (magnitude) igual a 1
        
    }
}
