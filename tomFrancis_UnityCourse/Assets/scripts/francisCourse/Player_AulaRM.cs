
using UnityEngine;

public class playerAulaRM : MonoBehaviour
{
 
      //Never set the value of a public variable here - the inspector will override it without telling you 
      public float speed;
      public GameObject bulletPrefab;

    void Start()
    {
    
    }

    // Update is called once per frame
    void Update()
    {


    //WASD to move

    //Find the new position we'll move to 

  float maxDistanceToMove = Time.deltaTime * speed; 

     Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));    //vetor que usa os inputs aplicados no axis 

     Vector3 movementVector = inputVector * maxDistanceToMove; //RM // valor do axis (quanto e em qual direção o player aponta) e a distancia máxima que ele pode se mover no jogo.
  
     Vector3 newPosition = transform.position + movementVector; 

       
        transform.LookAt(newPosition);           //face our new position  

        transform.position = newPosition;   //actually move there //registro para mostrar em RM
          
    //Click to fire
    if (Input.GetButton("Fire1"))
        {

            //cria a bala na posição do player mas com um offset para frente, para não colidir com o player
            //pega o "forward" do transform do player e soma isso com a posicao do player adcionando 1 unidade na frente
         Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);
        }
    
    }
}
