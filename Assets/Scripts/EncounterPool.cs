using UnityEngine;
using System.Collections.Generic;

// 풀: 같은 티어/구간의 인카운터 후보들 + 가중치.
// 슬더스 처럼 최근 N 개를 회피하여 같은 전투가 연속으로 등장하지 않게 한다.
[CreateAssetMenu(fileName = "NewEncounterPool", menuName = "Card Game/Encounter Pool")]
public class EncounterPool : ScriptableObject
{
    [Header("기본")]
    public string poolName;
    public EncounterData.Tier tier = EncounterData.Tier.Normal;

    [Header("최근 N 개 회피 (0 = 안 함)")]
    public int avoidLastN = 1;

    [Header("후보 (가중치 합으로 룰렛)")]
    public PoolEntry[] entries;

    [System.Serializable]
    public class PoolEntry
    {
        public EncounterData encounter;
        [Min(0f)] public float weight = 1f;
    }

    // 최근에 나온 인카운터들 (런 단위, 풀별 EncounterDatabase 가 보관)
    [System.NonSerialized] private List<EncounterData> recent = new List<EncounterData>();

    public void ResetRecent() => recent.Clear();

    // 가중치 룰렛 + 최근 회피
    public EncounterData Pick()
    {
        if (entries == null || entries.Length == 0) return null;

        // 1차 후보: avoidLastN 에 해당하지 않는 항목
        var candidates = new List<PoolEntry>();
        float totalWeight = 0f;
        int avoidCount = Mathf.Min(avoidLastN, recent.Count);

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || e.encounter == null || e.weight <= 0f) continue;
            bool isRecent = false;
            for (int r = recent.Count - avoidCount; r < recent.Count; r++)
                if (recent[r] == e.encounter) { isRecent = true; break; }
            if (isRecent) continue;
            candidates.Add(e);
            totalWeight += e.weight;
        }

        // 모두 회피된 경우 (풀이 작거나 avoidLastN 가 너무 큼) → 회피 무시하고 전체 사용
        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            candidates.Clear();
            totalWeight = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.encounter == null || e.weight <= 0f) continue;
                candidates.Add(e);
                totalWeight += e.weight;
            }
        }

        if (candidates.Count == 0 || totalWeight <= 0f) return null;

        float roll = Random.value * totalWeight;
        float acc = 0f;
        EncounterData picked = candidates[candidates.Count - 1].encounter;
        for (int i = 0; i < candidates.Count; i++)
        {
            acc += candidates[i].weight;
            if (roll <= acc) { picked = candidates[i].encounter; break; }
        }

        // 최근 메모리 업데이트
        recent.Add(picked);
        int maxKeep = Mathf.Max(avoidLastN, 0);
        while (recent.Count > maxKeep) recent.RemoveAt(0);

        return picked;
    }
}
