using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// 노드맵 드로잉 시스템 — 라인 데이터 보관 / 렌더 / 그리기 + 지우기 모드 처리.
// NodeMap 씬 진입 시 자동 부트스트랩 (MapDrawingUI 와 함께).
// 라인은 mapContainer 의 자식 GameObject 로 렌더되므로 스크롤 자동 따라감.
// 라인 데이터는 GameManager.drawingLines 와 동기화 (영구 세이브).
public class MapDrawingManager : MonoBehaviour
{
    public static MapDrawingManager Instance { get; private set; }

    public enum Mode { None, Drawing, Erasing }

    [Header("스타일")]
    [Tooltip("펜 굵기 (px).")]
    public float penThickness = 6f;
    [Tooltip("지우개 반경 (px). 라인의 점이 이 반경 안에 들어오면 라인 통째 삭제.")]
    public float eraserRadius = 28f;
    [Tooltip("드래그 중 새 점 추가 최소 거리 (px). 이보다 가까우면 점 추가 안 함.")]
    public float minPointDistance = 4f;

    [Header("색상 팔레트 — UI 와 동일하게 유지")]
    public Color colorRed = new Color(0.95f, 0.2f, 0.2f);
    public Color colorBlue = new Color(0.25f, 0.45f, 0.95f);
    public Color colorBlack = new Color(0.05f, 0.05f, 0.05f);
    public Color colorWhite = new Color(1f, 1f, 1f);

    public Mode currentMode { get; private set; } = Mode.None;
    public Color currentColor { get; private set; } = Color.black;

    // ─── 런타임 ──────────────────────────────────────────────────────
    private List<DrawLine> lines = new List<DrawLine>();
    // 각 라인의 segment GameObject 들 (lines 와 인덱스 1:1)
    private List<List<GameObject>> lineSegmentObjects = new List<List<GameObject>>();

    // 진행 중인 드로잉
    private DrawLine currentDrawingLine;
    private List<GameObject> currentDrawingSegments;

    private RectTransform mapContainer;
    private RectTransform linesContainer; // 라인 GO 의 부모 (mapContainer 의 자식)
    private RectTransform drawingOverlay; // 드래그 캡처 (mapContainer 의 자식, 가장 위)

    // ─── 부트스트랩 ──────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 이미 NodeMap 씬에 진입한 상태에서 도메인 리로드된 경우도 처리
        if (SceneManager.GetActiveScene().name == "NodeMap" && Instance == null)
            SpawnForNodeMap();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NodeMap" && Instance == null)
            SpawnForNodeMap();
    }

    static void SpawnForNodeMap()
    {
        GameObject go = new GameObject("MapDrawingManager");
        go.AddComponent<MapDrawingManager>();
        go.AddComponent<MapDrawingUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // mapContainer 찾기
        if (MapSceneManager.Instance != null)
            mapContainer = MapSceneManager.Instance.mapContainer;
        if (mapContainer == null)
        {
            Debug.LogError("[MapDrawingManager] mapContainer 못찾음 — MapSceneManager 가 먼저 Start 되어야 함");
            return;
        }

        BuildContainers();
        LoadFromGameManager();
        RebuildAllLines();
    }

    // ─── 컨테이너 구성 ───────────────────────────────────────────────
    void BuildContainers()
    {
        // 라인 컨테이너: mapContainer 의 pivot 에 anchor + sizeDelta=0 → 자식 segment 의 anchoredPosition 이
        // mapContainer-local 좌표와 1:1 매칭되도록.
        GameObject linesGO = new GameObject("DrawingLines", typeof(RectTransform));
        linesGO.transform.SetParent(mapContainer, false);
        linesContainer = (RectTransform)linesGO.transform;
        linesContainer.anchorMin = mapContainer.pivot;
        linesContainer.anchorMax = mapContainer.pivot;
        linesContainer.pivot = mapContainer.pivot;
        linesContainer.anchoredPosition = Vector2.zero;
        linesContainer.sizeDelta = Vector2.zero;

        // 드래그 캡처 오버레이: mapContainer 전체 stretch, raycastTarget 토글로 모드 제어.
        GameObject overlayGO = new GameObject("DrawingOverlay",
            typeof(RectTransform), typeof(Image));
        overlayGO.transform.SetParent(mapContainer, false);
        drawingOverlay = (RectTransform)overlayGO.transform;
        drawingOverlay.anchorMin = Vector2.zero;
        drawingOverlay.anchorMax = Vector2.one;
        drawingOverlay.offsetMin = Vector2.zero;
        drawingOverlay.offsetMax = Vector2.zero;
        Image overlayImg = overlayGO.GetComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0); // 완전 투명
        overlayImg.raycastTarget = false; // 모드 활성화 시에만 true
        drawingOverlay.SetAsLastSibling();

        DrawingOverlayHandler handler = overlayGO.AddComponent<DrawingOverlayHandler>();
        handler.manager = this;
    }

    // ─── 모드 제어 ───────────────────────────────────────────────────
    public void SetPencilMode(Color color)
    {
        currentMode = Mode.Drawing;
        currentColor = color;
        SetOverlayActive(true);
    }

    public void SetEraserMode()
    {
        currentMode = Mode.Erasing;
        SetOverlayActive(true);
    }

    public void SetNoneMode()
    {
        // 진행 중 드로잉이 있으면 마무리
        if (currentDrawingLine != null) FinalizeDrawingLine();
        currentMode = Mode.None;
        SetOverlayActive(false);
    }

    void SetOverlayActive(bool active)
    {
        if (drawingOverlay == null) return;
        Image img = drawingOverlay.GetComponent<Image>();
        if (img != null) img.raycastTarget = active;
    }

    public void ClearAll()
    {
        for (int i = 0; i < lineSegmentObjects.Count; i++)
            for (int j = 0; j < lineSegmentObjects[i].Count; j++)
                if (lineSegmentObjects[i][j] != null) Destroy(lineSegmentObjects[i][j]);
        lineSegmentObjects.Clear();
        lines.Clear();
        SaveDrawings();
    }

    // ─── 그리기 이벤트 (overlay handler 가 호출) ─────────────────────
    public void OnDrawStart(Vector2 mapLocalPos)
    {
        if (currentMode == Mode.Drawing)
        {
            currentDrawingLine = new DrawLine();
            currentDrawingLine.color = currentColor;
            currentDrawingLine.points.Add(mapLocalPos);
            currentDrawingSegments = new List<GameObject>();
        }
        else if (currentMode == Mode.Erasing)
        {
            EraseAt(mapLocalPos);
        }
    }

    public void OnDrawDrag(Vector2 mapLocalPos)
    {
        if (currentMode == Mode.Drawing && currentDrawingLine != null)
        {
            int n = currentDrawingLine.points.Count;
            if (n == 0)
            {
                currentDrawingLine.points.Add(mapLocalPos);
                return;
            }
            Vector2 last = currentDrawingLine.points[n - 1];
            if (Vector2.Distance(last, mapLocalPos) >= minPointDistance)
            {
                currentDrawingLine.points.Add(mapLocalPos);
                GameObject seg = CreateSegment(last, mapLocalPos, currentDrawingLine.color, penThickness);
                currentDrawingSegments.Add(seg);
            }
        }
        else if (currentMode == Mode.Erasing)
        {
            EraseAt(mapLocalPos);
        }
    }

    public void OnDrawEnd()
    {
        if (currentMode == Mode.Drawing && currentDrawingLine != null)
            FinalizeDrawingLine();
    }

    void FinalizeDrawingLine()
    {
        if (currentDrawingLine.points.Count >= 2)
        {
            lines.Add(currentDrawingLine);
            lineSegmentObjects.Add(currentDrawingSegments);
            SaveDrawings();
        }
        else
        {
            // 점 1개 이하 — 의미없는 클릭, 생성된 segment 정리
            if (currentDrawingSegments != null)
                foreach (var go in currentDrawingSegments)
                    if (go != null) Destroy(go);
        }
        currentDrawingLine = null;
        currentDrawingSegments = null;
    }

    // 지우개가 지나간 부분만 지움. 라인이 중간에서 끊기면 양쪽이 별개 라인으로 분할됨.
    public void EraseAt(Vector2 mapLocalPos)
    {
        bool changed = false;
        float r2 = eraserRadius * eraserRadius;

        // 역순 순회 — 분할로 늘어나는 라인은 끝에 추가하므로 인덱스 안전
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            DrawLine line = lines[i];
            bool anyHit = false;

            // 지우개에 닿은 점에서 청크 분할
            List<DrawLine> fragments = new List<DrawLine>();
            List<Vector2> chunk = new List<Vector2>();

            for (int j = 0; j < line.points.Count; j++)
            {
                Vector2 p = line.points[j];
                if ((p - mapLocalPos).sqrMagnitude <= r2)
                {
                    anyHit = true;
                    if (chunk.Count >= 2)
                        fragments.Add(new DrawLine { color = line.color, points = chunk });
                    chunk = new List<Vector2>();
                }
                else
                {
                    chunk.Add(p);
                }
            }
            if (chunk.Count >= 2)
                fragments.Add(new DrawLine { color = line.color, points = chunk });

            if (!anyHit) continue;
            changed = true;

            // 기존 segment GO 제거
            if (i < lineSegmentObjects.Count)
            {
                for (int k = 0; k < lineSegmentObjects[i].Count; k++)
                    if (lineSegmentObjects[i][k] != null) Destroy(lineSegmentObjects[i][k]);
            }

            if (fragments.Count == 0)
            {
                // 모든 점이 지워짐 → 라인 자체 삭제
                lines.RemoveAt(i);
                if (i < lineSegmentObjects.Count) lineSegmentObjects.RemoveAt(i);
            }
            else
            {
                // 첫 fragment 는 원본 자리에 교체, 나머지는 끝에 append
                lines[i] = fragments[0];
                if (i < lineSegmentObjects.Count)
                    lineSegmentObjects[i] = BuildSegmentsForLine(fragments[0]);
                else
                    lineSegmentObjects.Add(BuildSegmentsForLine(fragments[0]));

                for (int f = 1; f < fragments.Count; f++)
                {
                    lines.Add(fragments[f]);
                    lineSegmentObjects.Add(BuildSegmentsForLine(fragments[f]));
                }
            }
        }
        if (changed) SaveDrawings();
    }

    List<GameObject> BuildSegmentsForLine(DrawLine line)
    {
        List<GameObject> segs = new List<GameObject>();
        for (int j = 1; j < line.points.Count; j++)
            segs.Add(CreateSegment(line.points[j - 1], line.points[j], line.color, penThickness));
        return segs;
    }

    // ─── 렌더 ────────────────────────────────────────────────────────
    void RebuildAllLines()
    {
        // 기존 segment GO 제거
        for (int i = 0; i < lineSegmentObjects.Count; i++)
            for (int j = 0; j < lineSegmentObjects[i].Count; j++)
                if (lineSegmentObjects[i][j] != null) Destroy(lineSegmentObjects[i][j]);
        lineSegmentObjects.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            DrawLine line = lines[i];
            List<GameObject> segs = new List<GameObject>();
            for (int j = 1; j < line.points.Count; j++)
                segs.Add(CreateSegment(line.points[j - 1], line.points[j], line.color, penThickness));
            lineSegmentObjects.Add(segs);
        }
    }

    GameObject CreateSegment(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 mid = (from + to) * 0.5f;
        Vector2 dir = to - from;
        float length = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject seg = new GameObject("Seg",
            typeof(RectTransform), typeof(Image));
        seg.transform.SetParent(linesContainer, false);
        RectTransform rt = (RectTransform)seg.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(length, thickness);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        Image img = seg.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return seg;
    }

    // ─── 좌표 변환 ───────────────────────────────────────────────────
    public Vector2 ScreenToMapLocal(Vector2 screenPos, Camera cam)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContainer, screenPos, cam, out local);
        return local;
    }

    // ─── 세이브 / 로드 ───────────────────────────────────────────────
    void LoadFromGameManager()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.drawingLines == null)
            GameManager.Instance.drawingLines = new List<DrawLine>();
        // 동일 참조로 공유 → 양쪽이 같은 리스트를 본다
        lines = GameManager.Instance.drawingLines;
    }

    void SaveDrawings()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.drawingLines = lines;
        GameManager.Instance.Save();
    }
}

// 드로잉 오버레이의 포인터 이벤트 → 매니저로 위임
public class DrawingOverlayHandler : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public MapDrawingManager manager;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (manager == null) return;
        Vector2 local = manager.ScreenToMapLocal(eventData.position, eventData.pressEventCamera);
        manager.OnDrawStart(local);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (manager == null) return;
        Vector2 local = manager.ScreenToMapLocal(eventData.position, eventData.pressEventCamera);
        manager.OnDrawDrag(local);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (manager == null) return;
        manager.OnDrawEnd();
    }
}
