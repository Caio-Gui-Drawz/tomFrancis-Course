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

// Um item da fase de loot: o prefab e quantos nascem.
[Serializable]
public class LootItem
{
    [Tooltip("Prefab que nasce. Precisa existir um Spawner na cena para ele (o mesmo esquema das waves).")]
    public GameObject prefab;
    [Min(0)] public int count = 1;
}

// Pausa de loot entre uma wave e outra: sem inimigos, os prêmios sobem e o fundo desacelera.
// Desligada por padrão — nesse caso o intervalo continua sendo a rodada de bônus antiga.
[Serializable]
public class LootPhaseData
{
    public bool enabled = false;

    [Tooltip("Espera os inimigos da wave sumirem (mortos ou fora da tela) antes de começar a fase de loot.")]
    public bool waitForEnemiesToClear = true;
    [Tooltip("Se ainda sobrar inimigo depois deste tempo, a fase de loot começa mesmo assim.")]
    [Min(1f)] public float clearEnemiesTimeoutSeconds = 10f;

    [Tooltip("Duração planejada da fase de loot (substitui 'Seconds Between Waves' quando ligada). Com 'Termina antes se tudo for pego' ligado ela só define o espaçamento dos itens; o limite real é o tempo de segurança abaixo.")]
    [Min(1f)] public float durationSeconds = 6f;

    [Tooltip("Os prêmios nascem espalhados nesta fração inicial da fase; o resto é tempo para pegar.")]
    [Range(0.3f, 1f)] public float dropWindowFraction = 0.7f;

    [Tooltip("Tempo mínimo entre um item e o próximo. Evita itens colados (principalmente com os itens lentos).")]
    [Min(0.05f)] public float minSecondsBetweenDrops = 0.7f;

    [Tooltip("Ligado = a fase termina assim que TODOS os itens sumirem (pegos ou saíram da tela). A duração vira o tempo máximo.")]
    public bool endWhenAllCollected = true;
    [Tooltip("Com 'Termina antes se tudo for pego': tempo máximo de espera DEPOIS que o último item nasceu. Só serve para a fase não travar se algum item ficar preso.")]
    [Min(1f)] public float safetyTimeoutSeconds = 15f;
    [Tooltip("Mesmo que não sobre nenhum item, a fase dura pelo menos isto (evita um piscar).")]
    [Min(0f)] public float minSecondsBeforeEarlyEnd = 1.5f;

    [Tooltip("Velocidade dos itens da fase de loot (1 = normal). Vale para movimentos por Rigidbody (Straight/Axis) e Sine.")]
    [Range(0.1f, 1f)] public float itemSpeedMultiplier = 0.5f;

    [Header("Cura")]
    [Tooltip("Sempre nascem (ex.: 1 medkit).")]
    public LootItem[] healItems;
    [Tooltip("Se a vida do player estiver abaixo disso ao fim da wave, vêm itens de cura extras.")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.5f;
    [Tooltip("Quantas unidades EXTRA do primeiro item de cura nascem quando a vida está baixa.")]
    [Min(0)] public int extraHealsWhenLow = 1;

    [Header("Arma especial")]
    [Tooltip("Caixas de arma (ex.: shotgun, laser).")]
    public LootItem[] weaponItems;
    [Tooltip("Primeira wave concluída que dá arma.")]
    [Min(1)] public int weaponFirstWave = 2;
    [Tooltip("Depois da primeira, a arma vem a cada N waves (1 = toda wave).")]
    [Min(1)] public int weaponEveryNWaves = 2;

    [Header("Recompensa por desempenho")]
    [Tooltip("Tesouros etc. A quantidade é 'Count' x desempenho (1 - dano sofrido na wave / vida máxima).")]
    public LootItem[] performanceItems;
    [Tooltip("Unidades EXTRA do primeiro item de desempenho se o player não tomou nenhum dano na wave.")]
    [Min(0)] public int flawlessBonusCount = 2;
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

    [Header("Fase de loot entre waves")]
    public LootPhaseData lootPhase = new LootPhaseData();
}
