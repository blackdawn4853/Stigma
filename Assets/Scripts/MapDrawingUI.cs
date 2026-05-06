using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// 노드맵 드로잉 UI — 좌하단 버튼 바 (펜슬/지우개/전부지우기) + 색상 패널 + 확인 팝업 + 커서 이미지.
// MapDrawingManager 와 같은 GameObject 에 부착됨 (NodeMap 진입 시 자동 스폰).
// 자체 Canvas 를 만들어 ScreenSpaceOverlay 로 그림.
public class MapDrawingUI : MonoBehaviour
{
    [Header("UI 위치 / 크기")]
    [Tooltip("버튼 바 좌하단 기준 위치 (캔버스 좌하단 → 버튼 바 좌하단까지의 offset).")]
    public Vector2 buttonBarAnchoredPos = new Vector2(24f, 24f);
    [Tooltip("각 버튼 크기.")]
    public Vector2 buttonSize = new Vector2(56f, 56f);
    [Tooltip("버튼 사이 간격.")]
    public float buttonSpacing = 10f;

    [Header("색상 패널 (펜슬 버튼 클릭 시)")]
    [Tooltip("펜슬 버튼 위쪽으로의 offset.")]
    public Vector2 colorPanelOffset = new Vector2(0f, 70f);
    public float colorButtonSize = 44f;
    public float colorButtonSpacing = 8f;

    [Header("아이콘 (placeholder OK — 비어있으면 색상 사각형)")]
    public Sprite pencilIcon;
    public Sprite eraserIcon;
    public Sprite trashIcon;
    [Tooltip("그리기 모드 시 마우스 커서 이미지.")]
    public Sprite cursorPencilIcon;
    [Tooltip("지우기 모드 시 마우스 커서 이미지.")]
    public Sprite cursorEraserIcon;

    [Header("커서 이미지")]
    public Vector2 cursorSize = new Vector2(32f, 32f);
    [Tooltip("커서 이미지의 어느 점이 마우스 위치에 오는지 (0,0)=좌하단, (0,1)=좌상단.")]
    public Vector2 cursorPivot = new Vector2(0f, 1f);

    [Header("색상 팔레트 — MapDrawingManager 와 동일")]
    public Color colorRed = new Color(0.95f, 0.2f, 0.2f);
    public Color colorBlue = new Color(0.25f, 0.45f, 0.95f);
    public Color colorBlack = new Color(0.05f, 0.05f, 0.05f);
    public Color colorWhite = new Color(1f, 1f, 1f);

    [Header("색상 (placeholder — 아트 들어오면 무관)")]
    public Color buttonBgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
    public Color buttonHoverColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    public Color buttonActiveColor = new Color(0.85f, 0.6f, 0.25f, 1f); // 모드 활성 표시
    public Color panelBgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    public Color popupBgColor = new Color(0.13f, 0.13f, 0.17f, 1f);
    public Color popupOverlayColor = new Color(0f, 0f, 0f, 0.78f);

    [Header("기타")]
    [Tooltip("자체 Canvas sortingOrder. HUD 보다 위에 있어야 팝업이 가려지지 않음.")]
    public int canvasSortingOrder = 1100;

    // ─── 런타임 ──────────────────────────────────────────────────────
    private Canvas uiCanvas;
    private RectTransform uiCanvasRT;
    private Image pencilImg, eraserImg, clearImg;
    private GameObject colorPanel;
    private GameObject confirmPopup;
    private RectTransform cursorImage;
    private Image cursorImageImg;

    void Start()
    {
        BuildUI();
    }

    // ─── UI 빌드 ─────────────────────────────────────────────────────
    void BuildUI()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        // Canvas
        GameObject canvasGO = new GameObject("MapDrawingUICanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        uiCanvas = canvasGO.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        uiCanvasRT = (RectTransform)canvasGO.transform;

        // 좌하단 버튼 바
        GameObject barGO = new GameObject("DrawButtonBar", typeof(RectTransform));
        barGO.transform.SetParent(uiCanvas.transform, false);
        var barRT = (RectTransform)barGO.transform;
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(0f, 0f);
        barRT.pivot = new Vector2(0f, 0f);
        barRT.anchoredPosition = buttonBarAnchoredPos;
        barRT.sizeDelta = new Vector2(buttonSize.x * 3 + buttonSpacing * 2, buttonSize.y);

        pencilImg = CreateButton(barRT, "PencilBtn", 0, pencilIcon, OnPencilClicked);
        eraserImg = CreateButton(barRT, "EraserBtn", 1, eraserIcon, OnEraserClicked);
        clearImg  = CreateButton(barRT, "ClearBtn",  2, trashIcon,  OnClearClicked);

        BuildColorPanel(barRT, font);
        BuildConfirmPopup(font);
        BuildCursorImage();
    }

    Image CreateButton(RectTransform parent, string name, int idx, Sprite icon, UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(idx * (buttonSize.x + buttonSpacing), 0f);
        rt.sizeDelta = buttonSize;
        Image img = go.GetComponent<Image>();
        if (icon != null) { img.sprite = icon; img.color = Color.white; }
        else              { img.color = buttonBgColor; }
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = buttonHoverColor;
        cb.pressedColor = buttonActiveColor;
        cb.selectedColor = Color.white;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        return img;
    }

    void BuildColorPanel(RectTransform parentBar, TMP_FontAsset font)
    {
        GameObject panel = new GameObject("ColorPanel",
            typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parentBar, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = colorPanelOffset;
        float w = colorButtonSize * 4 + colorButtonSpacing * 3 + 16f;
        float h = colorButtonSize + 16f;
        rt.sizeDelta = new Vector2(w, h);
        var bg = panel.GetComponent<Image>();
        bg.color = panelBgColor;

        Color[] palette = { colorRed, colorBlue, colorBlack, colorWhite };
        for (int i = 0; i < palette.Length; i++)
        {
            int captured = i;
            Color c = palette[i];
            GameObject cb = new GameObject($"Color_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            cb.transform.SetParent(rt, false);
            var crt = (RectTransform)cb.transform;
            crt.anchorMin = new Vector2(0f, 0.5f);
            crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(8f + i * (colorButtonSize + colorButtonSpacing), 0f);
            crt.sizeDelta = new Vector2(colorButtonSize, colorButtonSize);
            Image cimg = cb.GetComponent<Image>();
            cimg.color = c;
            Button cbtn = cb.GetComponent<Button>();
            cbtn.targetGraphic = cimg;
            cbtn.onClick.AddListener(() => OnColorSelected(captured));
        }

        colorPanel = panel;
        colorPanel.SetActive(false);
    }

    void BuildConfirmPopup(TMP_FontAsset font)
    {
        // 오버레이 (배경 어둡게)
        GameObject overlay = new GameObject("ConfirmOverlay",
            typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(uiCanvas.transform, false);
        var ort = (RectTransform)overlay.transform;
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
        var oimg = overlay.GetComponent<Image>();
        oimg.color = popupOverlayColor;
        // 오버레이 클릭 = 취소
        var obtn = overlay.GetComponent<Button>();
        obtn.transition = Selectable.Transition.None;
        obtn.targetGraphic = oimg;
        obtn.onClick.AddListener(() => confirmPopup.SetActive(false));

        // 모달 박스 (오버레이의 자식)
        GameObject popup = new GameObject("ConfirmBox",
            typeof(RectTransform), typeof(Image));
        popup.transform.SetParent(overlay.transform, false);
        var prt = (RectTransform)popup.transform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(460, 200);
        var pbg = popup.GetComponent<Image>();
        pbg.color = popupBgColor;
        // 박스는 클릭 차단 (오버레이 onClick 으로 전파 방지)
        // — Image.raycastTarget=true (기본) 으로 이미 차단됨

        // 메시지 텍스트
        GameObject msgGO = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgGO.transform.SetParent(prt, false);
        var mrt = (RectTransform)msgGO.transform;
        mrt.anchorMin = new Vector2(0f, 1f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(0.5f, 1f);
        mrt.anchoredPosition = new Vector2(0f, -24f);
        mrt.sizeDelta = new Vector2(-32f, 90f);
        var msgTxt = msgGO.GetComponent<TextMeshProUGUI>();
        msgTxt.font = font;
        msgTxt.text = "모든 드로잉을 삭제하시겠습니까?";
        msgTxt.fontSize = 22f;
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.color = Color.white;
        msgTxt.raycastTarget = false;
        msgTxt.textWrappingMode = TextWrappingModes.Normal;

        // 확인 / 취소 버튼
        CreateModalButton(prt, "OK", "확인", font,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(60f, 24f),
            new Color(0.7f, 0.25f, 0.25f, 1f),
            () => { if (MapDrawingManager.Instance != null) MapDrawingManager.Instance.ClearAll(); confirmPopup.SetActive(false); });
        CreateModalButton(prt, "Cancel", "취소", font,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60f, 24f),
            new Color(0.3f, 0.3f, 0.35f, 1f),
            () => confirmPopup.SetActive(false));

        confirmPopup = overlay;
        confirmPopup.SetActive(false);
    }

    void CreateModalButton(RectTransform parent, string name, string label, TMP_FontAsset font,
                            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
                            Color bgColor, UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(110f, 44f);
        var img = go.GetComponent<Image>();
        img.color = bgColor;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject lblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(rt, false);
        var lrt = (RectTransform)lblGO.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var ltxt = lblGO.GetComponent<TextMeshProUGUI>();
        ltxt.font = font;
        ltxt.text = label;
        ltxt.fontSize = 20f;
        ltxt.alignment = TextAlignmentOptions.Center;
        ltxt.color = Color.white;
        ltxt.raycastTarget = false;
    }

    void BuildCursorImage()
    {
        GameObject go = new GameObject("DrawCursor", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(uiCanvas.transform, false);
        cursorImage = (RectTransform)go.transform;
        cursorImage.anchorMin = new Vector2(0f, 0f);
        cursorImage.anchorMax = new Vector2(0f, 0f);
        cursorImage.pivot = cursorPivot;
        cursorImage.sizeDelta = cursorSize;
        cursorImageImg = go.GetComponent<Image>();
        cursorImageImg.raycastTarget = false;
        cursorImageImg.preserveAspect = true;
        cursorImage.SetAsLastSibling();
        go.SetActive(false);
    }

    // ─── 버튼 핸들러 ─────────────────────────────────────────────────
    void OnPencilClicked()
    {
        var mgr = MapDrawingManager.Instance;
        if (mgr == null) return;
        if (mgr.currentMode == MapDrawingManager.Mode.Drawing)
        {
            // 그리기 모드 중 → 해제
            mgr.SetNoneMode();
            colorPanel.SetActive(false);
        }
        else
        {
            // 색상 패널 토글
            colorPanel.SetActive(!colorPanel.activeSelf);
        }
    }

    void OnColorSelected(int idx)
    {
        Color[] palette = { colorRed, colorBlue, colorBlack, colorWhite };
        Color c = palette[Mathf.Clamp(idx, 0, palette.Length - 1)];
        if (MapDrawingManager.Instance != null)
            MapDrawingManager.Instance.SetPencilMode(c);
        colorPanel.SetActive(false);
    }

    void OnEraserClicked()
    {
        var mgr = MapDrawingManager.Instance;
        if (mgr == null) return;
        if (mgr.currentMode == MapDrawingManager.Mode.Erasing)
            mgr.SetNoneMode();
        else
        {
            mgr.SetEraserMode();
            colorPanel.SetActive(false);
        }
    }

    void OnClearClicked()
    {
        if (confirmPopup != null)
        {
            confirmPopup.SetActive(true);
            confirmPopup.transform.SetAsLastSibling();
            // 커서가 팝업 위에 있을 때를 대비해 잠시 모드 해제하지 않음 — 그대로 두면 됨
        }
    }

    // ─── 매 프레임 갱신 ──────────────────────────────────────────────
    void Update()
    {
        var mgr = MapDrawingManager.Instance;
        if (mgr == null) return;

        // 버튼 활성화 색상 (placeholder — 아트 들어오면 그대로 두거나 별도 강조 가능)
        if (pencilImg != null)
            pencilImg.color = (mgr.currentMode == MapDrawingManager.Mode.Drawing) ? buttonActiveColor : Color.white;
        if (eraserImg != null)
            eraserImg.color = (mgr.currentMode == MapDrawingManager.Mode.Erasing) ? buttonActiveColor : Color.white;

        // 커서 이미지
        bool active = (mgr.currentMode != MapDrawingManager.Mode.None);
        if (cursorImage != null)
        {
            cursorImage.gameObject.SetActive(active);
            if (active)
            {
                if (mgr.currentMode == MapDrawingManager.Mode.Drawing)
                {
                    if (cursorPencilIcon != null)
                    { cursorImageImg.sprite = cursorPencilIcon; cursorImageImg.color = mgr.currentColor; }
                    else
                    { cursorImageImg.sprite = null; cursorImageImg.color = mgr.currentColor; }
                }
                else // Erasing
                {
                    if (cursorEraserIcon != null)
                    { cursorImageImg.sprite = cursorEraserIcon; cursorImageImg.color = Color.white; }
                    else
                    { cursorImageImg.sprite = null; cursorImageImg.color = new Color(0.9f, 0.9f, 0.9f, 0.7f); }
                }
                // 마우스 따라가기
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    uiCanvasRT, Input.mousePosition, null, out local);
                cursorImage.anchoredPosition = local + uiCanvasRT.rect.size * 0.5f;
            }
        }

        // 시스템 커서 숨김 (모드 활성화 시)
        Cursor.visible = !active;
    }

    void OnDisable()
    {
        // 시스템 커서 복원
        Cursor.visible = true;
    }
}
