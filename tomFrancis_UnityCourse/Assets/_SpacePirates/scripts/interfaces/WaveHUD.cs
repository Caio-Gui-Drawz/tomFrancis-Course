using System.Collections;
using UnityEngine;
using TMPro;

// Colocar num objeto SEMPRE ATIVO do HUD (ex: o Canvas ou o HudPanel) — não no bannerPanel
// que aparece/some, senão a inscrição no evento se perde quando ele for desativado.
public class WaveHUD : MonoBehaviour
{
    public WaveManager waveManager;

    [Header("Início da wave")]
    public GameObject bannerPanel; // o aviso em si (filho deste objeto), que aparece e some
    public TMP_Text waveText;
    public float visibleSeconds = 2f;

    [Header("Fim da wave")]
    [Tooltip("Aviso de fim de wave. Vazio = reaproveita o banner do início (mesmo painel e texto).")]
    public GameObject endBannerPanel;
    public TMP_Text endWaveText;
    [Tooltip("{0} = número da wave que acabou.")]
    public string endMessage = "Wave {0} concluída!";
    public float endVisibleSeconds = 2.5f;

    Coroutine hideRoutine;

    void OnEnable()
    {
        waveManager.OnWaveStarted += HandleWaveStarted;
        waveManager.OnWaveCompleted += HandleWaveCompleted;
    }

    void OnDisable()
    {
        waveManager.OnWaveStarted -= HandleWaveStarted;
        waveManager.OnWaveCompleted -= HandleWaveCompleted;
    }

    void HandleWaveStarted(int waveNumber)
    {
        Show(bannerPanel, waveText, "Wave " + waveNumber, visibleSeconds);
    }

    void HandleWaveCompleted(int waveNumber)
    {
        GameObject panel = endBannerPanel != null ? endBannerPanel : bannerPanel;
        TMP_Text text = endWaveText != null ? endWaveText : waveText;
        Show(panel, text, string.Format(endMessage, waveNumber), endVisibleSeconds);
    }

    void Show(GameObject panel, TMP_Text text, string message, float seconds)
    {
        if (panel == null) return;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        // Só um aviso por vez: esconde o outro painel se for diferente.
        if (bannerPanel != null && bannerPanel != panel) bannerPanel.SetActive(false);
        if (endBannerPanel != null && endBannerPanel != panel) endBannerPanel.SetActive(false);

        if (text != null) text.text = message;
        panel.SetActive(true);
        hideRoutine = StartCoroutine(HideAfterDelay(panel, seconds));
    }

    IEnumerator HideAfterDelay(GameObject panel, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        panel.SetActive(false);
    }
}
