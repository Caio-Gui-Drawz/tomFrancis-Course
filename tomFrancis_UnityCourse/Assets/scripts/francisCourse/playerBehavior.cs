
using UnityEngine;

public class playerBehavior : MonoBehaviour
{
 
      //Never set the value of a public variable here - the inspector will override it without telling you 
      public float speed;
      public GameObject bulletPrefab;

      public float fireRate; //how many seconds between shots 

      float secondsSinceLastShot;

    void Start()
    {


        secondsSinceLastShot = fireRate; //inicia podendo atirar

    }

    // Update is called once per frame
    void Update()
    {


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
    secondsSinceLastShot += Time.deltaTime; //vai aumentar esse valor todo segundo ate chegar no fireRate, ai o player vai poder atirar de novo



    if (secondsSinceLastShot >= fireRate && Input.GetButton("Fire1"))
        {

            //cria a bala na posição do player mas com um offset para frente, para não colidir com o player
            //pega o "forward" do transform do player e soma isso com a posicao do player adcionando 1 unidade na frente
         Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);

         secondsSinceLastShot = 0; //reseta o contador de tempo para o próximo tiro
        }
    
    }
}
