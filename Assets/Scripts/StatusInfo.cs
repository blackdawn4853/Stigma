using UnityEngine;
using System.Collections.Generic;

// 전투 중 표시되는 버프/디버프 카탈로그.
// 새 상태 추가 시 enum 에 항목 추가 후 Get() 에 데이터 분기만 더하면 된다.
public enum StatusType
{
    Strength,
    Weak,
    Regen,
}

public static class StatusInfo
{
    public struct Data
    {
        public string displayName;
        public string description;
        public Color tintColor;
    }

    public static Data Get(StatusType t)
    {
        switch (t)
        {
            case StatusType.Strength:
                return new Data {
                    displayName = "근력",
                    description = "공격 시 데미지가 수치만큼 추가됩니다.",
                    tintColor = new Color(1f, 0.55f, 0.2f)
                };
            case StatusType.Weak:
                return new Data {
                    displayName = "약화",
                    description = "주는 데미지가 25% 감소합니다.",
                    tintColor = new Color(0.7f, 0.4f, 1f)
                };
            case StatusType.Regen:
                return new Data {
                    displayName = "재생",
                    description = "턴 종료 시 체력을 일정량 회복합니다.",
                    tintColor = new Color(0.4f, 1f, 0.5f)
                };
        }
        return new Data { displayName = "?", description = "", tintColor = Color.white };
    }

    // 아이콘은 BattleUI 의 인스펙터에 드래그 → BattleUI.Awake() 에서 RegisterSprite() 호출.
    // 등록 안 된 타입은 null 반환 → StatusIconUI 가 색상 사각형 폴백.
    private static readonly Dictionary<StatusType, Sprite> spriteMap = new Dictionary<StatusType, Sprite>();

    public static void RegisterSprite(StatusType t, Sprite sprite)
    {
        if (sprite != null) spriteMap[t] = sprite;
    }

    public static Sprite GetSprite(StatusType t)
    {
        spriteMap.TryGetValue(t, out var s);
        return s;
    }
}
