using UnityEngine;

public class DragArrow : MonoBehaviour
{
    public static DragArrow Instance { get; private set; }

    [Header("화살표 설정")]
    public LineRenderer lineRenderer;
    public Transform arrowHead;      // 화살표 머리 스프라이트
    public int curveResolution = 28; // 곡선 부드러움
    public Color arrowColor = new Color(0.95f, 0.22f, 0.2f, 1f);   // 기본(무효) 빨강
    public Color validColor = new Color(1f, 0.84f, 0.32f, 1f);     // 유효 타겟 위 = 금빛
    // ⚠ 기존 lineWidth(0.1 직렬화) 대신 새 필드 — 직렬값이 없어 이 기본값(굵게)이 적용됨
    public float arrowWidth = 0.22f;
    [Tooltip("흐르는 점선 속도")]
    public float flowSpeed = 1.6f;
    [Tooltip("점선 한 칸의 월드 크기 (작을수록 촘촘)")]
    public float dashWorldSize = 0.55f;
    [Tooltip("몬스터/플레이어(=10) 보다 위에 그려지도록 충분히 큰 값. 클로즈업 pop(=100) 보다는 작게.")]
    public int sortingOrder = 50;

    private bool isActive = false;
    private Vector3 startPos;
    private float scroll;
    private Color curColor;
    private Monster currentTarget;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.mainTexture = MakeDashTexture();
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.numCapVertices = 4;     // 둥근 끝
        lineRenderer.numCornerVertices = 4;
        lineRenderer.startWidth = arrowWidth;
        lineRenderer.endWidth = arrowWidth * 0.45f;
        lineRenderer.positionCount = curveResolution;
        lineRenderer.enabled = false;
        lineRenderer.sortingOrder = sortingOrder;

        if (arrowHead != null)
        {
            arrowHead.gameObject.SetActive(false);
            var headSr = arrowHead.GetComponent<SpriteRenderer>();
            if (headSr != null) headSr.sortingOrder = sortingOrder + 1;
        }
        ApplyColor(arrowColor);
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

        // 베지어 곡선
        Vector3 controlPoint = new Vector3(
            (startPos.x + mouseWorldPos.x) / 2f,
            startPos.y + Vector3.Distance(startPos, mouseWorldPos) * 0.5f,
            0);

        for (int i = 0; i < curveResolution; i++)
        {
            float t = i / (float)(curveResolution - 1);
            lineRenderer.SetPosition(i, CalculateBezier(startPos, controlPoint, mouseWorldPos, t));
        }

        // 흐르는 점선 — 길이에 맞춰 타일 수 결정 + 스크롤
        float len = Vector3.Distance(startPos, mouseWorldPos);
        float tiles = Mathf.Max(1f, len / Mathf.Max(0.05f, dashWorldSize));
        scroll -= Time.unscaledDeltaTime * flowSpeed;
        lineRenderer.material.mainTextureScale = new Vector2(tiles, 1f);
        lineRenderer.material.mainTextureOffset = new Vector2(scroll, 0f);

        // 화살표 머리 위치 + 방향
        if (arrowHead != null)
        {
            arrowHead.position = mouseWorldPos;
            Vector3 dir = (mouseWorldPos - CalculateBezier(startPos, controlPoint, mouseWorldPos, 0.95f)).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrowHead.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        // 타겟 감지 + 하이라이트 + 색 전환
        Monster m = FindMonsterAt(mouseWorldPos);
        SetTarget(m);
        ApplyColor(m != null ? validColor : arrowColor);
    }

    public void HideArrow()
    {
        isActive = false;
        lineRenderer.enabled = false;
        if (arrowHead != null)
            arrowHead.gameObject.SetActive(false);
        SetTarget(null);
    }

    // ── 타겟 하이라이트 ──────────────────────────────────────────
    static Monster FindMonsterAt(Vector3 worldPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null || !hit.CompareTag("Monster")) return null;
        Monster m = hit.GetComponent<Monster>();
        if (m == null) m = hit.GetComponentInParent<Monster>();
        return (m != null && m.IsAlive) ? m : null;
    }

    void SetTarget(Monster m)
    {
        if (m == currentTarget) return;
        if (currentTarget != null)
        {
            var hOld = currentTarget.GetComponent<MonsterTargetHighlight>();
            if (hOld != null) hOld.Hide();
        }
        currentTarget = m;
        if (currentTarget != null)
            MonsterTargetHighlight.Ensure(currentTarget).Show();
    }

    void ApplyColor(Color c)
    {
        curColor = c;
        if (lineRenderer != null)
        {
            Color tail = c; tail.a = 0.45f;   // 꼬리는 옅게
            lineRenderer.startColor = tail;
            lineRenderer.endColor = c;         // 머리쪽 진하게
        }
        if (arrowHead != null)
        {
            var headSr = arrowHead.GetComponent<SpriteRenderer>();
            if (headSr != null) headSr.color = c;
        }
    }

    Texture2D MakeDashTexture()
    {
        // 가로 방향 점선 (앞쪽 채움 + 뒤쪽 투명)
        int w = 16;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w];
        for (int x = 0; x < w; x++)
        {
            float a = x < 10 ? 1f : 0f;          // 10칸 채움 / 6칸 빔
            px[x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    Vector3 CalculateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }
}

// 타겟 몬스터 뒤에 뜨는 금빛 실루엣 글로우 (맥동). 드래그로 가리키는 동안만 표시.
// 몬스터 본체 색/스케일은 건드리지 않음 — 뒤에 깔리는 글로우만 토글해 충돌 방지.
public class MonsterTargetHighlight : MonoBehaviour
{
    SpriteRenderer glow;
    SpriteRenderer src;
    bool shown;
    float t;

    public static MonsterTargetHighlight Ensure(Monster m)
    {
        var h = m.GetComponent<MonsterTargetHighlight>();
        if (h == null) h = m.gameObject.AddComponent<MonsterTargetHighlight>();
        h.Build(m);
        return h;
    }

    void Build(Monster m)
    {
        if (glow != null) return;
        src = m.spriteRenderer != null ? m.spriteRenderer : m.GetComponentInChildren<SpriteRenderer>();
        if (src == null) return;

        var go = new GameObject("TargetGlow");
        go.transform.SetParent(src.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 1.12f;     // 살짝 큰 실루엣 = 외곽 글로우 느낌
        glow = go.AddComponent<SpriteRenderer>();
        glow.sprite = src.sprite;
        glow.flipX = src.flipX;
        glow.sortingLayerID = src.sortingLayerID;
        glow.sortingOrder = src.sortingOrder - 1;          // 본체 뒤
        glow.color = new Color(1f, 0.84f, 0.32f, 0f);
        glow.enabled = false;
    }

    public void Show() { shown = true; if (glow != null) glow.enabled = true; }

    public void Hide()
    {
        shown = false;
        if (glow != null) glow.enabled = false;
    }

    void Update()
    {
        if (!shown || glow == null) return;
        // 본체 스프라이트/방향 동기화 (피격 플린치로 바뀌어도 따라가게)
        if (src != null) { glow.sprite = src.sprite; glow.flipX = src.flipX; }
        t += Time.unscaledDeltaTime;
        float a = 0.4f + Mathf.Sin(t * 6f) * 0.22f;        // 맥동
        glow.color = new Color(1f, 0.84f, 0.32f, a);
    }
}
