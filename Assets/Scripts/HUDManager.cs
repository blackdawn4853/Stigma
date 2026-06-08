using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// 상단 HUD — NodeMap / BattleScene 에서만 표시. 다른 씬에서는 자동 숨김.
// 슬롯: HP / 골드 / 층 / 보스 / 시선(전투=바, 노드맵=숫자) / 시선효과 5개(20-100).
// 호버 시 공통 툴팁(마우스 따라감)에 정보 표시.
//
// 아트 슬롯 비어있으면 색상 사각형 placeholder. 아트 들어오면 인스펙터에서 Sprite 드래그만.
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    // ─── 인스펙터 노출 ───────────────────────────────────────────────
    [System.Serializable]
    public class BossInfo
    {
        [Tooltip("막 이름 (Floor 표기 / 툴팁용)")]
        public string actName = "1막";
        [Tooltip("보스 이름 (호버 툴팁)")]
        public string bossName = "쇼거스";
        [Tooltip("보스 아이콘 (비어있으면 placeholder 색상)")]
        public Sprite icon;
    }

    [Header("표시 씬 (이 씬에서만 HUD 표시)")]
    [Tooltip("씬 이름 화이트리스트. 이 외의 씬에서는 자동 숨김.")]
    public string[] visibleScenes = { "NodeMap", "BattleScene" };

    [Header("HUD 슬롯 — 아이콘 (아트 들어오면 교체)")]
    [Tooltip("HP 슬롯 아이콘 (하트). 비어있으면 빨간 사각형 placeholder.")]
    public Sprite hpIcon;
    [Tooltip("골드 슬롯 아이콘. 비어있으면 노란 사각형 placeholder.")]
    public Sprite goldIcon;
    [Tooltip("시선 게이지 아이콘 (눈). 비어있으면 보라 사각형 placeholder.")]
    public Sprite gazeIcon;

    [Header("HUD 슬롯 — 보스 정보 (막별)")]
    [Tooltip("막별 보스 정보. bossesDefeated 인덱스로 참조.")]
    public BossInfo[] bossesByAct = new BossInfo[]
    {
        new BossInfo { actName = "1막", bossName = "쇼거스" },
    };

    [Header("HUD 레이아웃 — 위치/크기")]
    [Tooltip("HUD 바 높이 (가로 일자 배경 박스 높이).")]
    public float hudHeight = 64f;
    [Tooltip("HUD 바 좌측 시작 패딩.")]
    public float hudLeftPadding = 24f;
    [Tooltip("HUD 바 우측 패딩 (배경 박스 우측 여유).")]
    public float hudRightPadding = 24f;
    [Tooltip("HUD 바 상단에서 떨어지는 거리 (캔버스 상단 기준).")]
    public float hudTopOffset = 0f;
    [Tooltip("슬롯 사이 간격.")]
    public float slotGap = 18f;

    [Header("HUD 슬롯 — 너비")]
    [Tooltip("HP 슬롯 너비")]      public float hpSlotWidth = 140f;
    [Tooltip("골드 슬롯 너비")]    public float goldSlotWidth = 100f;
    [Tooltip("층 슬롯 너비")]      public float floorSlotWidth = 110f;
    [Tooltip("보스 슬롯 너비")]    public float bossSlotWidth = 56f;
    [Tooltip("시선 슬롯 너비")]    public float gazeSlotWidth = 200f;
    [Tooltip("시선효과 1개 크기")] public float effectIconSize = 40f;
    [Tooltip("시선효과 사이 간격")] public float effectIconGap = 8f;

    [Header("HUD 색상 (placeholder — 아트 들어오면 무관)")]
    [Tooltip("가로 일자 배경 바 색상 (슬더스 스타일 — 어두운 반투명).")]
    public Color hudBgColor = new Color(0.06f, 0.06f, 0.08f, 0.85f);
    [Tooltip("배경 바 하단 테두리 색 (슬더스 스타일 강조선).")]
    public Color hudBorderColor = new Color(0.6f, 0.5f, 0.3f, 0.7f);
    [Tooltip("배경 바 하단 테두리 두께 (0이면 생략).")]
    public float hudBorderThickness = 2f;
    public Color iconHpPlaceholder = new Color(0.85f, 0.2f, 0.2f, 1f);
    public Color iconGoldPlaceholder = new Color(0.95f, 0.78f, 0.2f, 1f);
    public Color iconGazePlaceholder = new Color(0.6f, 0.3f, 0.85f, 1f);
    public Color iconBossPlaceholder = new Color(0.4f, 0.1f, 0.45f, 1f);
    public Color effectActiveColor = new Color(0.95f, 0.7f, 0.25f, 1f);
    public Color effectInactiveColor = new Color(0.25f, 0.25f, 0.28f, 0.9f);
    public Color gazeBarBgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
    public Color gazeBarFillColor = new Color(0.6f, 0.3f, 0.85f, 1f);
    public Color textColor = Color.white;

    [Header("툴팁")]
    [Tooltip("툴팁 폭 (고정). 높이는 텍스트 길이에 따라 자동 조절.")]
    public float tooltipWidth = 360f;
    [Tooltip("내부 패딩 좌")]  public int tooltipPadLeft = 14;
    [Tooltip("내부 패딩 우")]  public int tooltipPadRight = 14;
    [Tooltip("내부 패딩 상")]  public int tooltipPadTop = 10;
    [Tooltip("내부 패딩 하")]  public int tooltipPadBottom = 12;
    [Tooltip("타이틀-본문 사이 간격")]
    public float tooltipSpacing = 6f;
    public Color tooltipBgColor = new Color(0f, 0f, 0f, 0.92f);
    public Vector2 tooltipMouseOffset = new Vector2(15f, -10f);

    [Header("기타")]
    [Tooltip("HUD Canvas 의 sortingOrder. 다른 UI 위에 표시되도록 충분히 큰 값.")]
    public int canvasSortingOrder = 1000;

    // ─── 런타임 참조 ─────────────────────────────────────────────────
    private Canvas hudCanvas;
    private RectTransform hudCanvasRect;
    private GameObject hudRoot;

    private TextMeshProUGUI hpText;
    private TextMeshProUGUI goldText;
    private TextMeshProUGUI floorText;
    private Image bossIconImg;
    private Image gazeIconImg;
    private Slider gazeBar;
    private Image gazeBarFill;
    private TextMeshProUGUI gazeText;       // "10 / 100" (노드맵 모드)
    private RectTransform gazeBarRect;

    private class EffectSlotRefs
    {
        public int threshold;
        public Image bg;
        public Image iconImg;
    }
    private List<EffectSlotRefs> effectSlots = new List<EffectSlotRefs>();

    private GameObject tooltipPanel;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipTitle;
    private TextMeshProUGUI tooltipBody;

    // ─── 부트스트랩 ──────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoBootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("HUDManager");
        if (prefab != null)
            Instantiate(prefab).name = "HUDManager";
        else
        {
            GameObject go = new GameObject("HUDManager");
            go.AddComponent<HUDManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BuildHUD();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVisibilityForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVisibilityForScene(scene.name);
    }

    void ApplyVisibilityForScene(string sceneName)
    {
        if (hudCanvas == null) return;
        bool show = false;
        for (int i = 0; i < visibleScenes.Length; i++)
            if (visibleScenes[i] == sceneName) { show = true; break; }
        hudCanvas.gameObject.SetActive(show);
    }

    // ─── UI 빌드 ─────────────────────────────────────────────────────
    void BuildHUD()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        // Canvas (전용 — 다른 캔버스 위로 표시)
        GameObject canvasGO = new GameObject("HUDCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        hudCanvas = canvasGO.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        hudCanvasRect = canvasGO.GetComponent<RectTransform>();

        // EventSystem 보장 (다른 씬에서 빠진 경우)
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        // HUD 배경 바 (가로 일자 풀폭, 슬더스 스타일)
        hudRoot = new GameObject("HUDBar", typeof(RectTransform), typeof(Image));
        hudRoot.transform.SetParent(hudCanvas.transform, false);
        var rootRT = (RectTransform)hudRoot.transform;
        rootRT.anchorMin = new Vector2(0f, 1f);
        rootRT.anchorMax = new Vector2(1f, 1f);
        rootRT.pivot = new Vector2(0.5f, 1f);
        rootRT.anchoredPosition = new Vector2(0f, -hudTopOffset);
        rootRT.sizeDelta = new Vector2(0f, hudHeight); // 좌우 stretch (sizeDelta.x=0 = 캔버스 폭)
        var bgImg = hudRoot.GetComponent<Image>();
        bgImg.color = hudBgColor;
        bgImg.raycastTarget = false;

        // 하단 테두리 강조선 (옵션)
        if (hudBorderThickness > 0f)
        {
            GameObject border = new GameObject("HUDBorder", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(rootRT, false);
            var brt = (RectTransform)border.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, hudBorderThickness);
            var bImg = border.GetComponent<Image>();
            bImg.color = hudBorderColor;
            bImg.raycastTarget = false;
        }

        // 좌(자원) / 중앙(시선) / 우(진행) 3그룹으로 분리 배치
        BuildLeftCluster(rootRT, font);
        BuildCenterCluster(rootRT, font);
        BuildRightCluster(rootRT, font);

        // 툴팁
        BuildTooltip(font);

        // 첫 갱신
        RefreshAll();
    }

    // ─── 슬롯 빌드 헬퍼 ──────────────────────────────────────────────
    // 각 슬롯은 [투명 hover Image (raycast=true)] + 자식 [icon, text] 구조.
    RectTransform CreateSlot(RectTransform parent, string name, float x, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(width, hudHeight);
        var img = go.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0); // 투명 hover 영역
        img.raycastTarget = true;
        return rt;
    }

    Image AddIcon(RectTransform parent, string name, Sprite sprite, Color placeholder, float size, float xOffset)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(xOffset, 0f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
        else                { img.color = placeholder; }
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    TextMeshProUGUI AddText(RectTransform parent, string name, TMP_FontAsset font, float fontSize,
                             float left, float right, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = fontSize;
        t.color = textColor;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    // ═══ 좌측 클러스터: HP + 골드 (핵심 자원, 크게) ═══
    void BuildLeftCluster(RectTransform bar, TMP_FontAsset font)
    {
        var c = NewCluster(bar, "LeftCluster", 0f, hudLeftPadding);
        float x = 0f;
        x = BuildResChip(c, font, "HpSlot", x, 156f, true, () => BuildHpTooltip(), out hpText);
        x += 26f;
        BuildResChip(c, font, "GoldSlot", x, 124f, false, () => BuildGoldTooltip(), out goldText);
    }

    // ═══ 우측 클러스터: 층/막 + 보스 (진행 정보, 보조) ═══
    void BuildRightCluster(RectTransform bar, TMP_FontAsset font)
    {
        var c = NewCluster(bar, "RightCluster", 1f, hudRightPadding);

        float bossSize = hudHeight - 16f;
        var bossGO = new GameObject("BossSlot", typeof(RectTransform), typeof(Image));
        bossGO.transform.SetParent(c, false);
        var brt = (RectTransform)bossGO.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(bossSize, hudHeight);
        var bimg = bossGO.GetComponent<Image>();
        bimg.color = new Color(0, 0, 0, 0); bimg.raycastTarget = true;
        bossIconImg = AddIcon(brt, "Icon", null, iconBossPlaceholder, bossSize, 0f);
        AddHover(brt, () => BuildBossTooltip());

        var floorGO = new GameObject("FloorText", typeof(RectTransform), typeof(TextMeshProUGUI));
        floorGO.transform.SetParent(c, false);
        var frt = (RectTransform)floorGO.transform;
        frt.anchorMin = frt.anchorMax = new Vector2(1f, 0.5f);
        frt.pivot = new Vector2(1f, 0.5f);
        frt.anchoredPosition = new Vector2(-(bossSize + 12f), 0f);
        frt.sizeDelta = new Vector2(220f, hudHeight);
        floorText = floorGO.GetComponent<TextMeshProUGUI>();
        floorText.font = font; floorText.fontSize = 20f; floorText.color = textColor;
        floorText.alignment = TextAlignmentOptions.MidlineRight;
        floorText.textWrappingMode = TextWrappingModes.NoWrap;
        floorText.raycastTarget = true;
        AddHover(frt, () => BuildFloorTooltip());
    }

    // ═══ 중앙 클러스터: 시선(노드맵=숫자/전투=바) + 효과 pip 5개 ═══
    void BuildCenterCluster(RectTransform bar, TMP_FontAsset font)
    {
        var c = NewCluster(bar, "CenterCluster", 0.5f, 0f);
        c.sizeDelta = new Vector2(360f, hudHeight);

        const float gazeW = 190f;

        // 시선 바 (전투) — 배경
        GameObject barBgGO = new GameObject("GazeBarBg", typeof(RectTransform), typeof(Image));
        barBgGO.transform.SetParent(c, false);
        gazeBarRect = (RectTransform)barBgGO.transform;
        gazeBarRect.anchorMin = gazeBarRect.anchorMax = new Vector2(0f, 0.5f);
        gazeBarRect.pivot = new Vector2(0f, 0.5f);
        gazeBarRect.anchoredPosition = new Vector2(0f, 0f);
        gazeBarRect.sizeDelta = new Vector2(gazeW, hudHeight - 30f);
        var gbg = barBgGO.GetComponent<Image>();
        gbg.color = gazeBarBgColor; gbg.raycastTarget = false;

        GameObject sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(gazeBarRect, false);
        var sRt = (RectTransform)sliderGO.transform;
        sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(2f, 2f); sRt.offsetMax = new Vector2(-2f, -2f);
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sRt, false);
        var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one; faRt.offsetMin = faRt.offsetMax = Vector2.zero;
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(faRt, false);
        var fRt = (RectTransform)fillGO.transform;
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.offsetMin = fRt.offsetMax = Vector2.zero;
        gazeBarFill = fillGO.GetComponent<Image>();
        gazeBarFill.color = gazeBarFillColor; gazeBarFill.raycastTarget = false;
        gazeBar = sliderGO.GetComponent<Slider>();
        gazeBar.transition = Selectable.Transition.None;
        gazeBar.fillRect = fRt; gazeBar.targetGraphic = null;
        gazeBar.minValue = 0f; gazeBar.maxValue = 1f; gazeBar.value = 0f; gazeBar.interactable = false;

        // 시선 텍스트 (항상) — 바 위/단독 오버레이 (바와 별개라 노드맵에서도 보임)
        GameObject gtGO = new GameObject("GazeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        gtGO.transform.SetParent(c, false);
        var gtRt = (RectTransform)gtGO.transform;
        gtRt.anchorMin = gtRt.anchorMax = new Vector2(0f, 0.5f);
        gtRt.pivot = new Vector2(0f, 0.5f);
        gtRt.anchoredPosition = new Vector2(0f, 0f);
        gtRt.sizeDelta = new Vector2(gazeW, hudHeight);
        gazeText = gtGO.GetComponent<TextMeshProUGUI>();
        gazeText.font = font; gazeText.fontSize = 22f; gazeText.color = textColor;
        gazeText.alignment = TextAlignmentOptions.Center;
        gazeText.textWrappingMode = TextWrappingModes.NoWrap;
        gazeText.raycastTarget = false;

        // 시선 호버 영역
        var gazeHover = CreateSlot(c, "GazeHover", 0f, gazeW);
        AddHover(gazeHover, () => BuildGazeTooltip());

        // 효과 pip 5개 (작은 점 — 활성/비활성 색)
        int[] thresholds = { 20, 40, 60, 80, 100 };
        const float pipSize = 18f, pipGap = 9f;
        float px = gazeW + 18f;
        for (int i = 0; i < thresholds.Length; i++)
        {
            int t = thresholds[i];
            float xi = px + i * (pipSize + pipGap);
            GameObject pipGO = new GameObject($"Pip_{t}", typeof(RectTransform), typeof(Image));
            pipGO.transform.SetParent(c, false);
            var rt = (RectTransform)pipGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xi, 0f);
            rt.sizeDelta = new Vector2(pipSize, pipSize);
            var img = pipGO.GetComponent<Image>();
            img.sprite = GetCircleSprite();
            img.color = effectInactiveColor;
            img.raycastTarget = true;
            int captured = t;
            AddHover(rt, () => BuildEffectTooltip(captured));
            effectSlots.Add(new EffectSlotRefs { threshold = t, bg = img, iconImg = null });
        }
    }

    // ─── 클러스터/칩/아이콘 헬퍼 ─────────────────────────────────────
    RectTransform NewCluster(RectTransform parent, string name, float anchorX, float padX)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.5f);
        rt.pivot = new Vector2(anchorX, 0.5f);
        float px = anchorX <= 0f ? padX : (anchorX >= 1f ? -padX : 0f);
        rt.anchoredPosition = new Vector2(px, 0f);
        rt.sizeDelta = new Vector2(10f, hudHeight);
        return rt;
    }

    float BuildResChip(RectTransform parent, TMP_FontAsset font, string name, float x, float width,
                       bool isHp, TooltipFunc tip, out TextMeshProUGUI text)
    {
        var slot = CreateSlot(parent, name, x, width);
        float iconSize = hudHeight - 22f;
        if (isHp) BuildHeartIcon(slot, iconSize, 4f);
        else BuildCoinIcon(slot, iconSize, 4f);
        text = AddText(slot, "Text", font, 28f, iconSize + 14f, 4f, TextAlignmentOptions.MidlineLeft);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        AddHover(slot, tip);
        return x + width;
    }

    RectTransform NewIconHolder(RectTransform slot, string name, float size, float xOffset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(slot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(xOffset, 0f);
        rt.sizeDelta = new Vector2(size, size);
        return rt;
    }

    // 하트 아이콘 (스프라이트 있으면 사용, 없으면 원 2개 + 다이아로 절차생성)
    void BuildHeartIcon(RectTransform slot, float size, float xOffset)
    {
        if (hpIcon != null) { AddIcon(slot, "Icon", hpIcon, iconHpPlaceholder, size, xOffset); return; }
        var holder = NewIconHolder(slot, "HpIcon", size, xOffset);
        Color red = iconHpPlaceholder;
        float u = size;
        MakeShape(holder, null, new Vector2(u * 0.6f, u * 0.6f), new Vector2(0f, -u * 0.06f), 45f, red);
        MakeShape(holder, GetCircleSprite(), new Vector2(u * 0.5f, u * 0.5f), new Vector2(-u * 0.19f, u * 0.17f), 0f, red);
        MakeShape(holder, GetCircleSprite(), new Vector2(u * 0.5f, u * 0.5f), new Vector2(u * 0.19f, u * 0.17f), 0f, red);
    }

    // 코인 아이콘 (스프라이트 있으면 사용, 없으면 금색 원 + 안쪽 림)
    void BuildCoinIcon(RectTransform slot, float size, float xOffset)
    {
        if (goldIcon != null) { AddIcon(slot, "Icon", goldIcon, iconGoldPlaceholder, size, xOffset); return; }
        var holder = NewIconHolder(slot, "GoldIcon", size, xOffset);
        Color gold = iconGoldPlaceholder;
        MakeShape(holder, GetCircleSprite(), new Vector2(size, size), Vector2.zero, 0f, gold);
        MakeShape(holder, GetCircleSprite(), new Vector2(size * 0.6f, size * 0.6f), Vector2.zero, 0f, Mul(gold, 0.7f));
    }

    Image MakeShape(RectTransform holder, Sprite sprite, Vector2 size, Vector2 pos, float rot, Color col)
    {
        var go = new GameObject("Shape", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(holder, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        var img = go.GetComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    static Color Mul(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

    static Sprite _hudCircle;
    static Sprite GetCircleSprite()
    {
        if (_hudCircle == null)
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = s * 0.5f;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int xx = 0; xx < s; xx++)
                {
                    float d = Mathf.Sqrt((xx - r) * (xx - r) + (y - r) * (y - r));
                    px[y * s + xx] = new Color(1f, 1f, 1f, Mathf.Clamp01((r - 1f - d) / 1.5f));
                }
            tex.SetPixels(px); tex.Apply();
            _hudCircle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
        return _hudCircle;
    }

    void BuildTooltip(TMP_FontAsset font)
    {
        // VerticalLayoutGroup + ContentSizeFitter 로 텍스트 길이에 맞춰 높이 자동 조절.
        // 폭은 tooltipWidth 로 고정 (긴 텍스트는 wrap).
        GameObject tip = new GameObject("HUDTooltip",
            typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tip.transform.SetParent(hudCanvas.transform, false);
        tooltipRect = (RectTransform)tip.transform;
        tooltipRect.anchorMin = new Vector2(0f, 0f);
        tooltipRect.anchorMax = new Vector2(0f, 0f);
        tooltipRect.pivot = new Vector2(0f, 1f); // 마우스 좌상단 기준 → 우하로 떨어짐
        tooltipRect.sizeDelta = new Vector2(tooltipWidth, 0f); // 높이는 fitter 가 결정
        var bg = tip.GetComponent<Image>();
        bg.color = tooltipBgColor;
        bg.raycastTarget = false;

        var vlg = tip.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(tooltipPadLeft, tooltipPadRight, tooltipPadTop, tooltipPadBottom);
        vlg.spacing = tooltipSpacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = tip.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 폭 고정
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 높이 자동

        // Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(tooltipRect, false);
        tooltipTitle = titleGO.GetComponent<TextMeshProUGUI>();
        tooltipTitle.font = font;
        tooltipTitle.fontSize = 22f;
        tooltipTitle.color = textColor;
        tooltipTitle.alignment = TextAlignmentOptions.TopLeft;
        tooltipTitle.textWrappingMode = TextWrappingModes.Normal;
        tooltipTitle.raycastTarget = false;

        // Body
        GameObject bodyGO = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(tooltipRect, false);
        tooltipBody = bodyGO.GetComponent<TextMeshProUGUI>();
        tooltipBody.font = font;
        tooltipBody.fontSize = 17f;
        tooltipBody.color = textColor;
        tooltipBody.alignment = TextAlignmentOptions.TopLeft;
        tooltipBody.textWrappingMode = TextWrappingModes.Normal;
        tooltipBody.raycastTarget = false;

        tooltipPanel = tip;
        tooltipPanel.SetActive(false);
    }

    // ─── 호버 바인딩 ──────────────────────────────────────────────────
    delegate (string title, string body) TooltipFunc();
    void AddHover(RectTransform target, TooltipFunc build)
    {
        var proxy = target.gameObject.GetComponent<HoverProxy>();
        if (proxy == null) proxy = target.gameObject.AddComponent<HoverProxy>();
        proxy.owner = this;
        proxy.builder = build;
    }

    // ─── 폴링 갱신 ────────────────────────────────────────────────────
    void Update()
    {
        if (hudCanvas == null || !hudCanvas.gameObject.activeSelf) return;
        RefreshAll();
        if (tooltipPanel != null && tooltipPanel.activeSelf) FollowMouse();
    }

    void RefreshAll()
    {
        // HP
        int curHp, maxHp;
        if (BattleManager.Instance != null && IsBattleScene())
        {
            curHp = BattleManager.Instance.playerCurrentHp;
            maxHp = BattleManager.Instance.playerMaxHp;
        }
        else if (GameManager.Instance != null)
        {
            curHp = GameManager.Instance.playerCurrentHp;
            maxHp = GameManager.Instance.playerMaxHp;
        }
        else { curHp = 0; maxHp = 0; }
        if (hpText != null) hpText.text = $"{Mathf.Max(0, curHp)} / {maxHp}";

        // Gold
        int gold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        if (goldText != null) goldText.text = gold.ToString();

        // Floor
        if (floorText != null) floorText.text = $"Floor {GetActNumber()}-{GetFloorNumber()}";

        // Boss
        var boss = GetCurrentBoss();
        if (bossIconImg != null)
        {
            if (boss != null && boss.icon != null)
            {
                bossIconImg.sprite = boss.icon;
                bossIconImg.color = Color.white;
            }
            else
            {
                bossIconImg.sprite = null;
                bossIconImg.color = iconBossPlaceholder;
            }
        }

        // Gaze
        int gaze = GetCurrentGaze();
        bool inBattle = IsBattleScene() && BattleManager.Instance != null;
        if (gazeBarRect != null) gazeBarRect.gameObject.SetActive(inBattle);
        if (gazeBar != null) gazeBar.value = Mathf.Clamp01(gaze / 100f);
        if (gazeText != null)
        {
            // 전투에서는 바 위에 작게 오버레이, 노드맵에서는 단독 텍스트
            gazeText.text = $"{gaze} / 100";
        }

        // Effects
        if (GazeEffectManager.Instance != null)
        {
            for (int i = 0; i < effectSlots.Count; i++)
            {
                var s = effectSlots[i];
                bool active = gaze >= s.threshold;
                if (s.bg != null) s.bg.color = active ? effectActiveColor : effectInactiveColor;

                var data = GazeEffectManager.Instance.GetEffectAt(s.threshold);
                if (s.iconImg != null)
                {
                    if (data != null && data.icon != null)
                    {
                        s.iconImg.sprite = data.icon;
                        s.iconImg.enabled = true;
                    }
                    else
                    {
                        s.iconImg.enabled = false; // placeholder 라벨이 보임
                    }
                }
            }
        }
    }

    // ─── 툴팁 콘텐츠 빌더 ────────────────────────────────────────────
    (string, string) BuildHpTooltip()
    {
        int cur, max;
        if (BattleManager.Instance != null && IsBattleScene())
        { cur = BattleManager.Instance.playerCurrentHp; max = BattleManager.Instance.playerMaxHp; }
        else if (GameManager.Instance != null)
        { cur = GameManager.Instance.playerCurrentHp; max = GameManager.Instance.playerMaxHp; }
        else { cur = 0; max = 0; }
        return ("체력", $"현재 체력: {Mathf.Max(0, cur)} / {max}");
    }

    (string, string) BuildGoldTooltip()
    {
        int g = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        return ("골드", $"보유 골드: {g}");
    }

    (string, string) BuildFloorTooltip()
    {
        return ("층", $"현재 위치: {GetActNumber()}막 {GetFloorNumber()}층");
    }

    (string, string) BuildBossTooltip()
    {
        var b = GetCurrentBoss();
        if (b == null) return ("보스", "보스 정보 없음");
        return (b.bossName, $"{b.actName} 보스");
    }

    (string, string) BuildGazeTooltip()
    {
        int gaze = GetCurrentGaze();
        // 다음 트리거 효과
        int nextThreshold = -1;
        if (gaze < 20) nextThreshold = 20;
        else if (gaze < 40) nextThreshold = 40;
        else if (gaze < 60) nextThreshold = 60;
        else if (gaze < 80) nextThreshold = 80;
        else if (gaze < 100) nextThreshold = 100;

        string body = $"현재 시선: {gaze} / 100";
        if (nextThreshold > 0 && GazeEffectManager.Instance != null)
        {
            var data = GazeEffectManager.Instance.GetEffectAt(nextThreshold);
            string nextName = data != null ? data.displayName : "(미정)";
            body += $"\n다음 트리거 ({nextThreshold}): {nextName}";
        }
        else if (gaze >= 100)
        {
            body += "\n100 도달 — 트리거 임박!";
        }
        return ("시선 게이지", body);
    }

    (string, string) BuildEffectTooltip(int threshold)
    {
        if (GazeEffectManager.Instance == null)
            return ($"[{threshold}] 시선 효과", "효과 정보를 불러올 수 없습니다.");
        var data = GazeEffectManager.Instance.GetEffectAt(threshold);
        if (data == null)
            return ($"[{threshold}] 시선 효과", "효과가 아직 결정되지 않았습니다.");
        string body =
            $"<color=#3FCB6E>버프</color>: {data.buffDescription}\n" +
            $"<color=#E25555>디버프</color>: {data.debuffDescription}";
        return ($"[{threshold}] {data.displayName}", body);
    }

    // ─── 툴팁 표시/숨김 ──────────────────────────────────────────────
    void ShowTooltip(string title, string body)
    {
        if (tooltipPanel == null) return;
        if (tooltipTitle != null) tooltipTitle.text = title;
        if (tooltipBody != null) tooltipBody.text = body;
        tooltipPanel.SetActive(true);
        tooltipPanel.transform.SetAsLastSibling();
        // ContentSizeFitter 가 새 텍스트 기준으로 즉시 높이 재계산하도록 강제 빌드.
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        FollowMouse();
    }

    void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    void FollowMouse()
    {
        if (tooltipRect == null || hudCanvasRect == null) return;

        RectTransform parentRect = tooltipRect.parent as RectTransform;
        if (parentRect == null) return;

        Camera cam = (hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? hudCanvas.worldCamera : null;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, Input.mousePosition, cam, out local);

        // GazeHoverUI 와 동일 변환: tooltipRect anchor 기준으로 local 좌표 보정
        Vector2 pivotOffset = new Vector2(
            parentRect.rect.width * (tooltipRect.anchorMin.x - parentRect.pivot.x),
            parentRect.rect.height * (tooltipRect.anchorMin.y - parentRect.pivot.y));

        tooltipRect.anchoredPosition = local + tooltipMouseOffset - pivotOffset;
    }

    // ─── 외부 강제 갱신용 (필요하면 다른 매니저에서 호출) ────────────
    public void ForceRefresh() { RefreshAll(); }

    // ─── 헬퍼 ────────────────────────────────────────────────────────
    bool IsBattleScene()
    {
        return SceneManager.GetActiveScene().name == "BattleScene";
    }

    int GetActNumber()
    {
        int defeated = GameManager.Instance != null ? GameManager.Instance.bossesDefeated : 0;
        return defeated + 1;
    }

    int GetFloorNumber()
    {
        if (GameManager.Instance == null) return 1;
        int layer = GameManager.Instance.currentNodeLayer;
        return Mathf.Max(0, layer) + 1;
    }

    BossInfo GetCurrentBoss()
    {
        if (bossesByAct == null || bossesByAct.Length == 0) return null;
        int defeated = GameManager.Instance != null ? GameManager.Instance.bossesDefeated : 0;
        int idx = Mathf.Clamp(defeated, 0, bossesByAct.Length - 1);
        return bossesByAct[idx];
    }

    int GetCurrentGaze()
    {
        if (IsBattleScene() && BattleManager.Instance != null)
            return BattleManager.Instance.gazeLevel;
        if (GameManager.Instance != null)
            return GameManager.Instance.runGazeLevel;
        return 0;
    }

    // ─── 내부 클래스: 마우스 호버 프록시 ─────────────────────────────
    private class HoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HUDManager owner;
        public TooltipFunc builder;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner == null || builder == null) return;
            var (title, body) = builder();
            owner.ShowTooltip(title, body);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != null) owner.HideTooltip();
        }
    }
}
