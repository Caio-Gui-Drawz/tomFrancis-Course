using UnityEngine;
using UnityEngine.SceneManagement;

// Atalho de DEBUG para testes rápidos. Considere remover (ou desativar o objeto)
// antes de entregar a versão final da demo.
public class DebugReset : MonoBehaviour
{
    public KeyCode resetKey = KeyCode.R;

    void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            GameManager.startPlayingOnLoad = true; // reinicia já jogando, sem passar pelo menu
            Time.timeScale = 1f; // por segurança, caso o reset aconteça com o jogo pausado
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
