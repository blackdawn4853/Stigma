using UnityEngine;

// 전투 UI 에서 공통으로 쓰는 그래픽 자산 (StatusType enum 에 안 들어가는 것들).
// BattleUI.Awake() 에서 인스펙터에 드래그된 스프라이트를 등록한다.
// 등록 안 된 항목은 기본 색상/무지 폴백.
public static class BattleIcons
{
    public static Sprite Defense { get; private set; }
    public static Sprite Strike { get; private set; }

    public static void RegisterDefense(Sprite s) { if (s != null) Defense = s; }
    public static void RegisterStrike(Sprite s) { if (s != null) Strike = s; }
}
