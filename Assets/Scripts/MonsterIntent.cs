using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterIntent : MonoBehaviour
{
    public static MonsterIntent Instance { get; private set; }

    [Header("미니 카드 UI 요소")]
    public GameObject intentCardObject;  // 미니 카드 전체 오브젝트
    public TextMeshProUGUI intentCardName;
    public TextMeshProUGUI intentCardDesc;
    public TextMeshProUGUI intentManaCost;
    public Image intentCardBackground;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        intentCardObject.SetActive(false);
    }

    public void UpdateIntent(CardData card)
    {
        if (card == null)
        {
            intentCardObject.SetActive(false);
            return;
        }

        intentCardObject.SetActive(true);
        intentCardName.text = card.cardName;
        intentCardDesc.text = card.description;
        intentManaCost.text = card.manaCost.ToString();

        // 카드 타입별 배경색
        if (intentCardBackground != null)
        {
            switch (card.effectType)
            {
                case CardData.CardEffectType.Damage:
                case CardData.CardEffectType.MultiHit:
                case CardData.CardEffectType.Execute:
                case CardData.CardEffectType.RageAttack:
                    intentCardBackground.color = new Color(0.7f, 0.1f, 0.1f); // 빨강
                    break;
                case CardData.CardEffectType.Taunt:
                    intentCardBackground.color = new Color(0.1f, 0.4f, 0.7f); // 파랑
                    break;
                case CardData.CardEffectType.Shield:
                case CardData.CardEffectType.Dodge:
                case CardData.CardEffectType.Thorns:
                    intentCardBackground.color = new Color(0.1f, 0.6f, 0.2f); // 초록
                    break;
                default:
                    intentCardBackground.color = new Color(0.3f, 0.3f, 0.3f); // 회색
                    break;
            }
        }
    }
}