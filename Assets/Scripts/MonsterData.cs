using UnityEngine;

[CreateAssetMenu(fileName = "NewMonster", menuName = "Card Game/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("몬스터 기본 정보")]
    public string monsterName;
    public int maxHp;

    [Header("몬스터 카드 풀")]
    public CardData[] cardPool; // 이 몬스터가 쓸 수 있는 카드 목록

    [Header("몬스터 타입")]
    public MonsterType monsterType;

    public enum MonsterType
    {
        Normal,  // 일반 몬스터 (랜덤으로 카드 사용)
        Elite,   // 엘리트 (조건부 카드 사용)
        Boss     // 보스 (조건부 카드 사용)
    }

    // 몬스터가 다음에 쓸 카드 결정
    public CardData GetNextCard(int currentHp)
    {
        if (cardPool == null || cardPool.Length == 0)
        {
            Debug.LogWarning($"{monsterName} 의 카드풀이 비어있어!");
            return null;
        }

        switch (monsterType)
        {
            case MonsterType.Normal:
                // 일반 몬스터는 랜덤
                return cardPool[Random.Range(0, cardPool.Length)];

            case MonsterType.Elite:
            case MonsterType.Boss:
                // 체력 50% 이하면 80% 확률로 첫 번째 카드 사용
                // (나중에 보스마다 커스텀 로직 추가 가능)
                float hpRatio = (float)currentHp / maxHp;
                if (hpRatio <= 0.5f && Random.value <= 0.8f)
                    return cardPool[0];
                return cardPool[Random.Range(0, cardPool.Length)];

            default:
                return cardPool[Random.Range(0, cardPool.Length)];
        }
    }
}