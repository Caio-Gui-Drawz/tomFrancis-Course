using UnityEngine;

public class Player2D : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate; // segundos entre tiros
    float secondsSinceLastShot;

    bool canAct;

    void OnEnable()
    {
        GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(GameState newState)
    {
        canAct = (newState == GameState.Playing);
    }

    void Start()
    {
        references.thePlayer = gameObject;
    }

    void Update()
    {
        if (!canAct) return;

        // Atirar
        secondsSinceLastShot += Time.deltaTime;
        if (secondsSinceLastShot >= fireRate && Input.GetButton("Fire1"))
        {
            Instantiate(bulletPrefab, transform.position + transform.right, transform.rotation);
            secondsSinceLastShot = 0;
        }

        // Virar de frente pro mouse
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = mousePosition - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
