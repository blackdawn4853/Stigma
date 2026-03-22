using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterIntent : MonoBehaviour
{
    public static MonsterIntent Instance { get; private set; }

    [Header("Intent UI")]
    public GameObject intentObject;
    public TextMeshProUGUI intentText;
    public TextMeshProUGUI intentTurnText;
    public Image intentIcon;

    [Header("아이콘 색상")]
    public Color attackColor = new Color(0.8f, 0.1f, 0.1f);
    public Color defendColor = new Color(0.1f, 0.5f, 0.8f);
    public Color buffColor = new Color(0.8f, 0.6f, 0.1f);
    public Color debuffColor = new Color(0.5f, 0.1f, 0.8f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (intentObject != null)
            intentObject.SetActive(false);
    }

    public void UpdateIntent(MonsterData.MonsterAction action)
    {
        if (action == null)
        {
            if (intentObject != null)
                intentObject.SetActive(false);
            return;
        }

        if (intentObject != null)
            intentObject.SetActive(true);

        if (intentTurnText != null)
            intentTurnText.text = "";

        switch (action.actionType)
        {
            case MonsterData.ActionType.Attack:
                int displayDamage = action.value + (BattleManager.Instance != null ? BattleManager.Instance.monsterStrength : 0);
                if (intentText != null)
                    intentText.text = $"공격 {displayDamage}";
                if (intentIcon != null)
                    intentIcon.color = attackColor;
                break;

            case MonsterData.ActionType.Defend:
                if (intentText != null)
                    intentText.text = $"방어 {action.value}";
                if (intentIcon != null)
                    intentIcon.color = defendColor;
                break;

            case MonsterData.ActionType.Buff:
                if (intentText != null)
                    intentText.text = "강화 +5";
                if (intentIcon != null)
                    intentIcon.color = buffColor;
                if (intentTurnText != null)
                    intentTurnText.text = $"{action.duration}턴";
                break;

            case MonsterData.ActionType.Debuff:
                if (intentText != null)
                    intentText.text = "약화 -25%";
                if (intentIcon != null)
                    intentIcon.color = debuffColor;
                if (intentTurnText != null)
                    intentTurnText.text = $"{action.duration}턴";
                break;
        }
    }

    public void UpdateActiveTurns()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        if (bm.monsterStrengthTurns > 0 && intentTurnText != null)
            intentTurnText.text = $"{bm.monsterStrengthTurns}턴 남음";
        else if (bm.playerDebuffTurns > 0 && intentTurnText != null)
            intentTurnText.text = $"{bm.playerDebuffTurns}턴 남음";
    }
}