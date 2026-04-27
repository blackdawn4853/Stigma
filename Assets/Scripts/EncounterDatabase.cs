using UnityEngine;
using System.Collections.Generic;

// 풀들을 모아두는 런 전체의 인카운터 마스터.
// 노드 클릭 시 PickForNode() → NextEncounter 에 저장 → BattleManager 가 읽어서 스폰.
// 새 런 시작 시 (GameManager.InitializeDeck) 모든 풀의 최근 메모리 리셋.
public class EncounterDatabase : MonoBehaviour
{
    public static EncounterDatabase Instance { get; private set; }

    // 다음 전투에 사용될 인카운터 (MapSceneManager 가 노드 클릭 시 세팅)
    public static EncounterData NextEncounter;

    [Header("Act 1 풀")]
    public EncounterPool act1Normal;
    public EncounterPool act1Elite;
    public EncounterPool act1Boss;

    // 향후 Act 2/3 풀도 같은 패턴으로 확장. (지금은 Act1 만)

    [Header("폴백 (풀 비었을 때)")]
    public EncounterData fallbackEncounter;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); }
    }

    public void ResetAllRecent()
    {
        if (act1Normal != null) act1Normal.ResetRecent();
        if (act1Elite != null) act1Elite.ResetRecent();
        if (act1Boss != null) act1Boss.ResetRecent();
    }

    // 노드 타입 + (현재) 액트 기반으로 풀 선택. 향후 액트 매개변수 추가 가능.
    public EncounterData PickForNode(NodeData.NodeType nodeType)
    {
        EncounterPool pool = SelectPool(nodeType);
        EncounterData picked = pool != null ? pool.Pick() : null;
        if (picked == null) picked = fallbackEncounter;
        return picked;
    }

    EncounterPool SelectPool(NodeData.NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeData.NodeType.Combat: return act1Normal;
            case NodeData.NodeType.Boss:   return act1Boss;
            // Elite NodeType 이 추후 추가되면 여기 케이스 추가
            default: return act1Normal;
        }
    }
}
