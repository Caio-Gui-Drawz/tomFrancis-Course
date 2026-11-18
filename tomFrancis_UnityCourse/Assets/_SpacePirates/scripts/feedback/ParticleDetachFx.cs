using System.Collections;
using UnityEngine;

// Toca partículas que são FILHAS de um objeto do pool (bala, inimigo) sem que elas sumam quando o dono é devolvido
// ao pool. Ao dar Play(), este objeto se DESGRUDA do dono e fica no ponto do acerto/morte; quando as partículas
// terminam, ele volta para o dono (na posição original) e espera o próximo uso.
//
// Como usar:
//   1. Coloque o ParticleSystem como FILHO do prefab (bala ou inimigo), na posição/rotação que quiser (use o modo de
//      edição do prefab). Desligue "Play On Awake" nele.
//   2. Adicione este componente no MESMO objeto do ParticleSystem (ou num pai dele que seja filho do dono).
//   3. Ligue Play() a um evento: "On Hit" da bala, "On Death" / "On Damaged" do HealthFeedbackEvents.
// A rotação do dono vai junto: se a bala está virada para a direita e a partícula filha aponta para trás, ela esguicha
// para o lado de onde o tiro veio.
public class ParticleDetachFx : MonoBehaviour
{
    [Tooltip("Sistemas de partículas a tocar. Vazio = todos os ParticleSystem deste objeto e dos filhos.")]
    public ParticleSystem[] systems;

    [Tooltip("Tempo máximo (s) antes de recolher mesmo que ainda haja partículas vivas (trava para sistemas em loop).")]
    [Min(0.1f)] public float maxSeconds = 5f;

    [Tooltip("Ligado = ao se desgrudar volta ao tamanho original (a bala encolhe no fim da vida; sem isso a partícula nasceria minúscula).")]
    public bool restoreScale = true;

    Transform owner;
    Vector3 localPosition;
    Quaternion localRotation;
    Vector3 localScale;
    Vector3 originalWorldScale;
    bool detached;
    Coroutine routine;

    void Awake()
    {
        owner = transform.parent;
        localPosition = transform.localPosition;
        localRotation = transform.localRotation;
        localScale = transform.localScale;
        originalWorldScale = transform.lossyScale;

        if (systems == null || systems.Length == 0) systems = GetComponentsInChildren<ParticleSystem>(true);
        if (owner == null) Debug.LogWarning("ParticleDetachFx: este objeto precisa ser FILHO do objeto que vai ser devolvido ao pool (bala/inimigo).", this);
    }

    void OnEnable()
    {
        // O dono acabou de ser ativado (saiu do pool): garante que nada fique tocando.
        if (!detached) StopAndClear();
    }

    // Ligue a um UnityEvent (On Hit, On Death...).
    public void Play()
    {
        if (owner == null) return;

        // Se ainda estava tocando um uso anterior (dono reaproveitado muito rápido), recolhe e recomeça aqui.
        if (detached) Reattach();

        transform.SetParent(null, true);
        if (restoreScale) transform.localScale = originalWorldScale;
        detached = true;
        gameObject.SetActive(true);

        foreach (ParticleSystem ps in systems)
        {
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }

        routine = StartCoroutine(WaitAndReattach());
    }

    IEnumerator WaitAndReattach()
    {
        float elapsed = 0f;
        yield return null;
        while (elapsed < maxSeconds && AnyAlive())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        routine = null;
        Reattach();
    }

    bool AnyAlive()
    {
        foreach (ParticleSystem ps in systems)
        {
            if (ps != null && ps.IsAlive(true)) return true;
        }
        return false;
    }

    void StopAndClear()
    {
        if (systems == null) return;
        foreach (ParticleSystem ps in systems)
        {
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Reattach()
    {
        if (!detached) return;
        detached = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopAndClear();

        if (owner == null)
        {
            Destroy(gameObject); // o dono foi destruído (ex.: troca de cena): não sobra órfão
            return;
        }

        transform.SetParent(owner, false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = localScale;
    }
}
