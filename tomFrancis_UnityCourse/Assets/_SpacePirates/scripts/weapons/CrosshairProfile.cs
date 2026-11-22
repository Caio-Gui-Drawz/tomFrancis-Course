using System;
using UnityEngine;

// O "visual" do crosshair de uma arma. Cada arma (WeaponData) aponta para um perfil; armas parecidas podem dividir o mesmo.
// Criar: Assets > Create > Cosmo Treasure > Crosshair Profile.
[CreateAssetMenu(fileName = "NewCrosshair", menuName = "Cosmo Treasure/Crosshair Profile")]
public class CrosshairProfile : ScriptableObject
{
    // Como o crosshair fica em um "momento" (recarregando, acertou, munição baixa).
    [Serializable]
    public class Look
    {
        [Tooltip("Sprite deste momento. Vazio = continua com o sprite Normal.")]
        public Sprite sprite;

        [Tooltip("Ligado = troca a cor neste momento. Desligado = continua com a cor Normal.")]
        public bool changeColor = true;
        public Color color = Color.white;

        [Tooltip("Multiplica o tamanho do crosshair neste momento (1 = igual ao normal).")]
        [Min(0.1f)] public float sizeMultiplier = 1f;
    }

    [Header("Normal")]
    public Sprite sprite;
    public Color color = Color.white;
    [Tooltip("Tamanho do crosshair na tela, em pixels do Canvas (o sprite mantém a proporção).")]
    [Min(1f)] public float size = 64f;

    [Header("Momentos")]
    [Tooltip("Durante a animação de recarga (troca de arma). Quando ela termina, volta ao Normal.")]
    public Look reload = new Look { color = new Color(1f, 1f, 1f, 0.45f) };

    [Tooltip("Munição baixa (só armas com munição).")]
    public Look lowAmmo = new Look { color = new Color(1f, 0.35f, 0.25f, 1f) };
    [Tooltip("Considera 'baixa' quando sobra esta fração da munição ou menos (sempre pelo menos 1 tiro).")]
    [Range(0.05f, 0.6f)] public float lowAmmoFraction = 0.25f;

    [Tooltip("Logo depois que uma bala sua acerta um inimigo.")]
    public Look hit = new Look { color = new Color(1f, 0.9f, 0.3f, 1f) };
    [Tooltip("Quanto tempo (s) o visual de acerto fica.")]
    [Min(0.02f)] public float hitDuration = 0.12f;

    [Header("Pulso ao atirar (DOTween)")]
    [Tooltip("Quanto o crosshair cresce a cada tiro. 0.25 = 25% maior no pico. 0 = sem pulso.")]
    [Min(0f)] public float firePunchScale = 0.25f;
    [Min(0.01f)] public float firePunchDuration = 0.12f;

    [Header("Transição")]
    [Tooltip("Tempo (s) da troca suave de cor entre os momentos. 0 = troca na hora.")]
    [Min(0f)] public float colorFadeTime = 0.06f;

    [Header("Arco de munição")]
    [Tooltip("Mostra o arco de munição em volta da mira quando a arma tem munição. Desligado = o arco fica escondido com esta arma.")]
    public bool showAmmoArc = true;
}
