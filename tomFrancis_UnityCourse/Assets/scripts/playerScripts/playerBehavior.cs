
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

     Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));    //vetor que usa os inputs aplicados no axis 
    Rigidbody ourRigidbody = GetComponent<Rigidbody>();
    ourRigidbody.linearVelocity = inputVector * speed;

    //Find the new position we'll move to 

     Vector3 lookAtPosition = transform.position + inputVector;
       
        transform.LookAt(lookAtPosition);           //face our new position  

    //  transform.position = newPosition;   //actually move there //registro para mostrar em RM
          
    //Click to fire
    if (Input.GetButton("Fire1"))
        {

            //cria a bala na posição do player mas com um offset para frente, para não colidir com o player
            //pega o "forward" do transform do player e soma isso com a posicao do player adcionando 1 unidade na frente
         Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);
        }
    
    }
}
