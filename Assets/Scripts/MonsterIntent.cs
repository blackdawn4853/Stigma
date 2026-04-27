using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 레거시 단일 몬스터 인텐트 UI. 다중 몬스터 환경에서는 MonsterRuntimeUI 가 메인이며,
// 이 컴포넌트는 씬에 남아있어도 PrimaryMonster 의 인텐트를 미러링해 보여주는 역할만 한다.
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
        UpdateIntent(action, BattleManager.Instance != null ? BattleManager.Instance.PrimaryMonster : null);
    }

    public void UpdateIntent(MonsterData.MonsterAction action, Monster source)
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

        int monsterStrength = source != null ? source.strength : 0;

        // 인텐트는 기획서 기술명 대신 generic 명칭만 사용 (공격/방어/강화/약화)
        switch (action.actionType)
        {
            case MonsterData.ActionType.Attack:
            {
                int displayDamage = action.value + monsterStrength;
                if (GazeEffectManager.Instance != null)
                    displayDamage += GazeEffectManager.Instance.GetMonsterBonusAttack();
                if (intentText != null) intentText.text = $"공격 {displayDamage}";
                if (intentIcon != null) intentIcon.color = attackColor;
                break;
            }

            case MonsterData.ActionType.Defend:
                if (intentText != null) intentText.text = $"방어 {action.value}";
                if (intentIcon != null) intentIcon.color = defendColor;
                break;

            case MonsterData.ActionType.Buff:
                if (intentText != null) intentText.text = "강화";
                if (intentIcon != null) intentIcon.color = buffColor;
                if (intentTurnText != null) intentTurnText.text = $"{action.duration}턴";
                break;

            case MonsterData.ActionType.Debuff:
                if (intentText != null) intentText.text = "약화";
                if (intentIcon != null) intentIcon.color = debuffColor;
                if (intentTurnText != null) intentTurnText.text = $"{action.duration}턴";
                break;

            case MonsterData.ActionType.AttackAndDebuff:
            {
                int displayDmg2 = action.value + monsterStrength;
                if (GazeEffectManager.Instance != null)
                    displayDmg2 += GazeEffectManager.Instance.GetMonsterBonusAttack();
                if (intentText != null) intentText.text = $"공격 {displayDmg2}";
                if (intentIcon != null) intentIcon.color = attackColor;
                if (intentTurnText != null) intentTurnText.text = $"약화 {action.duration}턴";
                break;
            }
        }
    }

    // 레거시 호환: PrimaryMonster 의 버프/디버프 표시
    public void UpdateActiveTurns()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;
        Monster primary = bm.PrimaryMonster;
        if (primary == null || intentTurnText == null) return;

        if (primary.strengthTurns > 0)
            intentTurnText.text = $"{primary.strengthTurns}턴 남음";
        else if (bm.playerDebuffTurns > 0)
            intentTurnText.text = $"{bm.playerDebuffTurns}턴 남음";
    }
}
