using UnityEngine;

[CreateAssetMenu(fileName = "NewMonster", menuName = "Card Game/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("몬스터 기본 정보")]
    public string monsterName;
    public int maxHp;

    [Header("몬스터 타입")]
    public MonsterType monsterType;

    public enum MonsterType
    {
        Normal,
        Elite,
        Boss
    }

    // 보스/특수 AI 식별자. None 이면 일반 몬스터처럼 actionPool 사용.
    // 신규 보스 추가 시 enum 항목 추가 + BattleManager.AttachBossAI 분기 추가.
    public enum BossAIType
    {
        None,
        Shoggoth,
    }

    // 몬스터 행동 타입
    public enum ActionType
    {
        Attack,           // 공격 (value=데미지)
        Defend,           // 방어 (value=방어도)
        Buff,             // 버프 (공격력 증가, duration 턴)
        Debuff,           // 디버프 (플레이어 받는 데미지 증가, duration 턴)
        AttackAndDebuff   // 공격 + 약화 동시 적용 (value=데미지, duration=약화 턴)
    }

    public enum ActionMode
    {
        Random,     // 매 턴 랜덤
        Sequential  // actionPool 순서대로 순환
    }

    [System.Serializable]
    public class MonsterAction
    {
        public ActionType actionType;
        public int value;       // 데미지 or 방어도 수치
        public int duration;    // 버프/디버프 지속 턴 (공격/방어는 0)
        public string displayName; // 표시 이름 (할퀴기/경계태세/급소타격 등). 비우면 actionType 기본 표시
    }

    [Header("행동 선택 방식 (BossAI 가 None 일 때만 사용)")]
    public ActionMode actionMode = ActionMode.Random;

    [Header("몬스터 행동 풀 (BossAI 가 None 일 때만 사용)")]
    public MonsterAction[] actionPool;

    [Header("보스/특수 AI (None 이 아니면 actionPool 무시하고 컴포넌트가 처리)")]
    public BossAIType bossAIType = BossAIType.None;

    [Header("시각 오버라이드 (비워두면 프리팹 기본 사용)")]
    public Sprite spriteOverride;
    public RuntimeAnimatorController animatorOverride;
    public Vector3 visualScale = Vector3.zero; // (0,0,0) 이면 프리팹 기본 사용
    [Tooltip("스프라이트 좌우 반전 — 새 sprite 가 의도와 반대 방향으로 그려졌을 때 켜기")]
    public bool flipSpriteX = false;

    // 다음 행동 결정 (랜덤 모드용 호환 헬퍼)
    public MonsterAction GetNextAction()
    {
        if (actionPool == null || actionPool.Length == 0)
        {
            Debug.LogWarning($"{monsterName} 의 행동 풀이 비어있어!");
            return new MonsterAction { actionType = ActionType.Attack, value = 5 };
        }
        return actionPool[Random.Range(0, actionPool.Length)];
    }
}
