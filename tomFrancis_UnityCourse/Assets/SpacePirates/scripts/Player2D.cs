using UnityEngine;

public class Player2D : MonoBehaviour
{

    public GameObject bulletPrefab;
    public float fireRate; //how many seconds between shots 
    float secondsSinceLastShot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        secondsSinceLastShot += Time.deltaTime; //vai aumentar esse valor todo segundo ate chegar no fireRate, ai o player vai poder atirar de novo
        
    if (secondsSinceLastShot >= fireRate && Input.GetButton("Fire1"))
        {

            //cria a bala na posição do player mas com um offset para frente, para não colidir com o player
            //pega o "forward" do transform do player e soma isso com a posicao do player adcionando 1 unidade na frente
         Instantiate(bulletPrefab, transform.position + transform.right, transform.rotation);

         secondsSinceLastShot = 0; //reseta o contador de tempo para o próximo tiro
        }
    
    }
}


