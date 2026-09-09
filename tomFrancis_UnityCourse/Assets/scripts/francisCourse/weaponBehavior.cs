using UnityEngine;

public class weaponBehavior : MonoBehaviour
{

     public GameObject bulletPrefab;

      public float fireRate; //how many seconds between shots 

      float secondsSinceLastShot;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
          secondsSinceLastShot = fireRate; //inicia podendo atirar
    }

    // Update is called once per frame
    void Update()
    {
         //Firing
    secondsSinceLastShot += Time.deltaTime; //vai aumentar esse valor todo segundo ate chegar no fireRate, ai o player vai poder atirar de novo




    }

    public void Fire()
    {
          if (secondsSinceLastShot >= fireRate)
        {

            //cria a bala na posição do player mas com um offset para frente, para não colidir com o player
            //pega o "forward" do transform do player e soma isso com a posicao do player adcionando 1 unidade na frente
         Instantiate(bulletPrefab, transform.position + transform.forward, transform.rotation);

         secondsSinceLastShot = 0; //reseta o contador de tempo para o próximo tiro
        }
    }
}
