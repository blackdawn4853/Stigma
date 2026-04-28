using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 마우스 위치를 따라가며 버프/디버프 설명을 띄우는 단일 툴팁.
// 첫 호출 시 별도 ScreenSpaceOverlay 캔버스를 만들어 최상위에 그린다.
public class StatusTooltip : MonoBehaviour
{
    private static StatusTooltip instance;

    private RectTransform panelRt;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;

    public static void Show(StatusType type, int value, int turns)
    {
        var inst = GetOrCreate();
        if (inst == null) return;

        var info = StatusInfo.Get(type);
        inst.titleText.text = info.displayName;
        inst.titleText.color = info.tintColor;

        string body = info.description;
        if (value != 0)
            body += $"\n수치: {(value > 0 ? "+" : "")}{value}";
        if (turns > 0)
            body += $"\n남은 턴: {turns}";
        inst.bodyText.text = body;

        inst.gameObject.SetActive(true);
        inst.UpdatePosition();
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (gameObject.activeSelf) UpdatePosition();
    }

    void UpdatePosition()
    {
        if (panelRt == null) return;
        Vector3 mouse = Input.mousePosition;
        // 우상단으로 약간 띄움
        panelRt.position = new Vector3(mouse.x + 18f, mouse.y + 18f, 0f);

        // 화면 우측/상단 클리핑 방지
        Vector3[] corners = new Vector3[4];
        panelRt.GetWorldCorners(corners);
        float screenW = Screen.width, screenH = Screen.height;
        float overflowX = Mathf.Max(0, corners[2].x - screenW);
        float overflowY = Mathf.Max(0, corners[2].y - screenH);
        if (overflowX > 0 || overflowY > 0)
            panelRt.position -= new Vector3(overflowX, overflowY, 0f);
    }

    static StatusTooltip GetOrCreate()
    {
        if (instance != null) return instance;

        GameObject host = new GameObject("StatusTooltip");
        DontDestroyOnLoad(host);
        var canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        host.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        host.AddComponent<GraphicRaycaster>();

        var inst = host.AddComponent<StatusTooltip>();
        inst.Build();
        host.SetActive(false);
        instance = inst;
        return instance;
    }

    void Build()
    {
        // 패널
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panelRt = (RectTransform)panel.transform;
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.sizeDelta = new Vector2(220f, 80f);
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        bg.raycastTarget = false;

        // 타이틀
        GameObject t = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.transform.SetParent(panelRt, false);
        var tRt = (RectTransform)t.transform;
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0f, 1f);
        tRt.offsetMin = new Vector2(8f, -28f);
        tRt.offsetMax = new Vector2(-8f, -4f);
        titleText = t.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.raycastTarget = false;

        // 본문
        GameObject b = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        b.transform.SetParent(panelRt, false);
        var bRt = (RectTransform)b.transform;
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 1f);
        bRt.pivot = new Vector2(0f, 1f);
        bRt.offsetMin = new Vector2(8f, 4f);
        bRt.offsetMax = new Vector2(-8f, -32f);
        bodyText = b.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 13;
        bodyText.color = new Color(0.92f, 0.92f, 0.92f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.raycastTarget = false;
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        // 가변 높이 — ContentSizeFitter 로 본문 양만큼 확장
        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 6, 6);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // 두 텍스트를 자식으로 직접 배치하면 VerticalLayoutGroup 가 처리
        // (위에서 anchor/offset 설정한 부분은 VerticalLayoutGroup 이 덮어씀)
    }
}
