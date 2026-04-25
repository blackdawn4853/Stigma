using UnityEngine;

public enum GazeEffectType
{
    // 20 구간 (누적형)
    ForbiddenResonance,   // 금단의 감응
    SharpIntuition,       // 예민한 직감
    ThinBarrier,          // 얇은 방벽
    WeaknessTracking,     // 약점 추적

    // 40 구간 (누적형)
    AbyssalGrimoire,      // 심연의 독본
    Onslaught,            // 몰아치기
    BloodyBreath,         // 핏빛 호흡
    GapVision,            // 틈새 시야

    // 60 구간 (누적형)
    OpeningOmen,          // 개안 전조
    Gluttony,             // 폭식
    DeepContact,          // 깊은 접촉
    FirstStrike,          // 첫 일격

    // 80 구간 (누적형)
    OuterGodDescend80,    // 외신의 강림 (80)
    TornMoment,           // 찢긴 찰나
    DoomContract,         // 파멸 계약
    MadnessCycle,         // 광기 순환

    // 100 구간 (트리거형)
    OuterGodDescend100,   // 외신의 강림 (100)
    ThrottlingHand,       // 목을 조르는 손
    OpenEye,              // 개안
    AbyssalCommand,       // 심연의 명령
}

[CreateAssetMenu(fileName = "NewGazeEffect", menuName = "Card Game/Gaze Effect")]
public class GazeEffectData : ScriptableObject
{
    [Header("기본 정보")]
    public GazeEffectType effectType;
    public string displayName;
    [TextArea(2, 4)] public string buffDescription;
    [TextArea(2, 4)] public string debuffDescription;

    [Header("구간 설정")]
    [Tooltip("20 / 40 / 60 / 80 / 100")]
    public int threshold;
    [Tooltip("100은 true, 나머지는 false")]
    public bool isTriggered;

    [Header("아트 (나중에 교체)")]
    public Sprite icon;
}
