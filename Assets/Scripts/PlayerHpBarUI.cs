using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 플레이어 HP 바 — 화면 왼쪽 끝에 고정되는 단순 horizontal 빨간 바.
// BattleManager.Awake 가 EnsureForBattle() 호출 → 자동 생성. 인스펙터 작업 불필요.
// 월드 스페이스 PlayerRuntimeUI 가 스폰돼있으면 함께 비활성화 (캐릭터 뒤에 가려지는 문제 해결).
public class PlayerHpBarUI : MonoBehaviour
{
    public static PlayerHpBarUI Instance { get; private set; }

    [Header("앵커 위치 (Canvas 좌하단 기준 — X 양수=오른쪽, Y 양수=위)")]
    public Vector2 anchoredPosition = new Vector2(40f, 180f);
    [Tooltip("HP 바 크기 (가로 x 세로 픽셀)")]
    public Vector2 size = new Vector2(280f, 24f);

    [Header("바 외관 — 이미지 3 같은 단순 빨간 바")]
    [Tooltip("HP fill 이미지 (예: 빨간 가로 바 sprite). 비워두면 단색 fill.")]
    public Sprite fillSprite;
    [Tooltip("Fill 색상 (방어도 없을 때, 평소 빨강)")]
    public Color fillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    [Tooltip("방어도가 있을 때 HP fill 색 (방어 중임을 표시 — 흰/푸른색)")]
    public Color defendedFillColor = new Color(0.62f, 0.82f, 1f, 1f);
    [Tooltip("배경 이미지 (선택). 비워두면 어두운 단색 backdrop.")]
    public Sprite backSprite;
    public Color backColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    [Tooltip("내부 fill 의 좌우상하 padding (픽셀)")]
    public float fillPadding = 2f;

    [Header("HP 숫자 텍스트")]
    [Tooltip("바 위에 '현재/최대' HP 숫자 표시 (예: 10/50)")]
    public bool showHpText = true;
    public Color hpTextColor = Color.white;
    public float hpTextFontSize = 16f;

    [Header("Canvas 렌더 순서")]
    [Tooltip("BottomHudBar(-100) 보다 위, 카드/버튼(0) 과 비슷하게 배치하려면 0~10")]
    public int canvasSortingOrder = 5;

    [Header("방어 플래시 (BodyDefenseUI)")]
    [Tooltip("플레이어 몸 위로 슬라이드되는 방패 아이콘의 크기 (canvas-local px). 클수록 화면에서 큼.")]
    public Vector2 bodyDefenseShieldSize = new Vector2(780f, 780f);
    [Tooltip("플레이어 위치 기준 월드 오프셋 (몸 중앙). Y 값이 클수록 위쪽에서 표시.")]
    public Vector3 bodyDefenseWorldOffset = new Vector3(1.8f, 2.7f, 0f);

    private Slider slider;
    private Image fillImage;
    private Image backImage;
    private TextMeshProUGUI hpText;
    private BodyDefenseUI playerBodyDefense;
    private DefenseBadgeUI defenseBadge;

    public static PlayerHpBarUI EnsureForBattle()
    {
        if (Instance != null) return Instance;
#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindAnyObjectByType<PlayerHpBarUI>();
#else
        var existing = Object.FindObjectOfType<PlayerHpBarUI>();
#endif
        if (existing != null)
        {
            Instance = existing;
            existing.Build();
            return existing;
        }
        var holder = new GameObject("PlayerHpBarUIHolder");
        Instance = holder.AddComponent<PlayerHpBarUI>();
        Instance.Build();
        return Instance;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        Build();
        DisableWorldSpacePlayerUI();
        EnsurePlayerBodyDefense();
    }

    void Update()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        if (slider != null && bm.playerMaxHp > 0)
        {
            float ratio = (float)bm.playerCurrentHp / bm.playerMaxHp;
            slider.value = Mathf.Clamp01(ratio);
            if (hpText != null && showHpText)
                hpText.text = $"{Mathf.Max(0, bm.playerCurrentHp)}/{bm.playerMaxHp}";
        }

        // 방어도가 있으면 HP 바 색을 푸른/흰색으로 (방어 중 표시), 없으면 평소 빨강.
        if (fillImage != null)
            fillImage.color = bm.playerDefense > 0 ? defendedFillColor : fillColor;

        // 방어도 배지 (방패 아이콘 + 숫자) — 0 이면 자동 숨김.
        if (defenseBadge != null) defenseBadge.SetValue(bm.playerDefense);

        // 플레이어가 방어도 얻을 때 슬라이드 방패 트리거 — 옛 PlayerRuntimeUI 가 비활성화돼서 끊긴 경로 복구.
        if (playerBodyDefense == null) EnsurePlayerBodyDefense();
        if (playerBodyDefense != null) playerBodyDefense.SetValue(bm.playerDefense);
    }

    void EnsurePlayerBodyDefense()
    {
        if (playerBodyDefense != null) return;
        if (BattleManager.Instance == null || BattleManager.Instance.playerObject == null) return;
        playerBodyDefense = BodyDefenseUI.CreateFor(
            BattleManager.Instance.playerObject.transform,
            bodyDefenseWorldOffset,
            flipX: false); // 방패 면이 오른쪽(몬스터)을 향함 — 스프라이트 기본 방향
        if (playerBodyDefense != null)
            playerBodyDefense.SetShieldSize(bodyDefenseShieldSize);
    }

    // 월드 스페이스 PlayerRuntimeUI 제거 — 캐릭터에 가려지는 옛 HP 바 + 동반 BodyDefenseUI 까지 정리.
    // BattleUI.useWorldSpacePlayerUI 도 false 로 강제 (이후 재스폰 방지).
    void DisableWorldSpacePlayerUI()
    {
        if (BattleUI.Instance != null) BattleUI.Instance.useWorldSpacePlayerUI = false;
#if UNITY_2023_1_OR_NEWER
        var prus = Object.FindObjectsByType<PlayerRuntimeUI>(FindObjectsSortMode.None);
#else
        var prus = Object.FindObjectsOfType<PlayerRuntimeUI>();
#endif
        // Destroy 호출 → PlayerRuntimeUI.OnDestroy 가 자기 BodyDefenseUI 도 Destroy 함 (중복 방패 방지)
        for (int i = 0; i < prus.Length; i++)
            if (prus[i] != null) Destroy(prus[i].gameObject);
    }

    void Build()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        var canvasRT = transform as RectTransform;
        if (canvasRT == null) return;

        const string sliderName = "PlayerHpSlider";
        var sliderRT = canvasRT.Find(sliderName) as RectTransform;
        GameObject sliderGo;
        if (sliderRT != null)
            sliderGo = sliderRT.gameObject;
        else
        {
            sliderGo = new GameObject(sliderName, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(canvasRT, false);
            sliderRT = (RectTransform)sliderGo.transform;
        }

        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(0f, 0f);
        sliderRT.pivot = new Vector2(0f, 0f);
        sliderRT.anchoredPosition = anchoredPosition;
        sliderRT.sizeDelta = size;

        slider = sliderGo.GetComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.targetGraphic = null;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;

        // Background
        const string backName = "Back";
        var backRT = sliderRT.Find(backName) as RectTransform;
        if (backRT == null)
        {
            var bg = new GameObject(backName, typeof(RectTransform), typeof(Image));
            backRT = (RectTransform)bg.transform;
            backRT.SetParent(sliderRT, false);
            backRT.SetAsFirstSibling();
        }
        backRT.anchorMin = Vector2.zero;
        backRT.anchorMax = Vector2.one;
        backRT.offsetMin = Vector2.zero;
        backRT.offsetMax = Vector2.zero;
        backImage = backRT.GetComponent<Image>();
        backImage.color = backColor;
        backImage.sprite = backSprite;
        backImage.type = backSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backImage.raycastTarget = false;

        // Fill area + fill
        const string fillAreaName = "FillArea";
        var fillAreaRT = sliderRT.Find(fillAreaName) as RectTransform;
        if (fillAreaRT == null)
        {
            var fa = new GameObject(fillAreaName, typeof(RectTransform));
            fillAreaRT = (RectTransform)fa.transform;
            fillAreaRT.SetParent(sliderRT, false);
        }
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(fillPadding, fillPadding);
        fillAreaRT.offsetMax = new Vector2(-fillPadding, -fillPadding);

        const string fillName = "Fill";
        var fillRT = fillAreaRT.Find(fillName) as RectTransform;
        if (fillRT == null)
        {
            var fl = new GameObject(fillName, typeof(RectTransform), typeof(Image));
            fillRT = (RectTransform)fl.transform;
            fillRT.SetParent(fillAreaRT, false);
        }
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillImage = fillRT.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.sprite = fillSprite;
        fillImage.type = fillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        fillImage.raycastTarget = false;

        slider.fillRect = fillRT;

        // HP 숫자 텍스트 (바 중앙에 오버레이)
        const string textName = "HpText";
        var textRT = sliderRT.Find(textName) as RectTransform;
        if (showHpText)
        {
            if (textRT == null)
            {
                var t = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
                textRT = (RectTransform)t.transform;
                textRT.SetParent(sliderRT, false);
            }
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            textRT.gameObject.SetActive(true);
            hpText = textRT.GetComponent<TextMeshProUGUI>();
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.fontSize = hpTextFontSize;
            hpText.color = hpTextColor;
            hpText.fontStyle = FontStyles.Bold;
            hpText.raycastTarget = false;
        }
        else if (textRT != null)
        {
            textRT.gameObject.SetActive(false);
            hpText = null;
        }

        // 방어도 배지 — 몬스터와 동일(방패 아이콘 + 숫자). HP 바 좌측 위에 배치, 방어도 있을 때만 표시.
        if (defenseBadge == null)
        {
            defenseBadge = DefenseBadgeUI.Create(sliderRT,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPos: new Vector2(26f, 34f),
                size: new Vector2(52f, 52f),
                bgColor: new Color(0.15f, 0.55f, 0.85f));
        }
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;
        Build();
    }
}
