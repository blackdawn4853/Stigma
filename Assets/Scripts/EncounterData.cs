using UnityEngine;

// 한 전투의 고정 조합 정의: 어떤 몬스터들이 어디에 배치되는지 + 티어.
// 슬더스의 Encounter 와 동일 개념. 풀에서 뽑혀 BattleManager 가 스폰한다.
[CreateAssetMenu(fileName = "NewEncounter", menuName = "Card Game/Encounter")]
public class EncounterData : ScriptableObject
{
    public enum Tier
    {
        Normal,  // 일반 전투
        Elite,   // 엘리트
        Boss     // 보스
    }

    [Header("기본")]
    public string encounterName;
    public Tier tier = Tier.Normal;

    [Header("구성 몬스터 (배치 위치는 BattleManager.encounterAnchor 기준 오프셋)")]
    public EncounterEntry[] entries;

    [System.Serializable]
    public class EncounterEntry
    {
        public MonsterData data;
        public Vector2 positionOffset; // 앵커로부터의 X/Y 오프셋
        // 위치 오프셋은 슬더스 처럼 인카운터별로 하드코딩 — 자동 정렬하지 않는다.
    }
}
