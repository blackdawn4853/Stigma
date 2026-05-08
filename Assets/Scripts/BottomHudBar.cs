using UnityEngine;
using UnityEngine.UI;

// 전투씬 하단을 가로로 채우는 HUD 배경 바.
// 자기 전용 root Canvas (sortingOrder=-100) 위에 그려져서 다른 모든 UI Canvas 보다 항상 뒤로 깔림.
// BattleManager.Awake 가 EnsureForBattle() 호출 → 씬 로드 시 자동 생성, 인스펙터 작업 불필요.
public class BottomHudBar : MonoBehaviour
{
    public static BottomHudBar Instance { get; private set; }

    [Header("바 크기")]
    [Tooltip("바의 세로 높이 (픽셀)")]
    public float barHeight = 200f;

    [Header("바 외관")]
    [Tooltip("바 배경 스프라이트 — 비워두면 단색")]
    public Sprite barSprite;
    [Tooltip("바 배경 색상 (스프라이트와 곱해짐)")]
    public Color barColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);

    [Header("렌더 순서")]
    [Tooltip("바 Canvas 의 sortingOrder. 음수일수록 뒤. 다른 UI Canvas (보통 0) 보다 작아야 가려지지 않음.")]
    public int canvasSortingOrder = -100;

    public RectTransform BarRect { get; private set; }

    public static BottomHudBar EnsureForBattle()
    {
        if (Instance != null) return Instance;

#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindAnyObjectByType<BottomHudBar>();
#else
        var existing = Object.FindObjectOfType<BottomHudBar>();
#endif
        if (existing != null)
        {
            Instance = existing;
            existing.Build();
            return existing;
        }

        var holder = new GameObject("BottomHudBarHolder");
        Instance = holder.AddComponent<BottomHudBar>();
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
    }

    void Build()
    {
        // 1) 자기 전용 root Canvas 만들기 (sortingOrder=-100)
        // — 기존 BattleScene Canvas (sortingOrder=0) 보다 작으므로 항상 뒤
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

        var canvasRT = transform as RectTransform;
        if (canvasRT == null) return;

        // 2) 바 Image 자식 생성 (이미 있으면 재사용)
        const string barName = "BottomHudBarImage";
        var existing = canvasRT.Find(barName) as RectTransform;
        GameObject go;
        Image img;
        if (existing != null)
        {
            go = existing.gameObject;
            img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            BarRect = existing;
        }
        else
        {
            go = new GameObject(barName, typeof(RectTransform), typeof(Image));
            BarRect = (RectTransform)go.transform;
            BarRect.SetParent(canvasRT, false);
            img = go.GetComponent<Image>();
        }

        BarRect.anchorMin = new Vector2(0f, 0f);
        BarRect.anchorMax = new Vector2(1f, 0f);
        BarRect.pivot = new Vector2(0.5f, 0f);
        BarRect.anchoredPosition = Vector2.zero;
        BarRect.sizeDelta = new Vector2(0f, barHeight);

        img.color = barColor;
        img.sprite = barSprite;
        img.type = barSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled || BarRect == null) return;
        BarRect.sizeDelta = new Vector2(0f, barHeight);
        var img = BarRect.GetComponent<Image>();
        if (img != null)
        {
            img.color = barColor;
            img.sprite = barSprite;
            img.type = barSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = canvasSortingOrder;
    }
}
