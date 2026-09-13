using System.Collections;
using UnityEngine;
using TMPro;

// Colocar num objeto SEMPRE ATIVO do HUD (ex: o Canvas ou o HudPanel) — não no bannerPanel
// que aparece/some, senão a inscrição no evento se perde quando ele for desativado.
public class WaveHUD : MonoBehaviour
{
    public WaveManager waveManager;
    public GameObject bannerPanel; // o aviso em si (filho deste objeto), que aparece e some
    public TMP_Text waveText;
    public float visibleSeconds = 2f;

    Coroutine hideRoutine;

    void OnEnable()
    {
        waveManager.OnWaveStarted += HandleWaveStarted;
    }

    void OnDisable()
    {
        waveManager.OnWaveStarted -= HandleWaveStarted;
    }

    void HandleWaveStarted(int waveNumber)
    {
        waveText.text = "Wave " + waveNumber;
        bannerPanel.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleSeconds);
        bannerPanel.SetActive(false);
    }
}
