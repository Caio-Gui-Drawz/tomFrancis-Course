
using UnityEngine;
using UnityEngine.Serialization;

public class healthSystem : MonoBehaviour
{

     [FormerlySerializedAs("health")] // we write this to tell unity not to lose our data when we rename a variable. This was its old name.
    public float maxHealth;
    float currentHealth;
    public GameObject healthBarPrefab;
    public float healthBarOffset = 1.5f;

    healthBarBehavior myHealthBar;

     // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
        //Create our health panel ON the canvas
        GameObject healthBarObject = Instantiate(healthBarPrefab, references.Canvas.transform);
        myHealthBar = healthBarObject.GetComponent<healthBarBehavior>();
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
        if (currentHealth <= 0)
        {
            Destroy(gameObject); 
        }
    }

    private void OnDestroy()
    {
        if (myHealthBar != null)
        {
           Destroy(myHealthBar.gameObject); 
        }
        
    }
   

    // Update is called once per frame
    void Update()
    {
        //Make our healthbar reflect our health - myHealthBar.ShowHealth();
        myHealthBar.ShowHealthFraction(currentHealth / maxHealth);

        
        //Make our healthbar follow us - move ir to our current position
        myHealthBar.transform.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up  * healthBarOffset);


    }
}
