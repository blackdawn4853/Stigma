using UnityEngine;
using System.Collections.Generic;

public class NodeData
{
    public enum NodeType
    {
        Start,      // 시작
        Combat,     // 전투
        Shop,       // 상점
        RandomEvent, // 랜덤 이벤트
        Boss,       // 보스
        Brand       // 낙인 (카드 제거 또는 시선 효과 교체)
    }

    public NodeType nodeType;
    public int layer;           // 층 번호
    public int index;           // 같은 층에서의 인덱스
    public Vector2 position;    // 화면 위치

    public List<NodeData> nextNodes = new List<NodeData>();  // 다음 노드들
    public List<NodeData> prevNodes = new List<NodeData>();  // 이전 노드들

    public bool isVisited = false;
    public bool isAccessible = false; // 현재 선택 가능한지
}