using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

// 마우스 위치를 따라가며 한 대상(플레이어/몬스터)이 가진 모든 버프/디버프 설명을
// 한 패널에 통합 표시하는 단일 툴팁. 슬더스 스타일.
//
// StatusIconBar 가 자기 lastEntries 를 통째로 넘겨주면 그대로 표시한다.
// 어느 아이콘에 호버해도 같은 패널이 같은 내용으로 뜬다.
public class StatusTooltip : MonoBehaviour
{
    private static StatusTooltip instance;

    [System.Serializable]
    public struct Entry
    {
        public StatusType type;
        public int value;
        public int turns;
    }

    private RectTransform panelRt;
    private TextMeshProUGUI bodyText;
    private static readonly StringBuilder sb = new StringBuilder(256);

    public static void Show(List<Entry> entries)
    {
        if (entries == null || entries.Count == 0) { Hide(); return; }

        var inst = GetOrCreate();
        if (inst == null) return;

        sb.Length = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var info = StatusInfo.Get(e.type);
            string hex = ColorUtility.ToHtmlStringRGB(info.tintColor);

            sb.Append("<color=#").Append(hex).Append("><b>").Append(info.displayName).Append("</b></color>");
            if (e.value != 0)
                sb.Append(' ').Append(e.value > 0 ? "+" : "").Append(e.value);
            if (e.turns > 0)
                sb.Append(" (").Append(e.turns).Append("턴)");
            sb.Append('\n');
            sb.Append("<size=85%><color=#E0E0E0>").Append(info.description).Append("</color></size>");
            if (i < entries.Count - 1) sb.Append("\n\n");
        }

        inst.bodyText.text = sb.ToString();
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
        // 패널 (ContentSizeFitter 로 본문 양만큼 세로 확장)
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panelRt = (RectTransform)panel.transform;
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.sizeDelta = new Vector2(280f, 80f);
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);
        bg.raycastTarget = false;

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 0f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // 본문 (모든 엔트리 통합 텍스트, 리치 텍스트로 색/크기 분리)
        GameObject b = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        b.transform.SetParent(panelRt, false);
        bodyText = b.GetComponent<TextMeshProUGUI>();
        bodyText.fontSize = 14;
        bodyText.color = Color.white;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.raycastTarget = false;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.richText = true;
    }
}
