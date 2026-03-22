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

    // 몬스터 행동 타입
    public enum ActionType
    {
        Attack,   // 공격
        Defend,   // 방어
        Buff,     // 버프 (공격력 증가)
        Debuff    // 디버프 (받는 데미지 증가)
    }

    [System.Serializable]
    public class MonsterAction
    {
        public ActionType actionType;
        public int value;       // 데미지 or 방어도 수치
        public int duration;    // 버프/디버프 지속 턴 (공격/방어는 0)
    }

    [Header("몬스터 행동 풀")]
    public MonsterAction[] actionPool;

    // 다음 행동 결정
    public MonsterAction GetNextAction()
    {
        if (actionPool == null || actionPool.Length == 0)
        {
            Debug.LogWarning($"{monsterName} 의 행동 풀이 비어있어!");
            // 기본 공격 반환
            return new MonsterAction { actionType = ActionType.Attack, value = 5 };
        }

        // 일단 랜덤으로 행동 선택
        return actionPool[Random.Range(0, actionPool.Length)];
    }
}