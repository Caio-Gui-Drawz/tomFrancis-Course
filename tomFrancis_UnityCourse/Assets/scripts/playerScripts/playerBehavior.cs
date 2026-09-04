
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
    Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));    //vetor que usa os inputs aplicados no axis 
    Vector3 movementVector = new inputVector * maxDistanceToMove;    // valor do axis (quanto e em qual direção o player aponta) e a distancia máxima que ele pode se mover no jogo.
    Vector3 newPosition = transform.position + movementVector;
       Debug.Log(Input.GetAxis("Vertical"));

       transform.position += Vector3.forward *  maxDistanceToMove;
       transform.position += Vector3.right *  maxDistanceToMove;

    //Click to fire
    if (Input.GetButton("Fire1"))
        {
         Instantiate(bulletPrefab, transform.position, transform.rotation);
        }
    
    }
}
