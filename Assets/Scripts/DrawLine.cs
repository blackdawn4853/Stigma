using System;
using System.Collections.Generic;
using UnityEngine;

// 노드맵에 그려진 한 개의 라인. 점 좌표는 mapContainer 의 local 좌표 (pivot 기준).
// JsonUtility 직렬화 가능 — GameManager.SaveData 에 List<DrawLine> 으로 저장됨.
[Serializable]
public class DrawLine
{
    public List<Vector2> points = new List<Vector2>();
    public Color color = Color.black;
}
