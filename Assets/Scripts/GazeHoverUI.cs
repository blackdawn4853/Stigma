using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GazeHoverUI : MonoBehaviour
{
    [System.Serializable]
    public class ThresholdSlot
    {
        public int threshold;        // 20 / 40 / 60 / 80 / 100
        public GameObject hoverZone; // 마우스 인식 영역 (Image, raycastTarget=true)
        public Image highlightImage; // 활성화 시 강조용 (Optional)
        public Image iconSlot;       // 효과 아이콘 (나중에 아트 교체)
    }

    [Header("구간 슬롯")]
    public List<ThresholdSlot> slots = new List<ThresholdSlot>();

    [Header("툴팁 패널")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipBuffText;
    public TextMeshProUGUI tooltipDebuffText;
    public RectTransform tooltipRect;
    public Vector2 tooltipOffset = new Vector2(15f, -10f); // 마우스 우측·아래로

    [Header("강조 색상")]
    public Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private RectTransform canvasRect;
    private Canvas canvasRef;

    void Start()
    {
        canvasRef = GetComponentInParent<Canvas>();
        if (canvasRef != null) canvasRect = canvasRef.GetComponent<RectTransform>();
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        foreach (var slot in slots)
        {
            if (slot.hoverZone == null) continue;
            HoverProxy proxy = slot.hoverZone.GetComponent<HoverProxy>();
            if (proxy == null) proxy = slot.hoverZone.AddComponent<HoverProxy>();
            proxy.Bind(this, slot.threshold);
        }
    }

    void Update()
    {
        RefreshHighlights();
        if (tooltipPanel != null && tooltipPanel.activeSelf)
            FollowMouse();
    }

    void RefreshHighlights()
    {
        if (BattleManager.Instance == null) return;
        int gaze = BattleManager.Instance.gazeLevel;
        foreach (var slot in slots)
        {
            if (slot.highlightImage == null) continue;
            bool active = gaze >= slot.threshold;
            slot.highlightImage.color = active ? activeColor : inactiveColor;
        }

        if (GazeEffectManager.Instance != null)
        {
            foreach (var slot in slots)
            {
                if (slot.iconSlot == null) continue;
                GazeEffectData data = GazeEffectManager.Instance.GetEffectAt(slot.threshold);
                if (data != null && data.icon != null)
                {
                    slot.iconSlot.sprite = data.icon;
                    slot.iconSlot.enabled = true;
                }
            }
        }
    }

    public void ShowTooltip(int threshold)
    {
        if (tooltipPanel == null || GazeEffectManager.Instance == null) return;
        GazeEffectData data = GazeEffectManager.Instance.GetEffectAt(threshold);
        if (data == null) return;

        if (tooltipNameText != null)
            tooltipNameText.text = $"[{threshold}] {data.displayName}";
        if (tooltipBuffText != null)
            tooltipBuffText.text = $"<color=#3FCB6E>버프: {data.buffDescription}</color>";
        if (tooltipDebuffText != null)
            tooltipDebuffText.text = $"<color=#E25555>디버프: {data.debuffDescription}</color>";

        tooltipPanel.SetActive(true);
        FollowMouse();
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    void FollowMouse()
    {
        if (tooltipRect == null) return;

        RectTransform parentRect = tooltipRect.parent as RectTransform;
        if (parentRect == null) return;

        Camera cam = (canvasRef != null && canvasRef.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvasRef.worldCamera : null;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, Input.mousePosition, cam, out local);

        // 부모 RectTransform의 local 좌표를 anchor 기준 좌표로 변환
        Vector2 pivotOffset = new Vector2(
            parentRect.rect.width * (tooltipRect.anchorMin.x - parentRect.pivot.x),
            parentRect.rect.height * (tooltipRect.anchorMin.y - parentRect.pivot.y));

        tooltipRect.anchoredPosition = local + tooltipOffset - pivotOffset;
    }

    private class HoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private GazeHoverUI owner;
        private int threshold;

        public void Bind(GazeHoverUI owner, int threshold)
        {
            this.owner = owner;
            this.threshold = threshold;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null) owner.ShowTooltip(threshold);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != null) owner.HideTooltip();
        }
    }
}
