using UnityEngine;

public class bullet2D : MonoBehaviour
{

    public float bulletSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Rigidbody2D ourRigidbody = GetComponent<Rigidbody2D>();
        ourRigidbody.linearVelocity = transform.right * bulletSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
