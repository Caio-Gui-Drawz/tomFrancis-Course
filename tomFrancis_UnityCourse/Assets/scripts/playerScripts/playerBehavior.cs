
using UnityEngine;

public class playerBehavior : MonoBehaviour
{
 
      //Never set the value of a public variable here - the inspector will override it without telling you 
      public float speed;
      public GameObject bulletPrefab;

    void Start()
    {
        Debug.Log(speed);
    }

    // Update is called once per frame
    void Update()
    {


    //WASD to move
      float maxDistanceToMove = Time.deltaTime * speed;

       Debug.Log(Input.GetAxis("Vertical"));

       transform.position += Vector3.forward * Input.GetAxis("Vertical") * maxDistanceToMove;
       transform.position += Vector3.right * Input.GetAxis("Horizontal") * maxDistanceToMove;

    //Click to fire
    if (Input.GetButton("Fire1"))
        {
         Instantiate(bulletPrefab, transform.position, transform.rotation);
        }
    
    }
}
