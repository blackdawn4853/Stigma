using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// BattleScene 안의 아무 GameObject에나 붙이면 됨.
// Start에서 시선 게이지 호버 UI + 툴팁 + 100-4 미션 UI를 캔버스에 자동 생성하고
// GazeEffectManager / GazeHoverUI 참조를 연결한다.
public class GazeBattleUISetup : MonoBehaviour
{
    [Header("선택 (없으면 씬에서 첫 Canvas 자동 탐색)")]
    public Canvas targetCanvas;

    [Header("호버 영역 배치")]
    public Vector2 hoverRowAnchoredPos = new Vector2(-220f, -120f); // 캔버스 우상단 기준
    public Vector2 hoverZoneSize = new Vector2(48f, 48f);
    public float hoverZoneSpacing = 12f;
    public Color zoneInactiveColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    public Color zoneActiveColor = new Color(0.85f, 0.6f, 0.2f, 0.95f);

    [Header("툴팁")]
    public Vector2 tooltipSize = new Vector2(360f, 160f);
    public Color tooltipBgColor = new Color(0f, 0f, 0f, 0.92f);

    [Header("미션 패널")]
    public Vector2 missionPanelSize = new Vector2(560f, 180f);
    public Vector2 missionPanelAnchoredPos = new Vector2(0f, -60f); // 상단 중앙 기준
    public Color missionBgColor = new Color(0.05f, 0f, 0.1f, 0.95f);

    void Start()
    {
        if (targetCanvas == null)
            targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[GazeBattleUISetup] Canvas 없음 - UI 생성 실패");
            return;
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        GameObject hoverRow = BuildHoverRow(font);
        GameObject tooltip = BuildTooltip(font);
        GameObject mission = BuildMissionPanel(font);

        WireGazeHoverUI(hoverRow, tooltip);
        WireGazeManager(mission);
    }

    // ─── 호버 영역 ────────────────────────────────────────────────
    private List<GazeHoverUI.ThresholdSlot> generatedSlots = new List<GazeHoverUI.ThresholdSlot>();

    GameObject BuildHoverRow(TMP_FontAsset font)
    {
        GameObject row = new GameObject("GazeHoverRow", typeof(RectTransform));
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.SetParent(targetCanvas.transform, false);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = hoverRowAnchoredPos;
        float width = 5 * hoverZoneSize.x + 4 * hoverZoneSpacing;
        rt.sizeDelta = new Vector2(width, hoverZoneSize.y);

        int[] thresholds = { 20, 40, 60, 80, 100 };
        for (int i = 0; i < 5; i++)
        {
            GameObject zone = new GameObject($"Zone_{thresholds[i]}",
                typeof(RectTransform), typeof(Image));
            RectTransform zr = zone.GetComponent<RectTransform>();
            zr.SetParent(rt, false);
            zr.anchorMin = new Vector2(0f, 0.5f);
            zr.anchorMax = new Vector2(0f, 0.5f);
            zr.pivot = new Vector2(0f, 0.5f);
            float x = i * (hoverZoneSize.x + hoverZoneSpacing);
            zr.anchoredPosition = new Vector2(x, 0f);
            zr.sizeDelta = hoverZoneSize;

            Image img = zone.GetComponent<Image>();
            img.color = zoneInactiveColor;
            img.raycastTarget = true;

            // 숫자 라벨
            GameObject lbl = new GameObject("Label",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform lr = lbl.GetComponent<RectTransform>();
            lr.SetParent(zr, false);
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            TextMeshProUGUI t = lbl.GetComponent<TextMeshProUGUI>();
            t.text = thresholds[i].ToString();
            t.font = font;
            t.fontSize = 22;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;

            generatedSlots.Add(new GazeHoverUI.ThresholdSlot
            {
                threshold = thresholds[i],
                hoverZone = zone,
                highlightImage = img,
                iconSlot = null,
            });
        }
        return row;
    }

    // ─── 툴팁 ─────────────────────────────────────────────────────
    private GameObject tooltipNameGO, tooltipBuffGO, tooltipDebuffGO;

    GameObject BuildTooltip(TMP_FontAsset font)
    {
        GameObject tip = new GameObject("GazeTooltip",
            typeof(RectTransform), typeof(Image));
        RectTransform rt = tip.GetComponent<RectTransform>();
        rt.SetParent(targetCanvas.transform, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 1f); // 좌상단 기준 → 마우스 아래로 내려감
        rt.sizeDelta = tooltipSize;
        tip.GetComponent<Image>().color = tooltipBgColor;

        tooltipNameGO   = AddText(rt, "Name",   font, 22, new Vector2(10, -10),  new Vector2(-10, -38));
        tooltipBuffGO   = AddText(rt, "Buff",   font, 18, new Vector2(10, -42),  new Vector2(-10, -90));
        tooltipDebuffGO = AddText(rt, "Debuff", font, 18, new Vector2(10, -94),  new Vector2(-10, -150));

        tip.SetActive(false);
        return tip;
    }

    // 좌상단 기준 offset
    GameObject AddText(RectTransform parent, string name, TMP_FontAsset font, float size,
                        Vector2 topLeft, Vector2 bottomRight)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rt.offsetMax = new Vector2(bottomRight.x, topLeft.y);

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        return go;
    }

    // ─── 미션 패널 ────────────────────────────────────────────────
    private GameObject missionTitleGO, missionCondGO, missionRewardGO, missionImgGO;

    GameObject BuildMissionPanel(TMP_FontAsset font)
    {
        GameObject panel = new GameObject("GazeMissionPanel",
            typeof(RectTransform), typeof(Image));
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.SetParent(targetCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = missionPanelAnchoredPos;
        rt.sizeDelta = missionPanelSize;
        panel.GetComponent<Image>().color = missionBgColor;

        // 좌측 아이콘 슬롯 (나중에 아트 교체)
        missionImgGO = new GameObject("Icon",
            typeof(RectTransform), typeof(Image));
        RectTransform ir = missionImgGO.GetComponent<RectTransform>();
        ir.SetParent(rt, false);
        ir.anchorMin = new Vector2(0f, 0.5f);
        ir.anchorMax = new Vector2(0f, 0.5f);
        ir.pivot = new Vector2(0f, 0.5f);
        ir.anchoredPosition = new Vector2(12f, 0f);
        ir.sizeDelta = new Vector2(120f, 120f);
        Image img = missionImgGO.GetComponent<Image>();
        img.color = new Color(0.15f, 0.05f, 0.2f, 1f);
        img.raycastTarget = false;

        missionTitleGO  = AddText(rt, "Title",  font, 26, new Vector2(150, -10), new Vector2(-10, -50));
        missionCondGO   = AddText(rt, "Cond",   font, 20, new Vector2(150, -55), new Vector2(-10, -110));
        missionRewardGO = AddText(rt, "Reward", font, 18, new Vector2(150, -115), new Vector2(-10, -170));

        panel.SetActive(false);
        return panel;
    }

    // ─── 와이어링 ─────────────────────────────────────────────────
    void WireGazeHoverUI(GameObject hoverRow, GameObject tooltip)
    {
        GazeHoverUI ui = gameObject.GetComponent<GazeHoverUI>();
        if (ui == null) ui = gameObject.AddComponent<GazeHoverUI>();
        ui.slots = generatedSlots;
        ui.tooltipPanel = tooltip;
        ui.tooltipRect = tooltip.GetComponent<RectTransform>();
        ui.tooltipNameText = tooltipNameGO.GetComponent<TextMeshProUGUI>();
        ui.tooltipBuffText = tooltipBuffGO.GetComponent<TextMeshProUGUI>();
        ui.tooltipDebuffText = tooltipDebuffGO.GetComponent<TextMeshProUGUI>();
    }

    void WireGazeManager(GameObject mission)
    {
        if (GazeEffectManager.Instance == null) return;
        GazeEffectManager m = GazeEffectManager.Instance;
        m.missionPanel = mission;
        m.missionTitleText = missionTitleGO.GetComponent<TextMeshProUGUI>();
        m.missionConditionText = missionCondGO.GetComponent<TextMeshProUGUI>();
        m.missionRewardText = missionRewardGO.GetComponent<TextMeshProUGUI>();
        m.missionImage = missionImgGO.GetComponent<Image>();
    }
}
