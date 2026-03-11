using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card")]
public class CardData : ScriptableObject
{
    [Header("카드 기본 정보")]
    public string cardName;
    public string description;
    public int manaCost;

    [Header("카드 등급")]
    public CardRarity rarity;

    [Header("카드 효과")]
    public CardEffectType effectType;

    [Header("수치")]
    public int value;        // 데미지, 방어막, 회복량 등
    public int hitCount = 1; // MultiHit에서 몇 번 때릴지 (기본 1)

    [Header("조건부 효과")]
    public CardCondition condition = CardCondition.None;
    public float conditionThreshold = 0.3f; // 조건 기준 (예: HP 30% 이하)

    public enum CardEffectType
    {
        Damage,       // 기본 데미지
        MultiHit,     // 여러번 공격 (value * hitCount)
        Execute,      // 적 HP가 conditionThreshold 이하일 때 즉사
        RageAttack,   // 내 HP가 낮을수록 데미지 증가
        Shield,       // 방어막 (다음 공격 1회 차단)
        Thorns,       // 반사 데미지 (공격받으면 value만큼 반사)
        Dodge,        // 다음 턴 공격 완전 회피
        WeakenEnemy,  // 적 약점 노출 (다음 공격 데미지 2배)
        Poison,       // 독 (매 턴 value 데미지)
        GainMana,     // 마나 즉시 회복
        Heal,         // HP 회복
        Taunt,        // 도발 (아무 효과 없음)
    }

    public enum CardCondition
    {
        None,         // 조건 없음
        LowHP,        // 내 HP가 conditionThreshold 이하일 때만 사용 가능
        EnemyLowHP,   // 적 HP가 conditionThreshold 이하일 때만 사용 가능
        HighMana,     // 마나가 conditionThreshold 이상일 때만 사용 가능
    }

    public enum CardRarity
    {
        Common,      // 일반
        Rare,        // 희귀
        Advanced,    // 고급
        Legendary,   // 전설
        Mythic       // 신화
    }

    // 조건 충족 여부 확인
    public bool IsConditionMet(int playerHp, int playerMaxHp, int monsterHp, int monsterMaxHp, int currentMana)
    {
        switch (condition)
        {
            case CardCondition.None:
                return true;
            case CardCondition.LowHP:
                return (float)playerHp / playerMaxHp <= conditionThreshold;
            case CardCondition.EnemyLowHP:
                return (float)monsterHp / monsterMaxHp <= conditionThreshold;
            case CardCondition.HighMana:
                return currentMana >= conditionThreshold;
            default:
                return true;
        }
    }

    // 등급별 색상
    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case CardRarity.Common:    return new Color(0.8f, 0.8f, 0.8f);
            case CardRarity.Rare:      return new Color(0.2f, 0.5f, 1.0f);
            case CardRarity.Advanced:  return new Color(0.6f, 0.2f, 1.0f);
            case CardRarity.Legendary: return new Color(1.0f, 0.7f, 0.0f);
            case CardRarity.Mythic:    return new Color(1.0f, 0.2f, 0.2f);
            default:                   return Color.white;
        }
    }
}