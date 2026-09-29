using UnityEngine;

public class CircularMovement : MonoBehaviour
{
    public float radius = 2f;
    public float forwardSpeed = 2f;   
    public float lifetimeSeconds = 6f; 

    [Header("Curva de aceleração por volta (360°)")]
    public float degreesPerCycle = 360f; 
    public float cycleDuration = 2f;     
    public AnimationCurve angularSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Vector3 origin;
    float currentAngleDegrees;
    float elapsedTime;
    float secondsRemaining;
    float cycleElapsed;

    void OnEnable()
    {
        origin = transform.position;
        currentAngleDegrees = 0f;
        elapsedTime = 0f;
        secondsRemaining = lifetimeSeconds;
        cycleElapsed = 0f;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        
        float centerX = origin.x - forwardSpeed * elapsedTime;
        float centerY = origin.y;

        
        cycleElapsed += Time.deltaTime;
        float cycleProgress = Mathf.Clamp01(cycleElapsed / cycleDuration);

       
        float baseAngularSpeed = degreesPerCycle / cycleDuration;
        float speedMultiplier = angularSpeedCurve.Evaluate(cycleProgress);
        float instantAngularSpeed = baseAngularSpeed * speedMultiplier;

        currentAngleDegrees += instantAngularSpeed * Time.deltaTime;

        if (cycleElapsed >= cycleDuration)
        {
            cycleElapsed = 0f; 
        }

       
        float angleRadians = currentAngleDegrees * Mathf.Deg2Rad;
        float offsetX = radius * Mathf.Cos(angleRadians);
        float offsetY = radius * Mathf.Sin(angleRadians);
        Debug.Log($"angleRadians: {angleRadians}, offsetX: {offsetX}, offsetY: {offsetY}");

        transform.position = new Vector3(centerX + offsetX, centerY + offsetY, origin.z);

        secondsRemaining -= Time.deltaTime;
        if (secondsRemaining <= 0f)
        {
            PoolManager.Instance.Return(gameObject);
        }
    }
}