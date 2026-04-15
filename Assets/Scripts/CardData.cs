using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Card Game/Card")]
public class CardData : ScriptableObject
{
    [Header("카드 기본 정보")]
    public string cardName;
    public string description;
    public int manaCost;

    [Header("카드 타입")]
    public CardType cardType;

    [Header("카드 등급")]
    public CardRarity rarity;

    [Header("카드 효과")]
    public CardEffectType effectType;

    [Header("타겟 필요 여부")]
    public bool requiresTarget = false;

    [Header("수치")]
    public int value;   // 주 수치 (데미지, 방어도 등)
    public int value2;  // 보조 수치 (콤보 방어도, 최대 데미지, 히트 수 등)
    public int value3;  // 3차 수치 (지속 턴 수, 재생 턴 수 등)

    [Header("시선 변화량")]
    public int gazeChange = 0; // 양수=상승, 음수=감소

    public enum CardType
    {
        Attack,    // 공격
        Skill,     // 스킬
        Forbidden, // 금단 (크툴루)
        Power      // 지속
    }

    public enum CardEffectType
    {
        Damage,             // 기본 타격 (value=데미지)
        Shield,             // 방어도 (value=방어도)
        Draw,               // 드로우 (value=장수)
        GazeChange,         // 시선 변화만
        DamageAndShield,    // 공수 (value=데미지, value2=방어도)
        MultiHit,           // 연속공격 (value=1회 데미지, value2=횟수)
        PenetratingDamage,  // 관통공격 - 방어도 무시 (value=데미지)
        RandomDamage,       // 랜덤 데미지 (value=최소, value2=최대)
        StrengthBuff,       // 힘 버프 (value=증가량, value2=지속턴)
        DrawAndReduceMana,  // 가속 (value=드로우장수, value2=다음턴마나감소)
        ShieldAndDraw,      // 긴급회피 (value=방어도, value2=드로우장수)
        Heal,               // 회복 (value=회복량, value2=최대체력감소)
        AllDamage,          // 전체공격 (value=데미지)
        AllMultiHit,        // 전체 다중공격 (value=1회데미지, value2=횟수)
        DamageSelfDamage,   // 살점폭발 (value=데미지, value2=자해, value3=재생턴)
        ImmunityShield,     // 절대방어 (value=방어도, 디버프면역)
        RandomCardUse,      // 이성붕괴 (value=카드장수, 랜덤사용)
    }

    public enum CardRarity
    {
        Common,
        Rare,
        Advanced,
        Legendary,
        Mythic
    }

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