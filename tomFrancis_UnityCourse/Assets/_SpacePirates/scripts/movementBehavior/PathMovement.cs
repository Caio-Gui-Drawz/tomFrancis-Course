using UnityEngine;
using DG.Tweening;

// Abordagem B: o DOTween dirige o movimento inteiro por uma SPLINE (curva Catmull-Rom),
// desenhada com waypoints relativos ao ponto de spawn.
// Isto é o "follow curve spawner / modular curves" que estava no seu board.
//
// REQUER: Rigidbody2D deste prefab com Body Type = Kinematic (senão a física briga com o tween).
// Compatível com pooling (OnEnable/OnDisable, não Start).
public class PathMovement : MonoBehaviour
{
    [Tooltip("Waypoints RELATIVOS ao ponto onde o objeto nasceu. Ex: (-5,2), (-10,-2), (-15,0)")]
    public Vector3[] relativeWaypoints;

    public float duration = 6f;
    public Ease ease = Ease.Linear;
    public PathType pathType = PathType.CatmullRom; // CatmullRom = curva suave; Linear = quebrada
    public bool lookAtPathDirection = false;        // gira o sprite pra "olhar" pra onde vai

    [Tooltip("Variação aleatória aplicada aos waypoints, pra duas instâncias não fazerem a curva idêntica.")]
    public float waypointJitter = 0.5f;

    Tween pathTween;

    void OnEnable()
    {
        if (relativeWaypoints == null || relativeWaypoints.Length == 0) return;

        Vector3 origin = transform.position;
        Vector3[] worldWaypoints = new Vector3[relativeWaypoints.Length];
        for (int i = 0; i < relativeWaypoints.Length; i++)
        {
            Vector3 jitter = new Vector3(
                Random.Range(-waypointJitter, waypointJitter),
                Random.Range(-waypointJitter, waypointJitter),
                0f);
            worldWaypoints[i] = origin + relativeWaypoints[i] + jitter;
        }

        // Guardar em 'var' preserva o tipo concreto do DOPath, que é o que o SetLookAt exige.
        var tweener = transform.DOPath(worldWaypoints, duration, pathType)
            .SetEase(ease);

        if (lookAtPathDirection)
        {
            tweener.SetLookAt(0.01f, Vector3.right);
        }

        pathTween = tweener;
    }

    void OnDisable()
    {
        // OBRIGATÓRIO com pooling — ver comentário no AxisMovement.
        pathTween?.Kill();
        pathTween = null;
    }
}
