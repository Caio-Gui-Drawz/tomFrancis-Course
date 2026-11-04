using UnityEngine;

// Controles de toque, no mesmo sistema de Input antigo. Convive com teclado e mouse na mesma build.
//   Metade ESQUERDA da tela: joystick vertical flutuante (o ponto onde o dedo toca vira o centro).
//   Metade DIREITA da tela: o dedo mira (posição do dedo) e atira enquanto estiver tocando.
// Cria a si mesmo ao iniciar o jogo (não precisa colocar na cena). Os controles só aparecem depois do primeiro toque.
//
// Uso no resto do jogo (troque as chamadas diretas de Input por estas, que somam teclado/mouse + toque):
//   TouchInput.Vertical        no lugar de Input.GetAxisRaw("Vertical")
//   TouchInput.FireHeld        no lugar de Input.GetButton("Fire1")
//   TouchInput.FireDown        no lugar de Input.GetButtonDown("Fire1")
//   TouchInput.PointerPosition no lugar de Input.mousePosition (posição de mira em pixels de tela)
[DefaultExecutionOrder(-100)]
public class TouchInput : MonoBehaviour
{
    [Tooltip("Raio do joystick, em fração da altura da tela.")]
    [Range(0.04f, 0.3f)] public float stickRadius = 0.12f;

    [Tooltip("Zona morta do joystick (0 a 1).")]
    [Range(0f, 0.6f)] public float deadZone = 0.15f;

    [Tooltip("Ligado = se o dedo passar do raio, o centro do joystick vai junto (não precisa voltar).")]
    public bool followFinger = true;

    [Tooltip("Até onde vai a zona do joystick, em fração da largura da tela (o resto é mira e tiro).")]
    [Range(0.2f, 0.8f)] public float stickZoneWidth = 0.5f;

    public static TouchInput Instance { get; private set; }

    // ---------------------------------------------------------------- API estática

    public static float Vertical
    {
        get
        {
            float v = Input.GetAxisRaw("Vertical");
            if (Instance != null) v += Instance.stickValue;
            return Mathf.Clamp(v, -1f, 1f);
        }
    }

    public static bool FireHeld
    {
        get { return Input.GetButton("Fire1") || (Instance != null && Instance.aimFinger != -1); }
    }

    public static bool FireDown
    {
        get { return Input.GetButtonDown("Fire1") || (Instance != null && Instance.fireDownFrame == Time.frameCount); }
    }

    public static Vector2 PointerPosition
    {
        get
        {
            if (Instance != null && Instance.touchMode) return Instance.aimPosition;
            return Input.mousePosition;
        }
    }

    public static bool TouchMode
    {
        get { return Instance != null && Instance.touchMode; }
    }

    // ---------------------------------------------------------------- estado

    bool touchMode;
    int stickFinger = -1;
    int aimFinger = -1;
    Vector2 stickOrigin;
    Vector2 stickPos;
    float stickValue;
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
        // O dedo do joystick não pode virar clique de mouse (senão atiraria). A mira por toque vem de PointerPosition.
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
        stickFinger = -1;
        aimFinger = -1;
        stickValue = 0f;
    }

    void Update()
    {
        // Mouse de verdade volta o jogo ao modo desktop (notebooks com tela de toque).
        if (Input.touchCount == 0 && touchMode && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            touchMode = false;
            stickFinger = -1;
            aimFinger = -1;
            stickValue = 0f;
        }

        float radius = Screen.height * stickRadius;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                touchMode = true;
                bool leftSide = t.position.x < Screen.width * stickZoneWidth;

                if (leftSide && stickFinger == -1)
                {
                    stickFinger = t.fingerId;
                    stickOrigin = t.position;
                    stickPos = t.position;
                }
                else if (!leftSide && aimFinger == -1)
                {
                    aimFinger = t.fingerId;
                    aimPosition = t.position;
                    fireDownFrame = Time.frameCount;
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId == stickFinger) { stickFinger = -1; stickValue = 0f; }
                if (t.fingerId == aimFinger) aimFinger = -1; // a mira fica onde parou
            }
            else
            {
                if (t.fingerId == stickFinger) stickPos = t.position;
                if (t.fingerId == aimFinger) aimPosition = t.position;
            }
        }

        if (stickFinger != -1)
        {
            float dy = stickPos.y - stickOrigin.y;

            if (followFinger && Mathf.Abs(dy) > radius)
            {
                stickOrigin.y += dy - Mathf.Sign(dy) * radius;
                dy = stickPos.y - stickOrigin.y;
            }

            float raw = Mathf.Clamp(dy / radius, -1f, 1f);
            float mag = Mathf.Abs(raw);
            stickValue = mag < deadZone ? 0f : Mathf.Sign(raw) * Mathf.InverseLerp(deadZone, 1f, mag);
        }
    }

    // ---------------------------------------------------------------- visual

    void OnGUI()
    {
        if (!touchMode || circle == null) return;

        float radius = Screen.height * stickRadius;
        Vector2 center = stickFinger != -1 ? stickOrigin : new Vector2(Screen.width * 0.12f, Screen.height * 0.3f);
        float knobY = stickFinger != -1 ? Mathf.Clamp(stickPos.y, stickOrigin.y - radius, stickOrigin.y + radius) : center.y;
        Vector2 knob = new Vector2(center.x, knobY);

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, stickFinger != -1 ? 0.25f : 0.12f);
        DrawCircle(center, radius);
        GUI.color = new Color(1f, 1f, 1f, stickFinger != -1 ? 0.6f : 0.2f);
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
