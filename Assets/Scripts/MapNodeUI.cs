using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// 노드맵 노드 비주얼 (전부 런타임 절차생성 — 씬/프리팹 에셋 미수정).
//  원형 노드 + 타입색 프레임 + 글로우(갈 수 있는 곳 호흡) + 현재위치 링 + 방문 어둡게 + 보스 강조 + 타입 엠블럼/라벨.
public class MapNodeUI : MonoBehaviour
{
    [Header("UI 요소 (프리팹 참조 — 일부는 런타임에 재구성)")]
    public Image nodeImage;
    public TextMeshProUGUI nodeTypeText;
    public GameObject visitedMark;

    // 음산한 다크 팔레트 (밝은 원색은 장난스러워서 톤다운)
    static readonly Color cStart = new Color(0.45f, 0.62f, 0.48f);
    static readonly Color cCombat = new Color(0.66f, 0.26f, 0.24f);
    static readonly Color cShop = new Color(0.40f, 0.55f, 0.66f);
    static readonly Color cEvent = new Color(0.66f, 0.56f, 0.32f);
    static readonly Color cBrand = new Color(0.56f, 0.40f, 0.66f);
    static readonly Color cBoss = new Color(0.74f, 0.18f, 0.22f);
    static readonly Color cVisited = new Color(0.34f, 0.34f, 0.38f);

    NodeData nodeData;
    bool isCurrent;
    bool built;
    bool glowActive;

    Image glow, baseDisc, rim, currentRing;
    RectTransform glowRT, currentRingRT;
    readonly List<Image> emblemParts = new List<Image>();

    static Sprite _circle, _glow, _ring;

    // ── 진입 ─────────────────────────────────────────────────────
    public void Setup(NodeData data)
    {
        nodeData = data;
        BuildDecorations();
        UpdateVisual();
    }

    public void SetCurrent(bool v) => isCurrent = v;
    public NodeData GetNodeData() => nodeData;

    void Update()
    {
        if (!built) return;

        // 갈 수 있는 노드: 글로우 호흡
        if (glowActive && glow != null)
        {
            float b = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var c = glow.color; c.a = Mathf.Lerp(0.22f, 0.5f, b); glow.color = c;
            glowRT.localScale = Vector3.one * Mathf.Lerp(0.95f, 1.12f, b);
        }

        // 현재 위치: 링 펄스
        if (isCurrent && currentRing != null && currentRing.gameObject.activeSelf)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            currentRingRT.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, p);
            var c = currentRing.color; c.a = Mathf.Lerp(0.55f, 1f, p); currentRing.color = c;
        }
    }

    // ── 데코 구축 ────────────────────────────────────────────────
    void BuildDecorations()
    {
        if (built) return;
        built = true;

        var rootRT = (RectTransform)transform;
        float s = rootRT.sizeDelta.x > 1f ? rootRT.sizeDelta.x : 70f;
        bool boss = nodeData != null && nodeData.nodeType == NodeData.NodeType.Boss;
        float k = boss ? 1.55f : 1f; // 보스 강조

        // 루트 이미지: 클릭 영역(투명) — 메시 컬링 끄고 raycast 유지
        var rootImg = nodeImage != null ? nodeImage : GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = GetCircle();
            rootImg.color = new Color(1f, 1f, 1f, 0f);
            rootImg.raycastTarget = true;
        }
        var cr = GetComponent<CanvasRenderer>();
        if (cr != null) cr.cullTransparentMesh = false;

        // 마우스 호버 시 확대 (현재 스케일 기준)
        if (GetComponent<HoverScale>() == null)
        {
            var hov = gameObject.AddComponent<HoverScale>();
            hov.hoverScale = 1.13f;
        }

        // 자식 렌더 순서: glow → 본체(돌) → 룬 테두리 → emblem → currentRing
        glow = MakeChild("Glow", GetGlow(), s * 1.7f * k);
        glowRT = glow.rectTransform;

        baseDisc = MakeChild("Base", GetCircle(), s * 0.95f * k);  // 어두운 돌 본체
        rim = MakeChild("Rim", GetRing(), s * 0.97f * k);          // 타입색 룬 테두리

        BuildEmblem(nodeData != null ? nodeData.nodeType : NodeData.NodeType.Combat, s * k);

        currentRing = MakeChild("CurrentRing", GetRing(), s * 1.3f * k);
        currentRingRT = currentRing.rectTransform;
        currentRing.gameObject.SetActive(false);

        // 라벨: 노드 아래로 이동 + 스타일
        if (nodeTypeText != null)
        {
            var lrt = nodeTypeText.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -4f);
            lrt.sizeDelta = new Vector2(s * 2.8f, 26f);
            nodeTypeText.fontSize = boss ? 19 : 15;
            nodeTypeText.fontStyle = FontStyles.Bold;
            nodeTypeText.alignment = TextAlignmentOptions.Center;
            nodeTypeText.enableWordWrapping = false;
            nodeTypeText.raycastTarget = false;
            nodeTypeText.transform.SetAsLastSibling();
        }

        if (visitedMark != null) visitedMark.SetActive(false); // 자체 데코로 표현
    }

    Image MakeChild(string name, Sprite sprite, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // 타입별 엠블럼 (라이트 컬러 도형) — emblemParts 에 모아 색 일괄 변경
    void BuildEmblem(NodeData.NodeType type, float s)
    {
        var holder = new GameObject("Emblem", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        var hrt = holder.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(s, s);

        float u = s * 0.42f;
        switch (type)
        {
            case NodeData.NodeType.Start:
                Dot(holder.transform, u * 0.6f);
                break;
            case NodeData.NodeType.Combat: // 칼 교차 (X)
                Bar(holder.transform, new Vector2(u * 1.1f, u * 0.15f), 45f);
                Bar(holder.transform, new Vector2(u * 1.1f, u * 0.15f), -45f);
                break;
            case NodeData.NodeType.Shop: // 동전 (링)
                RingShape(holder.transform, u * 1.0f);
                break;
            case NodeData.NodeType.RandomEvent: // 다이아
                Diamond(holder.transform, u * 0.78f);
                break;
            case NodeData.NodeType.Brand: // 8각 별 (낙인 인장)
                Diamond(holder.transform, u * 0.85f, 45f);
                Diamond(holder.transform, u * 0.85f, 0f);
                break;
            case NodeData.NodeType.Boss: // 눈 (링 + 중앙 점)
                RingShape(holder.transform, u * 1.15f);
                Dot(holder.transform, u * 0.42f);
                break;
        }
    }

    void Dot(Transform parent, float size)
    {
        var img = MakeChildOf(parent, "Dot", GetCircle(), new Vector2(size, size), 0f);
        emblemParts.Add(img);
    }

    void Bar(Transform parent, Vector2 size, float rot)
    {
        var img = MakeChildOf(parent, "Bar", null, size, rot);
        emblemParts.Add(img);
    }

    void Diamond(Transform parent, float size, float rot = 45f)
    {
        var img = MakeChildOf(parent, "Diamond", null, new Vector2(size, size), rot);
        emblemParts.Add(img);
    }

    void RingShape(Transform parent, float size)
    {
        var img = MakeChildOf(parent, "Ring", GetRing(), new Vector2(size, size), 0f);
        emblemParts.Add(img);
    }

    Image MakeChildOf(Transform parent, string name, Sprite sprite, Vector2 size, float rot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        var img = go.GetComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // ── 비주얼 갱신 ──────────────────────────────────────────────
    public void UpdateVisual()
    {
        if (nodeData == null) return;
        if (!built) BuildDecorations();

        if (nodeTypeText != null) nodeTypeText.text = TypeLabel(nodeData.nodeType);

        Color type = TypeColor(nodeData.nodeType);
        bool visited = nodeData.isVisited;
        bool accessible = nodeData.isAccessible && !visited;

        Color stone, rimC, emblemC, labelC;
        bool glowOn;

        if (visited)
        {
            stone = new Color(0.11f, 0.11f, 0.13f, 0.96f);
            rimC = Mul(cVisited, 0.9f);
            emblemC = new Color(0.46f, 0.46f, 0.5f, 0.75f);
            labelC = new Color(0.52f, 0.52f, 0.57f, 1f);
            glowOn = false;
        }
        else if (!nodeData.isAccessible)
        {
            stone = new Color(0.07f, 0.06f, 0.09f, 0.96f);
            rimC = Mul(type, 0.42f);
            emblemC = Fade(Mul(type, 0.7f), 0.7f);
            labelC = new Color(0.48f, 0.48f, 0.54f, 1f);
            glowOn = false;
        }
        else
        {
            stone = new Color(0.12f + type.r * 0.05f, 0.11f + type.g * 0.04f, 0.14f + type.b * 0.05f, 0.98f);
            rimC = type;
            emblemC = Color.Lerp(type, Color.white, 0.4f);
            labelC = new Color(0.92f, 0.9f, 0.95f, 1f);
            glowOn = true;
        }

        if (baseDisc != null) baseDisc.color = stone;
        if (rim != null) rim.color = rimC;
        foreach (var e in emblemParts) if (e != null) e.color = emblemC;
        if (nodeTypeText != null) nodeTypeText.color = labelC;

        glowActive = glowOn;
        if (glow != null)
        {
            glow.gameObject.SetActive(glowOn);
            if (glowOn) glow.color = new Color(type.r, type.g, type.b, 0.4f);
        }

        if (currentRing != null)
        {
            bool showRing = isCurrent && !visited;
            currentRing.gameObject.SetActive(showRing);
            if (showRing) currentRing.color = new Color(0.98f, 0.92f, 0.62f, 1f);
        }

        if (visitedMark != null) visitedMark.SetActive(false);
    }

    public void OnNodeClicked()
    {
        if (nodeData == null) return;
        if (!nodeData.isAccessible) return;
        if (nodeData.isVisited) return;
        MapSceneManager.Instance.OnNodeSelected(nodeData);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    Color TypeColor(NodeData.NodeType t)
    {
        switch (t)
        {
            case NodeData.NodeType.Start: return cStart;
            case NodeData.NodeType.Combat: return cCombat;
            case NodeData.NodeType.Shop: return cShop;
            case NodeData.NodeType.RandomEvent: return cEvent;
            case NodeData.NodeType.Brand: return cBrand;
            case NodeData.NodeType.Boss: return cBoss;
        }
        return cCombat;
    }

    string TypeLabel(NodeData.NodeType t)
    {
        switch (t)
        {
            case NodeData.NodeType.Start: return "시작";
            case NodeData.NodeType.Combat: return "전투";
            case NodeData.NodeType.Shop: return "상점";
            case NodeData.NodeType.RandomEvent: return "이벤트";
            case NodeData.NodeType.Brand: return "낙인";
            case NodeData.NodeType.Boss: return "보스";
        }
        return "";
    }

    static Color Mul(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
    static Color Fade(Color c, float a) { c.a = a; return c; }

    // ── 절차생성 스프라이트 ──────────────────────────────────────
    static Sprite GetCircle()
    {
        if (_circle == null) _circle = MakeCircle(128);
        return _circle;
    }
    static Sprite GetGlow()
    {
        if (_glow == null) _glow = MakeGlow(128);
        return _glow;
    }
    static Sprite GetRing()
    {
        if (_ring == null) _ring = MakeRing(128);
        return _ring;
    }

    static Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                float a = Mathf.Clamp01((r - 1f - d) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeGlow(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeRing(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        float outer = r - 1f, inner = r * 0.66f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                float a = Mathf.Clamp01((outer - d) / 1.5f) * Mathf.Clamp01((d - inner) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
