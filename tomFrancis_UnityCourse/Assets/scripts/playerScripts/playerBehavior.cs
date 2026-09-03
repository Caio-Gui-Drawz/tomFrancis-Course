
using UnityEngine;

public class playerBehavior : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

      float speed = 0.5f;
      //Debug.Log(Input.GetAxis("Vertical"));

       transform.position += Vector3.forward * Input.GetAxis("Vertical") * speed;

    }
}
