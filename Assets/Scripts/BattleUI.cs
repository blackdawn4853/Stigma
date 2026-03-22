using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    [Header("몬스터 UI")]
    public Slider monsterHPBar;
    public TextMeshProUGUI monsterHPText;

    [Header("플레이어 UI")]
    public Slider playerHPBar;
    public TextMeshProUGUI playerHPText;

    [Header("마나 UI")]
    public TextMeshProUGUI manaText;

    [Header("방어도 UI")]
    public TextMeshProUGUI playerDefenseText;
    public TextMeshProUGUI monsterDefenseText;

    [Header("시선 게이지 UI")]
    public Slider gazeBar;
    public TextMeshProUGUI gazeText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateUI()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        monsterHPBar.value = (float)bm.monsterCurrentHp / bm.monsterData.maxHp;
        monsterHPText.text = $"{bm.monsterCurrentHp}/{bm.monsterData.maxHp}";

        playerHPBar.value = (float)bm.playerCurrentHp / bm.playerMaxHp;
        playerHPText.text = $"{bm.playerCurrentHp}/{bm.playerMaxHp}";

        manaText.text = $"Mana: {bm.currentMana}/{bm.maxMana}";

        if (playerDefenseText != null)
            playerDefenseText.text = bm.playerDefense > 0 ? $"방어 {bm.playerDefense}" : "";
        if (monsterDefenseText != null)
            monsterDefenseText.text = bm.monsterDefense > 0 ? $"방어 {bm.monsterDefense}" : "";

        if (gazeBar != null)
            gazeBar.value = (float)bm.gazeLevel / 100f;
        if (gazeText != null)
            gazeText.text = $"시선: {bm.gazeLevel}";
    }

    public void UpdateMonsterIntent()
    {
        if (MonsterIntent.Instance != null)
        {
            MonsterIntent.Instance.UpdateIntent(BattleManager.Instance.monsterNextAction);
            MonsterIntent.Instance.UpdateActiveTurns();
        }
    }
}