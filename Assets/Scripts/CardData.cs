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

    [Header("타겟 필요 여부")]
    public bool requiresTarget = false; // true면 몬스터에게 드래그, false면 필드에 드래그

    [Header("수치")]
    public int value;

    public enum CardEffectType
    {
        Damage,      // 타격 - 몬스터에게 데미지
        Shield,      // 방어 - 방어도 획득
        // 추후 추가 예정
        // Buff,     // 플레이어 버프
        // Debuff,   // 몬스터 디버프
        // Heal,     // HP 회복
        // GainMana, // 마나 획득
    }

    public enum CardRarity
    {
        Common,      // 일반
        Rare,        // 희귀
        Advanced,    // 고급
        Legendary,   // 전설
        Mythic       // 신화
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