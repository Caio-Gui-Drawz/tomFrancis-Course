using System.Collections.Generic;
using UnityEngine;

// Marca um campo string como "nome de um muzzle flash": no Inspector vira uma lista com os flashes que existem no player.
public class MuzzleFlashNameAttribute : PropertyAttribute { }

// Fica no GunShootPoint (ou em qualquer pai dos prefabs de muzzle flash). Dá Play no flash escolhido pelo WeaponData.
// Cada flash é um filho com ParticleSystem; o NOME do GameObject é o que o WeaponData escolhe.
// Dica: desligue "Play On Awake" nos particles, senão eles tocam uma vez ao começar o jogo.
public class MuzzleFlashPlayer : MonoBehaviour
{
    [Tooltip("Ligado = antes de tocar um flash, para e limpa os outros (só um muzzle aparece por vez).")]
    public bool stopOthers = true;

    [Tooltip("Ligado = o flash gira para a direção do tiro (a mesma do braço/bala) a cada disparo. Desligado = fica na rotação em que foi desenhado.")]
    public bool rotateToAim = true;

    [Tooltip("Soma ao ângulo do tiro, em graus. O flash é considerado apontando para a DIREITA (+X) na rotação em que foi desenhado; se ele aponta para outro lado, corrija aqui (ex.: -90 se aponta para cima, 180 se aponta para a esquerda).")]
    public float angleOffset = 0f;

    [Tooltip("Liga o Render Alignment = Local nos particles dos flashes. Partículas em 'View' (o padrão) ignoram a rotação do objeto: o movimento gira, mas o desenho do sprite continua do mesmo jeito. Use se o flash não parece girar com a mira.")]
    public bool forceLocalAlignment = false;

    readonly Dictionary<ParticleSystem, Quaternion> baseRotation = new Dictionary<ParticleSystem, Quaternion>();
    readonly Dictionary<string, ParticleSystem> byName = new Dictionary<string, ParticleSystem>();
    readonly List<ParticleSystem> all = new List<ParticleSystem>();
    readonly HashSet<string> warned = new HashSet<string>();
    bool built;

    void Awake()
    {
        Build();
    }

    // Só os ParticleSystems "raiz" (sub-particles dentro de um flash tocam junto com o pai).
    public static List<ParticleSystem> FindRootSystems(Transform root)
    {
        List<ParticleSystem> result = new List<ParticleSystem>();
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            bool hasParentSystem = false;
            for (Transform t = ps.transform.parent; t != null && t != root; t = t.parent)
            {
                if (t.GetComponent<ParticleSystem>() != null) { hasParentSystem = true; break; }
            }
            if (!hasParentSystem) result.Add(ps);
        }
        return result;
    }

    void Build()
    {
        built = true;
        byName.Clear();
        all.Clear();
        baseRotation.Clear();
        foreach (ParticleSystem ps in FindRootSystems(transform))
        {
            all.Add(ps);
            baseRotation[ps] = ps.transform.rotation; // rotação desenhada = apontando para a direita (ver angleOffset)

            if (forceLocalAlignment)
            {
                foreach (ParticleSystemRenderer r in ps.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    r.alignment = ParticleSystemRenderSpace.Local;
                }
            }
            if (!byName.ContainsKey(ps.gameObject.name)) byName.Add(ps.gameObject.name, ps);
        }
    }

    // Toca o flash com esse nome (vazio = a arma não tem muzzle flash), mantendo a rotação desenhada.
    public void Play(string flashName)
    {
        Play(flashName, null);
    }

    // Igual, mas gira o flash para worldAngle (graus, espaço de mundo) se 'rotateToAim' estiver ligado.
    public void Play(string flashName, float worldAngle)
    {
        Play(flashName, (float?)worldAngle);
    }

    void Play(string flashName, float? worldAngle)
    {
        if (string.IsNullOrEmpty(flashName)) return;
        if (!built) Build();

        ParticleSystem ps;
        if (!byName.TryGetValue(flashName, out ps) || ps == null)
        {
            if (warned.Add(flashName)) Debug.LogWarning("MuzzleFlashPlayer: não há um flash chamado '" + flashName + "' dentro de '" + name + "'.", this);
            return;
        }

        if (stopOthers)
        {
            foreach (ParticleSystem other in all)
            {
                if (other != null && other != ps) other.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);

        if (rotateToAim && worldAngle.HasValue)
        {
            Quaternion baseRot;
            if (!baseRotation.TryGetValue(ps, out baseRot)) baseRot = ps.transform.rotation;
            ps.transform.rotation = Quaternion.Euler(0f, 0f, worldAngle.Value + angleOffset) * baseRot;
        }

        ps.Clear(true);
        ps.Play(true);
    }
}
