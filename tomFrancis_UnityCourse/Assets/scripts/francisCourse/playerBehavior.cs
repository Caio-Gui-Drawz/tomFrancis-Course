using System.Collections.Generic;
using UnityEngine;

public class playerBehavior : MonoBehaviour
{
 
      //Never set the value of a public variable here - the inspector will override it without telling you 
      public float speed;
      public List<weaponBehavior> weapons = new List<weaponBehavior>();
      public int selectedWeaponIndex;


    void Start()
    {
        
        references.thePlayer = gameObject; //guarda a referencia do player na classe references, para que outros scripts possam acessar o player
        selectedWeaponIndex = 0;

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
    
    if (weapons.Count > 0 && Input.GetButton("Fire1"))
        {
            //Tell our weapon to fire
            weapons[selectedWeaponIndex].Fire(cursorPosition); 

        }
    

    //change weapon
    
    if (Input.GetButtonDown("Fire2"))
        {
            ChangeWeaponIndex(selectedWeaponIndex +1 );

        }
    }


    private void ChangeWeaponIndex(int index)
    {
        //change our index
         selectedWeaponIndex = index;
        //fi its gone to far, loop back around
        if (selectedWeaponIndex >= weapons.Count)
        {
            selectedWeaponIndex = 0;
        }

        //For each weapon in our list, 
         for (
        int i = 0;  //Declare a variable to keep track of how many iterations we've done
        i < weapons.Count; // set a limit for how high this variable can go
        i++ //run this after each time we iterate - increase the iteration count.
        )
        {
            if (i == selectedWeaponIndex) 
            {
                //if its the one we just selected, enable it, else disable it
                weapons[i].gameObject.SetActive(true);
            } else
            {
                weapons[i].gameObject.SetActive(false);
            }
        }


    }


    private void OnTriggerEnter(Collider other)
    {
        weaponBehavior theirWeapon = other.GetComponentInParent<weaponBehavior>();
        if (theirWeapon != null)
        {
            weapons.Add(theirWeapon);
            theirWeapon.transform.SetParent(transform);
            theirWeapon.transform.position = transform.position;
            theirWeapon.transform.rotation = transform.rotation;
            ChangeWeaponIndex(weapons.Count - 1);
            
        }
    }  
    
}
