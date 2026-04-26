using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// 낙인 노드 씬 매니저
// - 5개 시선 효과(20/40/60/80/100) 중 하나를 다른 효과로 교체하거나
// - 덱에서 카드 한 장 제거
// 둘 중 하나만 선택 가능. 선택 후 다른 옵션은 X 표시로 잠금.
public class BrandNodeManager : MonoBehaviour
{
    [Header("UI 위치/크기")]
    public Vector2 titleAnchoredPos = new Vector2(0f, -80f);
    public Vector2 effectsRowAnchoredPos = new Vector2(0f, -220f);
    public Vector2 effectCardSize = new Vector2(220f, 240f);
    public float effectCardSpacing = 20f;
    public Vector2 cardRemoveButtonAnchoredPos = new Vector2(0f, -560f);
    public Vector2 cardRemoveButtonSize = new Vector2(360f, 80f);
    public Vector2 returnButtonAnchoredPos = new Vector2(0f, -680f);
    public Vector2 returnButtonSize = new Vector2(360f, 80f);

    [Header("씬 참조 (선택 — 미연결 시 자동 검색)")]
    public Canvas sceneCanvas;

    [Header("카드 비주얼")]
    [Tooltip("덱 카드 항목에 사용할 통일 스프라이트 (Assets/Sprites/card1.png 권장). 추후 카드별로 분기 가능.")]
    public Sprite cardSprite;
    public Color cardSelectedColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("색상")]
    public Color bgColor = new Color(0.1f, 0.05f, 0.15f, 1f);
    public Color cardBgColor = new Color(0.18f, 0.1f, 0.25f, 1f);
    public Color cardLockedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
    public Color buttonColor = new Color(0.3f, 0.15f, 0.4f, 1f);
    public Color buttonDisabledColor = new Color(0.15f, 0.1f, 0.18f, 1f);
    public Color modalDimColor = new Color(0f, 0f, 0f, 0.7f);
    public Color modalBgColor = new Color(0.12f, 0.08f, 0.18f, 1f);

    Canvas canvas;
    Transform root;
    bool actionLocked;
    readonly int[] thresholds = new int[] { 20, 40, 60, 80, 100 };
    Dictionary<int, EffectCardUI> effectCards = new Dictionary<int, EffectCardUI>();
    Button cardRemoveButton;
    GameObject cardRemoveX;
    GameObject modalRoot;
    Button returnButton;

    void Start()
    {
        canvas = sceneCanvas != null ? sceneCanvas : FindCanvasInOwnScene();
        if (canvas == null)
        {
            Debug.LogError("[Brand] Canvas not found in BrandNodeScene");
            return;
        }
        root = canvas.transform;

        // 씬에 미리 만들어둔 UI가 있으면 그것을 사용. 없으면 절차적 생성(빈 씬 폴백).
        bool wired = WireExistingUI();
        Debug.Log($"[Brand] Canvas={canvas.name} (scene={canvas.gameObject.scene.name}), wireExisting={wired}");
        if (!wired)
        {
            BuildBackground();
            BuildTitle();
            BuildEffectsRow();
            BuildCardRemoveButton();
            BuildReturnButton();
        }
    }

    Canvas FindCanvasInOwnScene()
    {
        var ownScene = gameObject.scene;
        var all = FindObjectsOfType<Canvas>();
        // 자기 씬 + 자식이 있는 Canvas 우선 선택
        Canvas withChildren = null, anyInScene = null;
        foreach (var c in all)
        {
            if (c.gameObject.scene != ownScene) continue;
            if (anyInScene == null) anyInScene = c;
            if (c.transform.childCount > 0 && withChildren == null) withChildren = c;
        }
        if (withChildren != null) return withChildren;
        if (anyInScene != null) return anyInScene;
        return all.Length > 0 ? all[0] : null;
    }

    bool WireExistingUI()
    {
        var bg = root.Find("Background");
        if (bg == null) return false;

        // 효과 카드 5개
        var row = root.Find("EffectsRow");
        if (row != null)
        {
            for (int i = 0; i < thresholds.Length; i++)
            {
                int threshold = thresholds[i];
                var card = row.Find($"EffectCard_{threshold}");
                if (card == null) continue;
                int captured = threshold;

                var btn = card.GetComponent<Button>();
                var img = card.GetComponent<Image>();
                var nameTmp = SafeGetText(card.Find("EffectName"));
                var descTmp = SafeGetText(card.Find("Desc"));
                var lockX = card.Find("LockX")?.gameObject;

                if (nameTmp != null) nameTmp.text = GetEffectName(threshold);
                if (descTmp != null) descTmp.text = GetEffectDesc(threshold);
                if (lockX != null) lockX.SetActive(false);
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnEffectCardClicked(captured));
                }

                effectCards[threshold] = new EffectCardUI
                {
                    root = card.gameObject,
                    image = img,
                    button = btn,
                    nameText = nameTmp,
                    descText = descTmp,
                    lockX = lockX
                };
            }
        }

        // 카드 제거 버튼
        var removeT = root.Find("CardRemoveButton");
        if (removeT != null)
        {
            cardRemoveButton = removeT.GetComponent<Button>();
            if (cardRemoveButton != null)
            {
                cardRemoveButton.onClick.RemoveAllListeners();
                cardRemoveButton.onClick.AddListener(OpenCardRemoveModal);
            }
            cardRemoveX = removeT.Find("LockX")?.gameObject;
            if (cardRemoveX != null) cardRemoveX.SetActive(false);
        }

        // 돌아가기 버튼
        var returnT = root.Find("ReturnButton");
        if (returnT != null)
        {
            returnButton = returnT.GetComponent<Button>();
            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(ReturnToMap);
            }
        }

        return true;
    }

    TextMeshProUGUI SafeGetText(Transform t) => t != null ? t.GetComponent<TextMeshProUGUI>() : null;

    // ────────────────────────────────────────────────────────────────
    // UI 구축
    // ────────────────────────────────────────────────────────────────
    void BuildBackground()
    {
        var bg = NewUI("Background", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        bg.image.color = bgColor;
    }

    void BuildTitle()
    {
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(root, false);
        var rt = titleGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = titleAnchoredPos;
        rt.sizeDelta = new Vector2(900f, 100f);
        var tmp = titleGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "낙인";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 64;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.95f, 0.85f, 1f);

        var subGO = new GameObject("Subtitle", typeof(RectTransform));
        subGO.transform.SetParent(root, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = titleAnchoredPos + new Vector2(0f, -70f);
        srt.sizeDelta = new Vector2(1200f, 50f);
        var stmp = subGO.AddComponent<TextMeshProUGUI>();
        stmp.text = "시선 효과 하나를 교체하거나, 덱에서 카드 하나를 제거하시오. (선택은 한 번뿐)";
        stmp.alignment = TextAlignmentOptions.Center;
        stmp.fontSize = 26;
        stmp.color = new Color(0.85f, 0.78f, 0.92f);
    }

    void BuildEffectsRow()
    {
        var rowGO = new GameObject("EffectsRow", typeof(RectTransform));
        rowGO.transform.SetParent(root, false);
        var rt = rowGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = effectsRowAnchoredPos;
        rt.sizeDelta = new Vector2(0f, effectCardSize.y);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = effectCardSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        for (int i = 0; i < thresholds.Length; i++)
        {
            int threshold = thresholds[i];
            var card = BuildEffectCard(rowGO.transform, threshold);
            effectCards[threshold] = card;
        }
    }

    EffectCardUI BuildEffectCard(Transform parent, int threshold)
    {
        var card = new GameObject($"EffectCard_{threshold}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);
        var rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = effectCardSize;
        var img = card.GetComponent<Image>();
        img.color = cardBgColor;
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = img;

        // Threshold label
        var thrGO = new GameObject("Threshold", typeof(RectTransform));
        thrGO.transform.SetParent(card.transform, false);
        var thrRT = thrGO.GetComponent<RectTransform>();
        thrRT.anchorMin = new Vector2(0f, 1f);
        thrRT.anchorMax = new Vector2(1f, 1f);
        thrRT.pivot = new Vector2(0.5f, 1f);
        thrRT.anchoredPosition = new Vector2(0f, -10f);
        thrRT.sizeDelta = new Vector2(0f, 40f);
        var thrTmp = thrGO.AddComponent<TextMeshProUGUI>();
        thrTmp.text = threshold.ToString();
        thrTmp.alignment = TextAlignmentOptions.Center;
        thrTmp.fontSize = 32;
        thrTmp.fontStyle = FontStyles.Bold;
        thrTmp.color = new Color(1f, 0.7f, 0.85f);

        // Effect name
        var nameGO = new GameObject("EffectName", typeof(RectTransform));
        nameGO.transform.SetParent(card.transform, false);
        var nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(1f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, 10f);
        nrt.sizeDelta = new Vector2(-16f, 60f);
        var nTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nTmp.text = GetEffectName(threshold);
        nTmp.alignment = TextAlignmentOptions.Center;
        nTmp.fontSize = 22;
        nTmp.fontStyle = FontStyles.Bold;
        nTmp.color = Color.white;
        nTmp.enableWordWrapping = true;

        // Description
        var descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(card.transform, false);
        var drt = descGO.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(1f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, 10f);
        drt.sizeDelta = new Vector2(-16f, -10f);
        var dTmp = descGO.AddComponent<TextMeshProUGUI>();
        dTmp.text = GetEffectDesc(threshold);
        dTmp.alignment = TextAlignmentOptions.Top;
        dTmp.fontSize = 14;
        dTmp.color = new Color(0.85f, 0.8f, 0.9f);
        dTmp.enableWordWrapping = true;

        // X mark (lock indicator)
        var xGO = new GameObject("LockX", typeof(RectTransform));
        xGO.transform.SetParent(card.transform, false);
        var xrt = xGO.GetComponent<RectTransform>();
        xrt.anchorMin = Vector2.zero;
        xrt.anchorMax = Vector2.one;
        xrt.offsetMin = xrt.offsetMax = Vector2.zero;
        var xTmp = xGO.AddComponent<TextMeshProUGUI>();
        xTmp.text = "✕";
        xTmp.alignment = TextAlignmentOptions.Center;
        xTmp.fontSize = 140;
        xTmp.fontStyle = FontStyles.Bold;
        xTmp.color = new Color(1f, 0.3f, 0.3f, 0.85f);
        xGO.SetActive(false);

        var ui = new EffectCardUI
        {
            root = card,
            image = img,
            button = btn,
            nameText = nTmp,
            descText = dTmp,
            lockX = xGO
        };
        btn.onClick.AddListener(() => OnEffectCardClicked(threshold));
        return ui;
    }

    void BuildCardRemoveButton()
    {
        var btnGO = new GameObject("CardRemoveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(root, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = cardRemoveButtonAnchoredPos;
        rt.sizeDelta = cardRemoveButtonSize;
        var img = btnGO.GetComponent<Image>();
        img.color = buttonColor;
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OpenCardRemoveModal);
        cardRemoveButton = btn;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "덱에서 카드 한 장 제거";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;

        // X mark
        var xGO = new GameObject("LockX", typeof(RectTransform));
        xGO.transform.SetParent(btnGO.transform, false);
        var xrt = xGO.GetComponent<RectTransform>();
        xrt.anchorMin = Vector2.zero;
        xrt.anchorMax = Vector2.one;
        xrt.offsetMin = xrt.offsetMax = Vector2.zero;
        var xTmp = xGO.AddComponent<TextMeshProUGUI>();
        xTmp.text = "✕";
        xTmp.alignment = TextAlignmentOptions.Center;
        xTmp.fontSize = 60;
        xTmp.fontStyle = FontStyles.Bold;
        xTmp.color = new Color(1f, 0.3f, 0.3f, 0.85f);
        xGO.SetActive(false);
        cardRemoveX = xGO;
    }

    void BuildReturnButton()
    {
        var btnGO = new GameObject("ReturnButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(root, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = returnButtonAnchoredPos;
        rt.sizeDelta = returnButtonSize;
        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.25f, 0.3f, 0.4f, 1f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ReturnToMap);
        returnButton = btn;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "맵으로 돌아가기";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
    }

    // ────────────────────────────────────────────────────────────────
    // 인터랙션
    // ────────────────────────────────────────────────────────────────
    void OnEffectCardClicked(int threshold)
    {
        if (actionLocked) return;
        OpenEffectSwapModal(threshold);
    }

    void OpenEffectSwapModal(int threshold)
    {
        CloseModal();
        var pool = GetPool(threshold);
        var current = GazeEffectManager.Instance != null ? GazeEffectManager.Instance.GetEffectAt(threshold) : null;

        var dim = NewUI("Modal", root, Vector2.zero, Vector2.one);
        var dimRT = dim.rectTransform;
        dimRT.offsetMin = dimRT.offsetMax = Vector2.zero;
        dim.image.color = modalDimColor;
        modalRoot = dim.gameObject;

        var panel = NewUI("Panel", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(900f, 700f);
        panel.image.color = modalBgColor;

        AddText(panel.transform, $"{threshold} 구간 효과 교체", new Vector2(0f, -20f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 32, FontStyles.Bold,
            new Color(1f, 0.85f, 0.95f), new Vector2(800f, 50f));

        var listGO = new GameObject("List", typeof(RectTransform));
        listGO.transform.SetParent(panel.transform, false);
        var lrt = listGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 1f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -90f);
        lrt.sizeDelta = new Vector2(800f, 0f);
        var vlg = listGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        bool anyOption = false;
        if (pool != null)
        {
            foreach (var effect in pool)
            {
                if (effect == null) continue;
                if (effect == current) continue;
                anyOption = true;
                var capturedEffect = effect;
                BuildModalEntry(listGO.transform, effect.displayName, FormatEffectDesc(effect),
                    () => { ApplyEffectSwap(threshold, capturedEffect); });
            }
        }
        if (!anyOption)
        {
            AddText(listGO.transform, "교체 가능한 다른 효과가 없습니다.", Vector2.zero,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 22, FontStyles.Italic,
                new Color(0.85f, 0.7f, 0.7f), new Vector2(700f, 60f));
        }

        BuildModalCloseButton(panel.transform);
    }

    // 카드 제거 모달 상태
    int cardRemoveSelectedIndex = -1;
    Button cardRemoveDeleteBtn;
    TextMeshProUGUI cardRemoveDeleteLabel;
    readonly List<CardItemUI> cardRemoveItems = new List<CardItemUI>();

    void OpenCardRemoveModal()
    {
        if (actionLocked) return;
        CloseModal();
        cardRemoveSelectedIndex = -1;
        cardRemoveItems.Clear();

        // Dim background
        var dim = NewUI("Modal", root, Vector2.zero, Vector2.one);
        var dimRT = dim.rectTransform;
        dimRT.offsetMin = dimRT.offsetMax = Vector2.zero;
        dim.image.color = modalDimColor;
        modalRoot = dim.gameObject;

        // Panel
        var panel = NewUI("Panel", dim.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.rectTransform.sizeDelta = new Vector2(1400f, 900f);
        panel.image.color = modalBgColor;

        // Title
        AddText(panel.transform, "제거할 카드 선택", new Vector2(0f, -20f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 36, FontStyles.Bold,
            new Color(1f, 0.85f, 0.95f), new Vector2(1300f, 60f));

        // ScrollView (Footer와 분리: 패널 상단~하단 100px 위까지)
        BuildCardScrollView(panel.transform);

        // Footer (스크롤 영역과 분리, 패널 하단 고정)
        BuildCardRemoveFooter(panel.transform);
    }

    void BuildCardScrollView(Transform parent)
    {
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGO.transform.SetParent(parent, false);
        var srt = scrollGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 1f);
        srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -90f);
        srt.sizeDelta = new Vector2(1300f, 660f);
        var sbg = scrollGO.GetComponent<Image>();
        sbg.color = new Color(0.05f, 0.03f, 0.08f, 0.8f);

        // Viewport
        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vrt = viewportGO.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = new Vector2(8f, 8f);
        vrt.offsetMax = new Vector2(-8f, -8f);
        var vimg = viewportGO.GetComponent<Image>();
        vimg.color = new Color(1f, 1f, 1f, 0.01f);
        var mask = viewportGO.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var crt = contentGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 0f);

        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180f, 240f);
        grid.spacing = new Vector2(40f, 40f);
        grid.padding = new RectOffset(40, 40, 40, 40);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ScrollRect 연결
        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // 카드 항목 채우기
        if (GameManager.Instance != null && GameManager.Instance.playerDeck.Count > 0)
        {
            for (int i = 0; i < GameManager.Instance.playerDeck.Count; i++)
            {
                var card = GameManager.Instance.playerDeck[i];
                if (card == null) continue;
                int captured = i;
                var item = BuildCardItem(contentGO.transform, card, () => OnCardRemoveItemClicked(captured));
                cardRemoveItems.Add(item);
            }
        }
        else
        {
            AddText(contentGO.transform, "덱에 카드가 없습니다.", Vector2.zero,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 22, FontStyles.Italic,
                new Color(0.85f, 0.7f, 0.7f), new Vector2(700f, 60f));
        }
    }

    CardItemUI BuildCardItem(Transform parent, CardData card, System.Action onClick)
    {
        // Shop_CardPrefab(120x160)을 1.5배 확대(180x240). 자식들은 같은 비율로 배치.
        // 외곽 프레임 (등급 색) — 선택 시 노란색으로 변경
        var itemGO = new GameObject("CardItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        itemGO.transform.SetParent(parent, false);
        var rt = itemGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180f, 240f);
        var frameBg = itemGO.GetComponent<Image>();
        Color baseFrameColor = card.GetRarityColor();
        frameBg.color = baseFrameColor;
        var btn = itemGO.GetComponent<Button>();
        btn.targetGraphic = frameBg;
        btn.onClick.AddListener(() => onClick?.Invoke());
        var le = itemGO.GetComponent<LayoutElement>();
        le.preferredWidth = 180f;
        le.preferredHeight = 240f;

        // 카드 스프라이트 (프레임 안쪽 4px 여백) — 카드 전체 비주얼
        var artGO = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(itemGO.transform, false);
        var art = artGO.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(4f, 4f);
        art.offsetMax = new Vector2(-4f, -4f);
        var artImg = artGO.GetComponent<Image>();
        if (cardSprite != null)
        {
            artImg.sprite = cardSprite;
            artImg.color = Color.white;
            artImg.preserveAspect = false;
        }
        else
        {
            artImg.color = new Color(0.18f, 0.12f, 0.25f, 1f);
        }
        artImg.raycastTarget = false;

        // ManaCostText (Shop_CardPrefab: anchored (-43.24, 62.82), size 30 → 1.5배 적용)
        var manaGO = new GameObject("ManaCostText", typeof(RectTransform));
        manaGO.transform.SetParent(itemGO.transform, false);
        var mrt = manaGO.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0.5f);
        mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = new Vector2(-64.86f, 94.23f);
        mrt.sizeDelta = new Vector2(45f, 45f);
        var manaTmp = manaGO.AddComponent<TextMeshProUGUI>();
        manaTmp.text = card.manaCost.ToString();
        manaTmp.alignment = TextAlignmentOptions.Center;
        manaTmp.fontSize = 24;
        manaTmp.fontStyle = FontStyles.Bold;
        manaTmp.color = new Color(0.392f, 0.784f, 1f, 1f);
        manaTmp.raycastTarget = false;

        // CardNameText (Shop_CardPrefab: anchored (5.4, 71), size 53x13 → 1.5배 적용)
        var nameGO = new GameObject("CardNameText", typeof(RectTransform));
        nameGO.transform.SetParent(itemGO.transform, false);
        var nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0.5f, 0.5f);
        nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(8.1f, 106.5f);
        nrt.sizeDelta = new Vector2(80f, 20f);
        var nTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nTmp.text = card.cardName;
        nTmp.alignment = TextAlignmentOptions.Center;
        nTmp.fontSize = 21;
        nTmp.fontStyle = FontStyles.Bold;
        nTmp.color = Color.white;
        nTmp.raycastTarget = false;

        // DescriptionText (Shop_CardPrefab: anchored (0, -59.8), size 81x40 → 1.5배 적용)
        var descGO = new GameObject("DescriptionText", typeof(RectTransform));
        descGO.transform.SetParent(itemGO.transform, false);
        var drt = descGO.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.5f, 0.5f);
        drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -89.7f);
        drt.sizeDelta = new Vector2(122f, 60f);
        var dTmp = descGO.AddComponent<TextMeshProUGUI>();
        dTmp.text = card.description;
        dTmp.alignment = TextAlignmentOptions.Center;
        dTmp.fontSize = 16;
        dTmp.color = Color.white;
        dTmp.enableWordWrapping = true;
        dTmp.raycastTarget = false;

        return new CardItemUI { root = itemGO, frameBg = frameBg, baseFrameColor = baseFrameColor };
    }

    void BuildCardRemoveFooter(Transform parent)
    {
        var footerGO = new GameObject("Footer", typeof(RectTransform));
        footerGO.transform.SetParent(parent, false);
        var frt = footerGO.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 0f);
        frt.anchorMax = new Vector2(0.5f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0f, 30f);
        frt.sizeDelta = new Vector2(900f, 80f);

        var hlg = footerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 40f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // 삭제 버튼
        var delGO = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        delGO.transform.SetParent(footerGO.transform, false);
        var drt = delGO.GetComponent<RectTransform>();
        drt.sizeDelta = new Vector2(380f, 70f);
        var dimg = delGO.GetComponent<Image>();
        dimg.color = new Color(0.7f, 0.2f, 0.2f, 1f);
        var dbtn = delGO.GetComponent<Button>();
        dbtn.targetGraphic = dimg;
        dbtn.interactable = false;
        dbtn.onClick.AddListener(ConfirmCardRemove);
        cardRemoveDeleteBtn = dbtn;

        var dlblGO = new GameObject("Label", typeof(RectTransform));
        dlblGO.transform.SetParent(delGO.transform, false);
        var dlrt = dlblGO.GetComponent<RectTransform>();
        dlrt.anchorMin = Vector2.zero;
        dlrt.anchorMax = Vector2.one;
        dlrt.offsetMin = dlrt.offsetMax = Vector2.zero;
        var dlbl = dlblGO.AddComponent<TextMeshProUGUI>();
        dlbl.text = "카드 선택";
        dlbl.alignment = TextAlignmentOptions.Center;
        dlbl.fontSize = 24;
        dlbl.fontStyle = FontStyles.Bold;
        dlbl.color = Color.white;
        cardRemoveDeleteLabel = dlbl;

        // 취소 버튼
        var cancelGO = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelGO.transform.SetParent(footerGO.transform, false);
        var crt = cancelGO.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(280f, 70f);
        var cimg = cancelGO.GetComponent<Image>();
        cimg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
        var cbtn = cancelGO.GetComponent<Button>();
        cbtn.targetGraphic = cimg;
        cbtn.onClick.AddListener(CloseModal);

        var clblGO = new GameObject("Label", typeof(RectTransform));
        clblGO.transform.SetParent(cancelGO.transform, false);
        var clrt = clblGO.GetComponent<RectTransform>();
        clrt.anchorMin = Vector2.zero;
        clrt.anchorMax = Vector2.one;
        clrt.offsetMin = clrt.offsetMax = Vector2.zero;
        var clbl = clblGO.AddComponent<TextMeshProUGUI>();
        clbl.text = "취소";
        clbl.alignment = TextAlignmentOptions.Center;
        clbl.fontSize = 22;
        clbl.color = Color.white;
    }

    void OnCardRemoveItemClicked(int index)
    {
        if (actionLocked) return;
        if (GameManager.Instance == null) return;
        if (index < 0 || index >= GameManager.Instance.playerDeck.Count) return;

        cardRemoveSelectedIndex = index;
        // 강조 갱신: 선택된 카드의 프레임 색을 노란색으로
        for (int i = 0; i < cardRemoveItems.Count; i++)
        {
            var item = cardRemoveItems[i];
            if (item.frameBg == null) continue;
            item.frameBg.color = (i == index) ? cardSelectedColor : item.baseFrameColor;
        }

        // 삭제 버튼 활성화 + 라벨 갱신
        if (cardRemoveDeleteBtn != null) cardRemoveDeleteBtn.interactable = true;
        if (cardRemoveDeleteLabel != null)
            cardRemoveDeleteLabel.text = $"\"{GameManager.Instance.playerDeck[index].cardName}\" 삭제";
    }

    void ConfirmCardRemove()
    {
        if (cardRemoveSelectedIndex < 0) return;
        ApplyCardRemove(cardRemoveSelectedIndex);
    }

    void BuildModalEntry(Transform parent, string title, string desc, System.Action onClick)
    {
        var entryGO = new GameObject("Entry", typeof(RectTransform), typeof(Image), typeof(Button));
        entryGO.transform.SetParent(parent, false);
        var rt = entryGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800f, 70f);
        var img = entryGO.GetComponent<Image>();
        img.color = new Color(0.22f, 0.15f, 0.32f, 1f);
        var btn = entryGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(entryGO.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(0.4f, 1f);
        trt.offsetMin = new Vector2(16f, 0f);
        trt.offsetMax = new Vector2(0f, 0f);
        var ttmp = titleGO.AddComponent<TextMeshProUGUI>();
        ttmp.text = title;
        ttmp.alignment = TextAlignmentOptions.MidlineLeft;
        ttmp.fontSize = 22;
        ttmp.fontStyle = FontStyles.Bold;
        ttmp.color = Color.white;

        var descGO = new GameObject("Desc", typeof(RectTransform));
        descGO.transform.SetParent(entryGO.transform, false);
        var drt = descGO.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.4f, 0f);
        drt.anchorMax = new Vector2(1f, 1f);
        drt.offsetMin = new Vector2(8f, 4f);
        drt.offsetMax = new Vector2(-16f, -4f);
        var dtmp = descGO.AddComponent<TextMeshProUGUI>();
        dtmp.text = desc ?? "";
        dtmp.alignment = TextAlignmentOptions.MidlineLeft;
        dtmp.fontSize = 16;
        dtmp.color = new Color(0.85f, 0.82f, 0.92f);
        dtmp.enableWordWrapping = true;
    }

    void BuildModalCloseButton(Transform panel)
    {
        var btnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(panel, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta = new Vector2(280f, 60f);
        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.25f, 0.2f, 0.3f, 1f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(CloseModal);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "취소";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 22;
        tmp.color = Color.white;
    }

    void CloseModal()
    {
        if (modalRoot != null) Destroy(modalRoot);
        modalRoot = null;
    }

    // ────────────────────────────────────────────────────────────────
    // 액션 적용
    // ────────────────────────────────────────────────────────────────
    void ApplyEffectSwap(int threshold, GazeEffectData newEffect)
    {
        if (actionLocked) return;
        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.ReplaceEffect(threshold, newEffect);
        CloseModal();
        LockAll(chosenThreshold: threshold, removedCard: false);
    }

    void ApplyCardRemove(int deckIndex)
    {
        if (actionLocked) return;
        if (GameManager.Instance != null && deckIndex >= 0 && deckIndex < GameManager.Instance.playerDeck.Count)
        {
            var removed = GameManager.Instance.playerDeck[deckIndex];
            GameManager.Instance.playerDeck.RemoveAt(deckIndex);
            Debug.Log($"[Brand] 덱에서 제거: {(removed != null ? removed.cardName : "(null)")}");
        }
        CloseModal();
        LockAll(chosenThreshold: -1, removedCard: true);
    }

    void LockAll(int chosenThreshold, bool removedCard)
    {
        actionLocked = true;

        // 효과 카드 잠금
        foreach (var kv in effectCards)
        {
            kv.Value.button.interactable = false;
            kv.Value.image.color = cardLockedColor;
            // 갱신: 교체 후 새 효과명/설명 반영
            kv.Value.nameText.text = GetEffectName(kv.Key);
            kv.Value.descText.text = GetEffectDesc(kv.Key);
            if (kv.Key == chosenThreshold)
                kv.Value.lockX.SetActive(true);
        }

        if (cardRemoveButton != null)
        {
            cardRemoveButton.interactable = false;
            var img = cardRemoveButton.GetComponent<Image>();
            if (img != null) img.color = buttonDisabledColor;
        }
        if (cardRemoveX != null && removedCard) cardRemoveX.SetActive(true);
    }

    void ReturnToMap()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.returningFromBattle = true;
            GameManager.Instance.LoadNodeMap();
        }
        else
        {
            SceneManager.LoadScene("NodeMap");
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 헬퍼
    // ────────────────────────────────────────────────────────────────
    GazeEffectData[] GetPool(int threshold)
    {
        var gem = GazeEffectManager.Instance;
        if (gem == null) return null;
        switch (threshold)
        {
            case 20: return gem.pool20;
            case 40: return gem.pool40;
            case 60: return gem.pool60;
            case 80: return gem.pool80;
            case 100: return gem.pool100;
        }
        return null;
    }

    string GetEffectName(int threshold)
    {
        var gem = GazeEffectManager.Instance;
        if (gem == null) return "(GazeEffectManager 없음)";
        var e = gem.GetEffectAt(threshold);
        return e != null ? e.displayName : "(미배정)";
    }

    string GetEffectDesc(int threshold)
    {
        var gem = GazeEffectManager.Instance;
        if (gem == null) return "";
        var e = gem.GetEffectAt(threshold);
        return FormatEffectDesc(e);
    }

    string FormatEffectDesc(GazeEffectData e)
    {
        if (e == null) return "";
        string buff = string.IsNullOrEmpty(e.buffDescription) ? "" : "[버프] " + e.buffDescription;
        string debuff = string.IsNullOrEmpty(e.debuffDescription) ? "" : "[디버프] " + e.debuffDescription;
        if (buff.Length > 0 && debuff.Length > 0) return buff + "\n" + debuff;
        return buff + debuff;
    }

    UIRef NewUI(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        return new UIRef
        {
            gameObject = go,
            transform = go.transform,
            rectTransform = rt,
            image = go.GetComponent<Image>()
        };
    }

    void AddText(Transform parent, string text, Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax,
        int fontSize, FontStyles style, Color color, Vector2 sizeDelta)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
    }

    class UIRef
    {
        public GameObject gameObject;
        public Transform transform;
        public RectTransform rectTransform;
        public Image image;
    }

    class EffectCardUI
    {
        public GameObject root;
        public Image image;
        public Button button;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;
        public GameObject lockX;
    }

    class CardItemUI
    {
        public GameObject root;
        public Image frameBg;            // 외곽 프레임 (등급 색)
        public Color baseFrameColor;     // 등급 색 원본
    }
}
