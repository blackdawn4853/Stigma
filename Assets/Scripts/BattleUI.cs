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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        
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
    }

    public void UpdateMonsterIntent()
    {
        if (MonsterIntent.Instance != null)
        {
            MonsterIntent.Instance.UpdateIntent(BattleManager.Instance.monsterNextCard);
        }
    }
}