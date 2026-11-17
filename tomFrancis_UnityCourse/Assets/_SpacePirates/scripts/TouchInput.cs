using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Controles de toque, no mesmo sistema de Input antigo. Convive com teclado e mouse na mesma build.
//   JOYSTICK fixo no canto ESQUERDO da tela (aparece quando o jogo percebe que é toque). É a ÚNICA forma de mover o player.
//   QUALQUER toque FORA do joystick é uma área de tiro: atira enquanto o dedo estiver na tela e mira para o ponto
//   tocado (cima, baixo, até para trás do player). Funciona ao mesmo tempo que o dedo do joystick.
// Cria a si mesmo ao iniciar o jogo (não precisa colocar na cena).
//
// Uso no resto do jogo (troque as chamadas diretas de Input por estas, que somam teclado/mouse + toque):
//   TouchInput.Vertical / TouchInput.Horizontal   no lugar de Input.GetAxisRaw("Vertical"/"Horizontal")
//   TouchInput.FireHeld        no lugar de Input.GetButton("Fire1")
//   TouchInput.FireDown        no lugar de Input.GetButtonDown("Fire1")
//   TouchInput.PointerPosition no lugar de Input.mousePosition (posição de mira em pixels de tela)
[DefaultExecutionOrder(-100)]
public class TouchInput : MonoBehaviour
{
    [Header("Joystick (canto esquerdo)")]
    [Tooltip("Raio do joystick, em fração da altura da tela.")]
    [Range(0.05f, 0.3f)] public float stickRadius = 0.14f;

    [Tooltip("Posição do centro do joystick: X em fração da LARGURA da tela, Y em fração da ALTURA (0 = embaixo). O centro nunca fica tão perto da borda que o círculo seja cortado.")]
    public Vector2 stickCenter = new Vector2(0.10f, 0.28f);

    [Tooltip("Área onde um toque 'pega' o joystick, em múltiplos do raio (1 = só o círculo). Maior = mais fácil de acertar com o dedo.")]
    [Range(1f, 2.5f)] public float grabScale = 1.5f;

    [Tooltip("Zona morta do joystick (0 a 1).")]
    [Range(0f, 0.6f)] public float deadZone = 0.15f;

    [Header("Tiro")]
    [Tooltip("Ligado = toque em cima de botão/UI (pausa, menu) não conta como tiro. Se o tiro parar de funcionar em algum ponto da tela, desligue: pode haver uma imagem de UI cobrindo a tela com 'Raycast Target' ligado.")]
    public bool ignoreTouchesOverUI = true;

    public static TouchInput Instance { get; private set; }

    // ---------------------------------------------------------------- API estática

    public static float Vertical
    {
        get
        {
            float v = Input.GetAxisRaw("Vertical");
            if (Instance != null) v += Instance.stickVec.y;
            return Mathf.Clamp(v, -1f, 1f);
        }
    }

    public static float Horizontal
    {
        get
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (Instance != null) h += Instance.stickVec.x;
            return Mathf.Clamp(h, -1f, 1f);
        }
    }

    public static bool FireHeld
    {
        get { return Input.GetButton("Fire1") || (Instance != null && Instance.aimFingers.Count > 0); }
    }

    public static bool FireDown
    {
        get { return Input.GetButtonDown("Fire1") || (Instance != null && Instance.fireDownFrame == Time.frameCount); }
    }

    // Em toque: o ponto onde está (ou esteve por último) o dedo de tiro. No desktop: o mouse.
    public static Vector2 PointerPosition
    {
        get
        {
            if (Instance != null && Instance.touchMode) return Instance.aimPosition;
            return Input.mousePosition;
        }
    }

    // Mantidos só para não quebrar scripts antigos: a mira por ângulo foi aposentada (a mira agora é pelo ponto tocado).
    public static bool UseAimAngle { get { return false; } }
    public static float AimAngleDegrees { get { return 0f; } }

    public static bool TouchMode
    {
        get { return Instance != null && Instance.touchMode; }
    }

    // ---------------------------------------------------------------- estado

    bool touchMode;
    int stickFinger = -1;
    Vector2 stickPos;
    Vector2 stickVec;
    readonly List<int> aimFingers = new List<int>(); // o último da lista é o que manda na mira
    Vector2 aimPosition;
    int fireDownFrame = -1;
    Texture2D circle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("TouchInput");
        DontDestroyOnLoad(go);
        go.AddComponent<TouchInput>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Os dedos não podem virar clique de mouse (senão o joystick atiraria). O tiro por toque vem de FireHeld/PointerPosition.
        Input.simulateMouseWithTouches = false;
        aimPosition = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        circle = MakeCircle(128);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (circle != null) Destroy(circle);
    }

    void OnDisable()
    {
        ResetFingers();
    }

    void ResetFingers()
    {
        stickFinger = -1;
        aimFingers.Clear();
        stickVec = Vector2.zero;
    }

    float Radius { get { return Screen.height * stickRadius; } }

    Vector2 StickScreenCenter()
    {
        float r = Radius;
        float x = Mathf.Max(stickCenter.x * Screen.width, r * 1.2f);
        float y = Mathf.Max(stickCenter.y * Screen.height, r * 1.2f);
        return new Vector2(x, y);
    }

    void Update()
    {
        // Mouse de verdade volta o jogo ao modo desktop (notebooks com tela de toque).
        if (Input.touchCount == 0 && touchMode && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            touchMode = false;
            ResetFingers();
        }

        float radius = Radius;
        Vector2 center = StickScreenCenter();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                touchMode = true;

                if (stickFinger == -1 && Vector2.Distance(t.position, center) <= radius * grabScale)
                {
                    stickFinger = t.fingerId;
                    stickPos = t.position;
                }
                else if (!IsOverUI(t.fingerId))
                {
                    aimFingers.Add(t.fingerId);
                    aimPosition = t.position;
                    fireDownFrame = Time.frameCount;
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId == stickFinger) stickFinger = -1;
                else aimFingers.Remove(t.fingerId);
            }
            else if (t.fingerId == stickFinger)
            {
                stickPos = t.position;
            }
        }

        // Mira = posição do dedo de tiro mais recente que ainda está na tela (se soltar um, vale o outro).
        if (aimFingers.Count > 0)
        {
            int id = aimFingers[aimFingers.Count - 1];
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId == id) { aimPosition = t.position; break; }
            }
        }

        // Joystick: deslocamento do dedo a partir do centro fixo, limitado ao raio.
        if (stickFinger != -1)
        {
            Vector2 raw = Vector2.ClampMagnitude((stickPos - center) / radius, 1f);
            float mag = raw.magnitude;
            stickVec = mag < deadZone ? Vector2.zero : raw.normalized * Mathf.InverseLerp(deadZone, 1f, mag);
        }
        else
        {
            stickVec = Vector2.zero;
        }
    }

    bool IsOverUI(int fingerId)
    {
        return ignoreTouchesOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    // ---------------------------------------------------------------- visual

    void OnGUI()
    {
        if (!touchMode || circle == null) return;

        float radius = Radius;
        Vector2 center = StickScreenCenter();
        bool active = stickFinger != -1;
        Vector2 knob = center;
        if (active) knob = center + Vector2.ClampMagnitude(stickPos - center, radius);

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, active ? 0.25f : 0.14f);
        DrawCircle(center, radius);
        GUI.color = new Color(1f, 1f, 1f, active ? 0.6f : 0.25f);
        DrawCircle(knob, radius * 0.45f);
        GUI.color = old;
    }

    void DrawCircle(Vector2 screenCenter, float radius)
    {
        // OnGUI tem o Y invertido em relação à posição do toque.
        Rect r = new Rect(screenCenter.x - radius, Screen.height - screenCenter.y - radius, radius * 2f, radius * 2f);
        GUI.DrawTexture(r, circle);
    }

    static Texture2D MakeCircle(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float c = (size - 1) * 0.5f;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((1f - d) * c * 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
