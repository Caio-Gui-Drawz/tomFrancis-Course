using DG.Tweening;
using TMPro;
using UnityEngine;

// Mostra a pontuação e a wave alcançada. Serve para a tela de Game Over E para a de vitória: coloque o componente
// no próprio painel (ou em qualquer filho dele) e arraste os textos. Os valores são lidos toda vez que o painel
// aparece (OnEnable), então estão sempre atualizados.
public class EndScreenStats : MonoBehaviour
{
    [Header("Textos")]
    public TMP_Text scoreText;
    public TMP_Text waveText;

    [Header("Formato")]
    [Tooltip("{0} = pontuação.")]
    public string scoreFormat = "Pontuação: {0}";
    [Tooltip("{0} = wave alcançada, {1} = total de waves (0 se o jogo não tem limite).")]
    public string waveFormat = "Wave alcançada: {0}/{1}";

    [Header("Animação")]
    [Tooltip("A pontuação sobe de 0 até o valor final nesse tempo (s). 0 = aparece direto.")]
    [Min(0f)] public float scoreCountSeconds = 0.8f;
    [Tooltip("Espera (s) antes de começar a contar.")]
    [Min(0f)] public float scoreCountDelay = 0.3f;

    Tween countTween;

    void OnEnable()
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.currentScore : 0;

        WaveManager waves = FindFirstObjectByType<WaveManager>();
        int wave = waves != null ? waves.CurrentWave : 0;
        int total = waves != null ? waves.TotalWaves : 0;

        if (waveText != null) waveText.text = string.Format(waveFormat, wave, total);

        if (scoreText == null) return;

        if (countTween != null) countTween.Kill();
        if (scoreCountSeconds <= 0f || score <= 0)
        {
            scoreText.text = string.Format(scoreFormat, score);
            return;
        }

        scoreText.text = string.Format(scoreFormat, 0);
        float shown = 0f;
        countTween = DOTween.To(() => shown, v =>
            {
                shown = v;
                scoreText.text = string.Format(scoreFormat, Mathf.RoundToInt(v));
            }, score, scoreCountSeconds)
            .SetDelay(scoreCountDelay)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => scoreText.text = string.Format(scoreFormat, score));
    }

    void OnDisable()
    {
        if (countTween != null) { countTween.Kill(); countTween = null; }
    }
}
