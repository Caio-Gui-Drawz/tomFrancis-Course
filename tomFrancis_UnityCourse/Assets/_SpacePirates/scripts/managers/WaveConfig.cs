using System;
using UnityEngine;

// Uma faixa da tabela "quem nasce em cada wave": o prefab (não o Spawner da cena, porque um asset
// não pode apontar para objetos de cena) e em quais waves ele pode aparecer.
[Serializable]
public class WaveRangeData
{
    [Tooltip("Prefab que nasce. O WaveManager procura, na cena, o Spawner que usa este prefab.")]
    public GameObject prefab;
    public int startWave = 1;
    [Tooltip("Deixe bem alto (ou não mexa) para 'nunca remove'.")]
    public int endWave = int.MaxValue;
    [Tooltip("Maior = mais frequente, menor = mais raro.")]
    public float weight = 1f;
}

// Configuração das waves guardada em um ASSET: vale para todas as cenas e a janela de Balance
// edita este arquivo (assim mudanças em Play mode também ficam salvas).
// Criar: Assets > Create > Cosmo Treasure > Wave Config (ou o menu de contexto do WaveManager).
[CreateAssetMenu(fileName = "WaveConfig", menuName = "Cosmo Treasure/Wave Config")]
public class WaveConfig : ScriptableObject
{
    [Header("Ritmo")]
    public int startingBudget = 30;
    public int budgetIncrementPerWave = 10;
    public float secondsBetweenSpawnsInWave = 0.5f;
    public float secondsBetweenWaves = 3f;

    [Tooltip("Quanto tempo depois de começar a jogar até a wave 1 realmente começar (janela de só voar, sem combate).")]
    public float secondsBeforeFirstWave = 3f;

    [Header("Quem nasce em cada wave")]
    public WaveRangeData[] ranges;

    [Header("Rodada de bônus entre waves")]
    [Tooltip("Prefabs de tesouro que nascem durante o intervalo entre uma wave e outra.")]
    public GameObject[] treasureBonusPrefabs;
    public float secondsBetweenBonusSpawns = 0.3f;
}
