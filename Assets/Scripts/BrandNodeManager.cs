using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
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
    [Tooltip("덱 카드 항목에 사용할 실제 카드 프리팹 (Assets/Prefabs/CardPrefab.prefab). 연결되면 인게임 카드와 동일하게(일러스트+코스트박스+아이콘+이름+설명) 표시.")]
    public GameObject cardPrefab;
    [Tooltip("(폴백) cardPrefab 미연결 시 사용할 통일 스프라이트.")]
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

        // 배경: 거의 검정 + 은은한 비네트 (잡티 없이 깔끔하게)
        BuildAtmosphere();
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

                var ui = StyleEffectCard(card.gameObject, threshold);
                ui.button.onClick.RemoveAllListeners();
                ui.button.onClick.AddListener(() => OnEffectCardClicked(captured));
                effectCards[threshold] = ui;
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

        // 씬 프리빌트 UI는 글자/크기가 작아 가독성이 떨어짐 → 런타임에 약 1.35배로 확대 + 행 중앙 재정렬.
        ApplyLargerLayout();

        return true;
    }

    // 프리빌트 씬 UI(제목/부제/효과카드/버튼)를 가독성 위해 일괄 확대하고
    // 효과 카드 행을 정확히 화면 중앙으로 재정렬한다. (모달은 절차생성 코드에서 별도 확대)
    void ApplyLargerLayout()
    {
        // 제목 — 크게 + 그을린 밝은 톤
        SetFontOf(root.Find("Title"), 96f);
        var titleT = root.Find("Title");
        if (titleT != null)
        {
            var ttmp = titleT.GetComponent<TextMeshProUGUI>();
            if (ttmp != null)
            {
                ttmp.color = new Color(0.94f, 0.87f, 0.96f, 1f);
                ttmp.characterSpacing = 14f;
            }
        }
        // 제목 아래 룬 라인
        if (titleT != null) BuildTitleRule(titleT);

        // 부제
        var sub = root.Find("Subtitle");
        SetFontOf(sub, 30f);
        if (sub != null)
        {
            var srt = sub.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(1500f, 60f);
            srt.anchoredPosition = new Vector2(0f, -210f);
            var stmp = sub.GetComponent<TextMeshProUGUI>();
            if (stmp != null) stmp.color = new Color(0.76f, 0.71f, 0.84f, 1f);
        }

        // 효과 카드 행: 중앙 재정렬 + 위치/간격 (카드 내부 디자인은 StyleEffectCard 담당)
        const float cardW = 300f, spacing = 24f;
        var row = root.Find("EffectsRow");
        if (row != null)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = spacing;
                hlg.padding = new RectOffset(0, 0, 0, 0);
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false; hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            }
            int n = effectCards.Count;
            float totalW = n > 0 ? n * cardW + (n - 1) * spacing : 0f;
            var rrt = row.GetComponent<RectTransform>();
            rrt.anchoredPosition = new Vector2(0f, -300f);
            rrt.sizeDelta = new Vector2(totalW, 384f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
        }

        // 하단 버튼 2개 확대 + 재배치 + 다크 리스타일
        ResizeButton(root.Find("CardRemoveButton"), new Vector2(520f, 108f), new Vector2(0f, -724f), 36f);
        ResizeButton(root.Find("ReturnButton"),     new Vector2(520f, 108f), new Vector2(0f, -860f), 36f);
        StyleBottomButton(root.Find("CardRemoveButton"), new Color(0.17f, 0.09f, 0.11f, 1f), new Color(0.74f, 0.27f, 0.27f, 1f));
        StyleBottomButton(root.Find("ReturnButton"),     new Color(0.12f, 0.12f, 0.16f, 1f), new Color(0.44f, 0.48f, 0.58f, 1f));
    }

    // 하단 버튼 다크 리스타일: 어두운 패널 + 좌측 얇은 강조바 + 밝은 라벨 (과하지 않게)
    void StyleBottomButton(Transform btn, Color bg, Color accent)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = bg;

        if (btn.Find("AccentBar") == null)
        {
            var bar = MakeRect(btn, Vector2.zero, Vector2.zero, accent);
            bar.name = "AccentBar";
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(6f, -20f); brt.anchoredPosition = new Vector2(10f, 0f);
            bar.raycastTarget = false;
        }

        var label = btn.Find("Label");
        if (label != null)
        {
            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.color = colTextLight; tmp.characterSpacing = 3f; }
        }
    }

    // 제목 아래 가느다란 룬 장식선 (한 번만)
    void BuildTitleRule(Transform titleT)
    {
        if (titleT.Find("TitleRule") != null) return;
        var line = MakeRect(titleT, new Vector2(0f, -64f), new Vector2(360f, 3f), new Color(0.6f, 0.16f, 0.16f, 0.8f));
        line.name = "TitleRule";
        var diamond = MakeRect(titleT, new Vector2(0f, -64f), new Vector2(12f, 12f), new Color(0.78f, 0.24f, 0.2f, 1f));
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void SetFontOf(Transform t, float size)
    {
        if (t == null) return;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.fontSize = size;
    }

    void ResizeButton(Transform btn, Vector2 size, Vector2 pos, float labelFont)
    {
        if (btn == null) return;
        var rt = btn.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        if (btn.GetComponent<HoverScale>() == null) btn.gameObject.AddComponent<HoverScale>();
        SetFontOf(btn.Find("Label"), labelFont);
        var lx = btn.Find("LockX");
        if (lx != null)
        {
            var t = lx.GetComponent<TextMeshProUGUI>();
            if (t != null) t.fontSize = 80f;
        }
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
        var ui = StyleEffectCard(card, threshold);
        ui.button.onClick.RemoveAllListeners();
        ui.button.onClick.AddListener(() => OnEffectCardClicked(threshold));
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
        panel.rectTransform.sizeDelta = new Vector2(1120f, 820f);
        panel.image.color = modalBgColor;

        AddText(panel.transform, $"{threshold} 구간 효과 교체", new Vector2(0f, -24f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 42, FontStyles.Bold,
            new Color(1f, 0.85f, 0.95f), new Vector2(1000f, 64f));

        var listGO = new GameObject("List", typeof(RectTransform));
        listGO.transform.SetParent(panel.transform, false);
        var lrt = listGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 1f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -116f);
        lrt.sizeDelta = new Vector2(1020f, 0f);
        var vlg = listGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
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
                BuildEffectSwapEntry(listGO.transform, effect,
                    () => { ApplyEffectSwap(threshold, capturedEffect); });
            }
        }
        if (!anyOption)
        {
            AddText(listGO.transform, "교체 가능한 다른 효과가 없습니다.", Vector2.zero,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 28, FontStyles.Italic,
                new Color(0.85f, 0.7f, 0.7f), new Vector2(900f, 70f));
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
        AddText(panel.transform, "제거할 카드 선택", new Vector2(0f, -26f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 44, FontStyles.Bold,
            new Color(1f, 0.85f, 0.95f), new Vector2(1340f, 72f));

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
        srt.anchoredPosition = new Vector2(0f, -100f);
        srt.sizeDelta = new Vector2(1340f, 656f);
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
        grid.cellSize = new Vector2(224f, 300f);
        grid.spacing = new Vector2(30f, 34f);
        grid.padding = new RectOffset(28, 28, 28, 28);
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
        rt.sizeDelta = new Vector2(224f, 300f);
        var frameBg = itemGO.GetComponent<Image>();
        Color baseFrameColor = card.GetRarityColor();
        frameBg.color = baseFrameColor;
        var btn = itemGO.GetComponent<Button>();
        btn.targetGraphic = frameBg;
        btn.onClick.AddListener(() => onClick?.Invoke());
        var le = itemGO.GetComponent<LayoutElement>();
        le.preferredWidth = 224f;
        le.preferredHeight = 300f;

        // 실제 게임 카드 프리팹으로 표시 — 일러스트 + 코스트박스 + 아이콘 + 이름 + 설명 전부 인게임과 동일.
        if (cardPrefab != null)
        {
            var cardGO = Instantiate(cardPrefab, itemGO.transform);
            var cardRt = (RectTransform)cardGO.transform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            // 배틀씬과 동일한 내부 배치(아이콘/마나박스 위치)를 위해 프리팹 네이티브 크기를 유지하고
            // 셀(224x300)에 맞게 '균일 스케일'만 적용한다. (sizeDelta를 바꾸면 앵커드 자식들이 어긋남)
            Vector2 native = cardRt.sizeDelta;
            if (native.x < 1f || native.y < 1f) native = new Vector2(200f, 280f); // 안전장치
            float fit = Mathf.Min(224f / native.x, 300f / native.y) * 0.92f; // 프레임이 테두리로 보이게 살짝 축소
            cardRt.localScale = Vector3.one * fit;
            var cui = cardGO.GetComponent<CardUI>();
            if (cui != null) { cui.Setup(card); cui.enabled = false; } // Setup 후 비활성(드래그/Update 차단)
            var cg = cardGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardGO.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false; // 클릭은 프레임 버튼이 받음
            return new CardItemUI { root = itemGO, frameBg = frameBg, baseFrameColor = baseFrameColor };
        }

        // (폴백) cardPrefab 미연결 시 수동 빌드
        // 카드 스프라이트 (프레임 안쪽 4px 여백) — 카드 전체 비주얼
        var artGO = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(itemGO.transform, false);
        var art = artGO.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(4f, 4f);
        art.offsetMax = new Vector2(-4f, -4f);
        var artImg = artGO.GetComponent<Image>();
        // 카드별 일러스트(CardData.cardImage)를 사용. (이전엔 공용 cardSprite 필드를 참조해 전부 빈칸이었음)
        if (card.cardImage != null)
        {
            artImg.sprite = card.cardImage;
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
        frt.anchoredPosition = new Vector2(0f, 34f);
        frt.sizeDelta = new Vector2(1000f, 96f);

        var hlg = footerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 48f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // 삭제 버튼
        var delGO = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        delGO.transform.SetParent(footerGO.transform, false);
        var drt = delGO.GetComponent<RectTransform>();
        drt.sizeDelta = new Vector2(440f, 84f);
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
        dlbl.fontSize = 30;
        dlbl.fontStyle = FontStyles.Bold;
        dlbl.color = Color.white;
        cardRemoveDeleteLabel = dlbl;

        // 취소 버튼
        var cancelGO = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelGO.transform.SetParent(footerGO.transform, false);
        var crt = cancelGO.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(320f, 84f);
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
        clbl.fontSize = 28;
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
        rt.sizeDelta = new Vector2(1020f, 96f);
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
        ttmp.fontSize = 30;
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
        dtmp.fontSize = 22;
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
        rt.anchoredPosition = new Vector2(0f, 28f);
        rt.sizeDelta = new Vector2(340f, 78f);
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
        tmp.fontSize = 30;
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
            // 갱신: 교체 후 새 효과명/버프/디버프 반영
            FillEffectCard(kv.Value, kv.Key);
            var hov = kv.Value.root != null ? kv.Value.root.GetComponent<HoverScale>() : null;
            if (hov != null) hov.Disable();
        }

        if (cardRemoveButton != null)
        {
            cardRemoveButton.interactable = false;
            var img = cardRemoveButton.GetComponent<Image>();
            if (img != null) img.color = buttonDisabledColor;
            var hov = cardRemoveButton.GetComponent<HoverScale>();
            if (hov != null) hov.Disable();
        }
        if (cardRemoveX != null) cardRemoveX.SetActive(false);

        // 선택한 항목 위에 낙인 각인(A) → 사슬 봉인(C) 연출
        RectTransform target = null;
        var charTexts = new List<TextMeshProUGUI>();
        if (chosenThreshold >= 0 && effectCards.TryGetValue(chosenThreshold, out var chosen) && chosen.root != null)
        {
            target = chosen.root.GetComponent<RectTransform>();
            if (chosen.nameText != null) charTexts.Add(chosen.nameText);
            if (chosen.buffText != null) charTexts.Add(chosen.buffText);
            if (chosen.debuffText != null) charTexts.Add(chosen.debuffText);
        }
        else if (removedCard && cardRemoveButton != null)
        {
            target = cardRemoveButton.GetComponent<RectTransform>();
            var label = cardRemoveButton.transform.Find("Label");
            if (label != null)
            {
                var lblTmp = label.GetComponent<TextMeshProUGUI>();
                if (lblTmp != null) charTexts.Add(lblTmp);
            }
        }

        if (target != null) StartCoroutine(PlayBrandSequence(target, charTexts));
    }

    // ────────────────────────────────────────────────────────────────
    // 낙인 연출 (A: 각인 / C: 사슬 봉인) — 전부 코드 생성, 스프라이트 미사용
    // ────────────────────────────────────────────────────────────────
    static readonly Color charColor = new Color(0.5f, 0.13f, 0.11f, 1f); // 그을린 검붉은색
    static readonly Color ironColor = new Color(0.34f, 0.34f, 0.38f, 1f); // 사슬 쇠색
    static readonly Color chainHoleColor = new Color(0.05f, 0.04f, 0.06f, 0.92f); // 링 구멍

    IEnumerator PlayBrandSequence(RectTransform target, List<TextMeshProUGUI> charTexts)
    {
        if (target == null) yield break;

        // 텍스트 시작 색 저장
        int n = charTexts.Count;
        var startCols = new Color[n];
        for (int i = 0; i < n; i++) startCols[i] = charTexts[i].color;

        Vector3 baseScale = target.localScale;
        const float dur = 0.4f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            // 펀치 스케일: 초반 35% 구간에서 1 → 1.08 → 1
            float punchT = Mathf.Clamp01(p / 0.35f);
            float punch = 1f + 0.08f * Mathf.Sin(punchT * Mathf.PI);
            target.localScale = baseScale * punch;

            // 미세 진동 (인두 떨림) — 회전으로만 (레이아웃 그룹과 충돌 없음)
            target.localRotation = (t < 0.18f)
                ? Quaternion.Euler(0f, 0f, Random.Range(-1.6f, 1.6f))
                : Quaternion.identity;

            // 텍스트: 흰색 → 그을린 검붉은색 (주황 번쩍 없이 차분하게 타들어감)
            for (int i = 0; i < n; i++)
                charTexts[i].color = Color.Lerp(startCols[i], charColor, p);

            yield return null;
        }

        target.localScale = baseScale;
        target.localRotation = Quaternion.identity;
        for (int i = 0; i < n; i++) charTexts[i].color = charColor;

        // 사슬 봉인: 아래에서부터 X자로 잠긴다
        var refs = BuildChainSeal(target);
        refs.strandB.Reverse(); // 두 가닥 모두 아래→위 순서로 등장하도록 정렬
        yield return AnimateChainsLockIn(refs);
    }

    class BrandSealRefs
    {
        public CanvasGroup dim;
        public List<CanvasGroup> strandA = new List<CanvasGroup>();
        public List<CanvasGroup> strandB = new List<CanvasGroup>();
        public CanvasGroup center;
    }

    BrandSealRefs BuildChainSeal(RectTransform target)
    {
        var refs = new BrandSealRefs();

        var seal = new GameObject("BrandSeal", typeof(RectTransform));
        seal.transform.SetParent(target, false);
        var rt = seal.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        seal.transform.SetAsLastSibling();

        // 어둡게 가라앉힘 (사슬보다 먼저 옅게 깔림)
        var dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dimGO.transform.SetParent(seal.transform, false);
        var drt = dimGO.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        var dimg = dimGO.GetComponent<Image>();
        dimg.color = new Color(0.02f, 0.01f, 0.02f, 0.55f);
        dimg.raycastTarget = false;
        refs.dim = dimGO.GetComponent<CanvasGroup>();
        refs.dim.alpha = 0f;

        Vector2 size = target.rect.size;
        // 대상 모양에 맞춰 사슬 규모/각도 조정 (넓고 낮은 버튼이면 작고 납작한 X)
        bool wide = size.x > size.y * 1.8f;
        float linkScale = wide ? Mathf.Clamp(size.y / 200f, 0.45f, 1f) : 1f;
        float angle = wide ? Mathf.Clamp(Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg, 10f, 30f) : 32f;
        float lengthMul = wide ? 0.96f : 0.92f;
        refs.strandA = BuildChainStrand(seal.transform, size, angle, linkScale, lengthMul);
        refs.strandB = BuildChainStrand(seal.transform, size, -angle, linkScale, lengthMul);

        // 중앙 인장 (사슬이 교차하며 마지막에 잠김)
        var centerGO = new GameObject("Center", typeof(RectTransform), typeof(CanvasGroup));
        centerGO.transform.SetParent(seal.transform, false);
        var crt = centerGO.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = Vector2.zero;
        refs.center = centerGO.GetComponent<CanvasGroup>();
        refs.center.alpha = 0f;
        centerGO.transform.localScale = Vector3.one * 0.5f;
        // 사슬 쇠색 8각 별 (사각 두 개를 45° 엇갈려 겹침) — 장식적인 인장 베이스 (대상 규모에 맞춰 축소)
        MakeRect(centerGO.transform, Vector2.zero, new Vector2(38f, 38f) * linkScale, ironColor);
        var star2 = MakeRect(centerGO.transform, Vector2.zero, new Vector2(38f, 38f) * linkScale, ironColor);
        star2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        // 어두운 인셋
        var inset = MakeRect(centerGO.transform, Vector2.zero, new Vector2(24f, 24f) * linkScale, new Color(0.10f, 0.09f, 0.12f, 1f));
        inset.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        // 낙인 룬: 은은한 검붉은 헤일로 + 작은 보석점 (진한 단색 네모 X)
        var halo = MakeRect(centerGO.transform, Vector2.zero, new Vector2(20f, 20f) * linkScale, new Color(0.6f, 0.2f, 0.16f, 0.32f));
        halo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var gem = MakeRect(centerGO.transform, Vector2.zero, new Vector2(9f, 9f) * linkScale, new Color(0.72f, 0.28f, 0.22f, 1f));
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        return refs;
    }

    // 사슬 한 가닥: 가로/세로 링을 번갈아 겹쳐 배치 후 통째로 회전.
    // 각 링은 CanvasGroup 컨테이너로 만들어 하나씩 등장시킬 수 있게 한다.
    List<CanvasGroup> BuildChainStrand(Transform parent, Vector2 area, float angleDeg, float linkScale, float lengthMul)
    {
        var units = new List<CanvasGroup>();

        var strand = new GameObject("Strand", typeof(RectTransform));
        strand.transform.SetParent(parent, false);
        var srt = strand.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = Vector2.zero;
        srt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        float length = Mathf.Sqrt(area.x * area.x + area.y * area.y) * lengthMul;
        float step = 20f * linkScale;
        int count = Mathf.Max(3, Mathf.CeilToInt(length / step));
        float start = -((count - 1) * step) / 2f;

        for (int i = 0; i < count; i++)
        {
            bool horiz = (i % 2 == 0);
            float lx = start + i * step;

            var link = new GameObject("Link", typeof(RectTransform), typeof(CanvasGroup));
            link.transform.SetParent(strand.transform, false);
            var lrt = link.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = new Vector2(lx, 0f);
            lrt.sizeDelta = Vector2.zero;
            var cg = link.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            link.transform.localScale = Vector3.one * 0.5f;

            Vector2 linkSize = (horiz ? new Vector2(34f, 15f) : new Vector2(15f, 34f)) * linkScale;
            Vector2 holeSize = (horiz ? new Vector2(20f, 6f) : new Vector2(6f, 20f)) * linkScale;
            MakeRect(link.transform, Vector2.zero, linkSize, ironColor);      // 링 외곽
            MakeRect(link.transform, Vector2.zero, holeSize, chainHoleColor); // 링 구멍

            units.Add(cg);
        }
        return units;
    }

    Image MakeRect(Transform parent, Vector2 pos, Vector2 size, Color col)
    {
        var go = new GameObject("Rect", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    IEnumerator AnimateChainsLockIn(BrandSealRefs refs)
    {
        if (refs == null) yield break;

        // 바닥 어둠 빠르게 깔기
        if (refs.dim != null) StartCoroutine(FadeCanvasGroup(refs.dim, 0f, 1f, 0.15f));

        // 두 가닥을 동시에, 옆에서부터 한 링씩 톡톡 올라오게
        int m = Mathf.Max(refs.strandA.Count, refs.strandB.Count);
        const float stagger = 0.03f;
        for (int i = 0; i < m; i++)
        {
            if (i < refs.strandA.Count) StartCoroutine(PopLink(refs.strandA[i]));
            if (i < refs.strandB.Count) StartCoroutine(PopLink(refs.strandB[i]));
            yield return new WaitForSeconds(stagger);
        }

        // 마지막에 중앙 인장이 찰칵 잠김
        yield return new WaitForSeconds(0.05f);
        if (refs.center != null) yield return PopLink(refs.center);
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        cg.alpha = to;
    }

    // 링 하나가 톡 올라오며 잠기는 팝 (살짝 오버슛)
    IEnumerator PopLink(CanvasGroup cg)
    {
        if (cg == null) yield break;
        var tr = cg.transform;
        const float dur = 0.12f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            cg.alpha = p;
            // 0.5 → 1.1 → 1.0 오버슛
            float s = (p < 0.6f)
                ? Mathf.Lerp(0.5f, 1.1f, p / 0.6f)
                : Mathf.Lerp(1.1f, 1f, (p - 0.6f) / 0.4f);
            tr.localScale = Vector3.one * s;
            yield return null;
        }
        cg.alpha = 1f;
        tr.localScale = Vector3.one;
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

    // ════════════════════════════════════════════════════════════════
    // UI 업그레이드 (다크·음산 톤) — 전부 코드 절차생성, 외부 스프라이트 미사용
    // ════════════════════════════════════════════════════════════════
    static Sprite _spVignette;

    static readonly Color colCardBg     = new Color(0.11f, 0.085f, 0.14f, 0.99f);
    static readonly Color colCardEdge   = new Color(0.03f, 0.025f, 0.05f, 1f);
    static readonly Color colTextLight  = new Color(0.93f, 0.89f, 0.97f, 1f);
    static readonly Color colBuffBg     = new Color(0.06f, 0.16f, 0.16f, 0.96f);
    static readonly Color colBuffTag    = new Color(0.42f, 0.93f, 0.83f, 1f);
    static readonly Color colDebuffBg   = new Color(0.20f, 0.055f, 0.085f, 0.96f);
    static readonly Color colDebuffTag  = new Color(1f, 0.48f, 0.48f, 1f);

    // 시선 단계별 위험색 (20 옅음 → 100 핏빛)
    Color ThresholdColor(int threshold)
    {
        float p = Mathf.InverseLerp(20f, 100f, threshold);
        return Color.Lerp(new Color(0.45f, 0.38f, 0.55f, 1f), new Color(0.80f, 0.13f, 0.13f, 1f), p);
    }

    // 효과 카드 한 장을 다크 패널 + 버프/디버프 블록 구조로 재구성
    EffectCardUI StyleEffectCard(GameObject cardGO, int threshold)
    {
        var rt = cardGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 384f);

        var frame = cardGO.GetComponent<Image>();
        if (frame == null) frame = cardGO.AddComponent<Image>();
        frame.color = colCardEdge;
        var btn = cardGO.GetComponent<Button>();
        if (btn == null) btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = frame;
        btn.transition = Selectable.Transition.None;
        if (cardGO.GetComponent<HoverScale>() == null) cardGO.AddComponent<HoverScale>();

        // 기존 자식 정리 후 새로 구성
        for (int i = cardGO.transform.childCount - 1; i >= 0; i--)
            Destroy(cardGO.transform.GetChild(i).gameObject);

        Color accent = ThresholdColor(threshold);

        // 안쪽 패널 (테두리 3px 보이게)
        var inner = MakeRect(cardGO.transform, Vector2.zero, Vector2.zero, colCardBg);
        var irt = inner.rectTransform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3f, 3f); irt.offsetMax = new Vector2(-3f, -3f);
        inner.raycastTarget = false;

        // 콘텐츠 세로 스택
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(inner.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(10f, 12f); crt.offsetMax = new Vector2(-10f, -10f);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 9f;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

        // 헤더 밴드: "시선 N" + 하단 강조선
        var header = MakeBlock(content.transform, 46f, new Color(accent.r * 0.32f, accent.g * 0.30f, accent.b * 0.34f, 0.6f));
        var headerTxt = AddBlockText(header.transform, $"시선 {threshold}", 30f, FontStyles.Bold,
            new Color(Mathf.Min(1f, accent.r + 0.32f), Mathf.Min(1f, accent.g + 0.28f), Mathf.Min(1f, accent.b + 0.32f), 1f),
            TextAlignmentOptions.Center);
        var hLine = MakeRect(header.transform, Vector2.zero, Vector2.zero, accent);
        var hlrt = hLine.rectTransform;
        hlrt.anchorMin = new Vector2(0f, 0f); hlrt.anchorMax = new Vector2(1f, 0f); hlrt.pivot = new Vector2(0.5f, 0f);
        hlrt.offsetMin = new Vector2(8f, 0f); hlrt.offsetMax = new Vector2(-8f, 3f); hlrt.anchoredPosition = Vector2.zero;
        hLine.raycastTarget = false;

        // 이름
        var nameBlock = MakeBlock(content.transform, 52f, new Color(0f, 0f, 0f, 0f));
        var nameTxt = AddBlockText(nameBlock.transform, "", 34f, FontStyles.Bold, colTextLight, TextAlignmentOptions.Center);

        // 버프 / 디버프 패널
        TextMeshProUGUI buffTxt, debuffTxt;
        BuildStatPanel(content.transform, "버프", colBuffBg, colBuffTag, out buffTxt);
        BuildStatPanel(content.transform, "디버프", colDebuffBg, colDebuffTag, out debuffTxt);

        var ui = new EffectCardUI
        {
            root = cardGO, image = frame, button = btn,
            headerText = headerTxt, nameText = nameTxt, buffText = buffTxt, debuffText = debuffTxt
        };
        FillEffectCard(ui, threshold);
        return ui;
    }

    void FillEffectCard(EffectCardUI ui, int threshold)
    {
        var gem = GazeEffectManager.Instance;
        var e = gem != null ? gem.GetEffectAt(threshold) : null;
        if (ui.nameText != null) ui.nameText.text = e != null ? e.displayName : "(미배정)";
        if (ui.buffText != null) ui.buffText.text = (e != null && !string.IsNullOrEmpty(e.buffDescription)) ? e.buffDescription : "—";
        if (ui.debuffText != null) ui.debuffText.text = (e != null && !string.IsNullOrEmpty(e.debuffDescription)) ? e.debuffDescription : "—";
    }

    // 세로 스택용 고정/가변 높이 블록
    Image MakeBlock(Transform parent, float height, Color bg)
    {
        var go = new GameObject("Block", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = bg; img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>(); le.preferredHeight = height;
        return img;
    }

    TextMeshProUGUI AddBlockText(Transform parent, string text, float size, FontStyles style, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(6f, 2f); rt.offsetMax = new Vector2(-6f, -2f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = col;
        tmp.alignment = align; tmp.enableWordWrapping = true; tmp.raycastTarget = false;
        return tmp;
    }

    // 버프/디버프 패널: 좌측 accent 바 + 태그 + 내용
    void BuildStatPanel(Transform parent, string tag, Color bg, Color tagCol, out TextMeshProUGUI contentText)
    {
        var panel = new GameObject($"Panel_{tag}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        panel.transform.SetParent(parent, false);
        var img = panel.GetComponent<Image>(); img.color = bg; img.raycastTarget = false;
        var le = panel.GetComponent<LayoutElement>(); le.flexibleHeight = 1f; le.minHeight = 92f;

        var bar = MakeRect(panel.transform, Vector2.zero, Vector2.zero, tagCol);
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(5f, 0f); brt.anchoredPosition = Vector2.zero;
        bar.raycastTarget = false;

        var tagGO = new GameObject("Tag", typeof(RectTransform));
        tagGO.transform.SetParent(panel.transform, false);
        var trt = tagGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(14f, -7f); trt.sizeDelta = new Vector2(-20f, 24f);
        var ttmp = tagGO.AddComponent<TextMeshProUGUI>();
        ttmp.text = tag; ttmp.fontSize = 20f; ttmp.fontStyle = FontStyles.Bold; ttmp.color = tagCol;
        ttmp.alignment = TextAlignmentOptions.Left; ttmp.raycastTarget = false;

        var cGO = new GameObject("Content", typeof(RectTransform));
        cGO.transform.SetParent(panel.transform, false);
        var crt = cGO.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(14f, 8f); crt.offsetMax = new Vector2(-10f, -32f);
        var ctmp = cGO.AddComponent<TextMeshProUGUI>();
        ctmp.text = ""; ctmp.fontSize = 21f; ctmp.color = colTextLight;
        ctmp.alignment = TextAlignmentOptions.TopLeft; ctmp.enableWordWrapping = true; ctmp.raycastTarget = false;
        contentText = ctmp;
    }

    // 효과 교체 모달 항목: 이름 + 버프/디버프 라인
    void BuildEffectSwapEntry(Transform parent, GazeEffectData effect, System.Action onClick)
    {
        var entry = new GameObject("Entry", typeof(RectTransform), typeof(Image), typeof(Button));
        entry.transform.SetParent(parent, false);
        entry.GetComponent<RectTransform>().sizeDelta = new Vector2(1020f, 144f);
        var img = entry.GetComponent<Image>(); img.color = new Color(0.15f, 0.11f, 0.21f, 1f);
        var btn = entry.GetComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());
        if (entry.GetComponent<HoverScale>() == null) { var h = entry.AddComponent<HoverScale>(); h.hoverScale = 1.015f; }

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(entry.transform, false);
        var nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0f, 1f);
        nrt.anchoredPosition = new Vector2(20f, -8f); nrt.sizeDelta = new Vector2(-40f, 44f);
        var ntmp = nameGO.AddComponent<TextMeshProUGUI>();
        ntmp.text = effect != null ? effect.displayName : "";
        ntmp.fontSize = 30f; ntmp.fontStyle = FontStyles.Bold; ntmp.color = colTextLight;
        ntmp.alignment = TextAlignmentOptions.Left; ntmp.raycastTarget = false;

        string buff = (effect != null && !string.IsNullOrEmpty(effect.buffDescription)) ? effect.buffDescription : "—";
        string deb = (effect != null && !string.IsNullOrEmpty(effect.debuffDescription)) ? effect.debuffDescription : "—";
        BuildStatLine(entry.transform, "버프", colBuffBg, colBuffTag, buff, new Vector2(16f, -58f), new Vector2(988f, 38f));
        BuildStatLine(entry.transform, "디버프", colDebuffBg, colDebuffTag, deb, new Vector2(16f, -100f), new Vector2(988f, 38f));
    }

    void BuildStatLine(Transform parent, string tag, Color bg, Color tagCol, string text, Vector2 pos, Vector2 size)
    {
        var row = new GameObject($"Line_{tag}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = row.GetComponent<Image>(); img.color = bg; img.raycastTarget = false;

        var bar = MakeRect(row.transform, Vector2.zero, Vector2.zero, tagCol);
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(4f, 0f); brt.anchoredPosition = Vector2.zero; bar.raycastTarget = false;

        var tagGO = new GameObject("Tag", typeof(RectTransform));
        tagGO.transform.SetParent(row.transform, false);
        var trt = tagGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(14f, 0f); trt.sizeDelta = new Vector2(86f, 0f);
        var ttmp = tagGO.AddComponent<TextMeshProUGUI>();
        ttmp.text = tag; ttmp.fontSize = 20f; ttmp.fontStyle = FontStyles.Bold; ttmp.color = tagCol;
        ttmp.alignment = TextAlignmentOptions.Left; ttmp.raycastTarget = false;

        var cGO = new GameObject("Content", typeof(RectTransform));
        cGO.transform.SetParent(row.transform, false);
        var crt = cGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0f, 0.5f);
        crt.offsetMin = new Vector2(108f, 0f); crt.offsetMax = new Vector2(-12f, 0f);
        var ctmp = cGO.AddComponent<TextMeshProUGUI>();
        ctmp.text = text; ctmp.fontSize = 21f; ctmp.color = colTextLight;
        ctmp.alignment = TextAlignmentOptions.Left; ctmp.enableWordWrapping = false; ctmp.raycastTarget = false;
        ctmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ─── 배경: 거의 검정 + 은은한 비네트 ─────────────────────────────
    void BuildAtmosphere()
    {
        var bg = root.Find("Background");
        int vigIndex = 0;
        if (bg != null)
        {
            var bi = bg.GetComponent<Image>();
            if (bi != null) bi.color = new Color(0.035f, 0.025f, 0.05f, 1f); // 거의 검정 (살짝 보랏빛)
            bg.SetSiblingIndex(0);
            vigIndex = 1;
        }

        // 가장자리만 서서히 어두워지는 비네트 (카드 뒤)
        var vig = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        vig.transform.SetParent(root, false);
        StretchFull(vig.GetComponent<RectTransform>());
        vig.transform.SetSiblingIndex(vigIndex);
        var vimg = vig.GetComponent<Image>();
        vimg.sprite = GetVignetteSprite();
        vimg.color = new Color(0f, 0f, 0f, 0.75f);
        vimg.raycastTarget = false;
    }

    void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ─── 절차생성 비네트 스프라이트 ──────────────────────────────
    Sprite GetVignetteSprite() { if (_spVignette == null) _spVignette = MakeVignetteSprite(128); return _spVignette; }

    Sprite MakeVignetteSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                float a = Mathf.Clamp01((d - 0.6f) / 0.4f);
                a = a * a;
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
        public Image image;            // 카드 외곽(테두리) 프레임
        public Button button;
        public TextMeshProUGUI headerText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI buffText;
        public TextMeshProUGUI debuffText;
    }

    class CardItemUI
    {
        public GameObject root;
        public Image frameBg;            // 외곽 프레임 (등급 색)
        public Color baseFrameColor;     // 등급 색 원본
    }
}

// 마우스 오버 시 살짝 떠오르는(확대) 효과. 잠금 시 Disable() 호출.
public class HoverScale : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    public float hoverScale = 1.04f;
    public float speed = 12f;
    Vector3 baseScale = Vector3.one; // 원래 스케일 기준 — 노드맵처럼 1이 아닌 경우도 대응
    Vector3 target = Vector3.one;
    bool captured;

    void Awake() => Capture();
    void Capture() { if (!captured) { baseScale = transform.localScale; target = baseScale; captured = true; } }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
    {
        if (enabled) target = baseScale * hoverScale;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
    {
        target = baseScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * speed);
    }

    public void Disable()
    {
        target = baseScale;
        transform.localScale = baseScale;
        enabled = false; // Update 정지 → 각인 연출의 펀치 스케일과 충돌 없음
    }
}
