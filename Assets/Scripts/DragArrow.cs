using UnityEngine;

public class DragArrow : MonoBehaviour
{
    public static DragArrow Instance { get; private set; }

    [Header("화살표 설정")]
    public LineRenderer lineRenderer;
    public Transform arrowHead;      // 화살표 머리 스프라이트
    public int curveResolution = 20; // 곡선 부드러움
    public Color arrowColor = Color.red;
    public float lineWidth = 0.1f;

    private bool isActive = false;
    private Vector3 startPos;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // LineRenderer 설정
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = arrowColor;
        lineRenderer.endColor = arrowColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.3f;
        lineRenderer.positionCount = curveResolution;
        lineRenderer.enabled = false;

        if (arrowHead != null)
            arrowHead.gameObject.SetActive(false);
    }

    public void ShowArrow(Vector3 start)
    {
        isActive = true;
        startPos = start;
        lineRenderer.enabled = true;
        if (arrowHead != null)
            arrowHead.gameObject.SetActive(true);
    }

    public void UpdateArrow(Vector3 mouseWorldPos)
    {
        if (!isActive) return;

        // 베지어 곡선 계산
        Vector3 controlPoint = new Vector3(
            (startPos.x + mouseWorldPos.x) / 2f,
            startPos.y + Vector3.Distance(startPos, mouseWorldPos) * 0.5f,
            0);

        for (int i = 0; i < curveResolution; i++)
        {
            float t = i / (float)(curveResolution - 1);
            Vector3 point = CalculateBezier(startPos, controlPoint, mouseWorldPos, t);
            lineRenderer.SetPosition(i, point);
        }

        // 화살표 머리 위치 + 방향
        if (arrowHead != null)
        {
            arrowHead.position = mouseWorldPos;
            Vector3 dir = (mouseWorldPos - CalculateBezier(startPos, controlPoint, mouseWorldPos, 0.95f)).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrowHead.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
    }

    public void HideArrow()
    {
        isActive = false;
        lineRenderer.enabled = false;
        if (arrowHead != null)
            arrowHead.gameObject.SetActive(false);
    }

    Vector3 CalculateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }
}