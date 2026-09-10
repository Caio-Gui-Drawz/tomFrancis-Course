
using UnityEngine;

public class playerBehavior : MonoBehaviour
{
 
      //Never set the value of a public variable here - the inspector will override it without telling you 
      public float speed;
      public weaponBehavior myWeapon; 

    void Start()
    {

        references.thePlayer = gameObject; //guarda a referencia do player na classe references, para que outros scripts possam acessar o player

    }

    // Update is called once per frame
    void Update()
    {

    Debug.Log("player health: " + GetComponent<healthSystem>().maxHealth);

    //WASD to move
    Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));    //vetor que usa os inputs aplicados no axis 
    Rigidbody ourRigidbody = GetComponent<Rigidbody>();
    ourRigidbody.linearVelocity = inputVector * speed;


    // Fazer os tiros irem na direção do mouse
    Ray rayFromCameraToCursor = Camera.main.ScreenPointToRay(Input.mousePosition); //cria um raio que vai da camera até o cursor do mouse
    Plane playerPlane = new Plane(Vector3.up, transform.position);
    playerPlane.Raycast (rayFromCameraToCursor, out float distanceFromCamera);
    Vector3 cursorPosition = rayFromCameraToCursor.GetPoint(distanceFromCamera); //pega a posição do cursor no plano do player

    //face our new position  
    Vector3 lookAtPosition = cursorPosition;
       
    transform.LookAt(lookAtPosition);        


          
    //Firing
    
    if (Input.GetButton("Fire1"))
        {
            //Tell our weapon to fire
            myWeapon.Fire(cursorPosition); 

        }
    
    }
}
