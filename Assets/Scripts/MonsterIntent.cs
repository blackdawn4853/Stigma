using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterIntent : MonoBehaviour
{
    public static MonsterIntent Instance { get; private set; }

    [Header("Intent UI")]
    public GameObject intentObject;
    public TextMeshProUGUI intentText;
    public Image intentIcon;

    [Header("아이콘 색상")]
    public Color attackColor = new Color(0.8f, 0.1f, 0.1f);   // 빨강
    public Color defendColor = new Color(0.1f, 0.5f, 0.8f);   // 파랑
    public Color buffColor = new Color(0.8f, 0.6f, 0.1f);     // 노랑
    public Color debuffColor = new Color(0.5f, 0.1f, 0.8f);   // 보라

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

        switch (action.actionType)
        {
            case MonsterData.ActionType.Attack:
                if (intentText != null)
                    intentText.text = $"공격 {action.value}";
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
                    intentText.text = $"강화 {action.duration}턴";
                if (intentIcon != null)
                    intentIcon.color = buffColor;
                break;

            case MonsterData.ActionType.Debuff:
                if (intentText != null)
                    intentText.text = $"약화 {action.duration}턴";
                if (intentIcon != null)
                    intentIcon.color = debuffColor;
                break;
        }
    }
}