using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

// 게임오버 화면 — 플레이어 사망 시 BattleManager.CheckPlayerDeath → GameManager.GameOver() 가 이 씬을 로드.
// UI 는 전부 코드로 절차 생성한다(프로젝트의 HUDManager/BrandNodeManager 관례).
// 덱 확인 팝업은 낙인 노드의 카드 그리드(CardPrefab + CardUI.Setup)를 재활용.
// '타이틀로' 버튼: 세이브 삭제 + 런 초기화(데이터 소멸) 후 MainMenu 로 페이드 이동.
public class GameOverScene : MonoBehaviour
{
    [Header("카드 표시 (Assets/Prefabs/CardPrefab.prefab)")]
    public GameObject cardPrefab;

    [Header("폰트 (비우면 TMP 기본)")]
    public TMP_FontAsset font;

    [Header("이동 대상")]
    public string titleSceneName = "MainMenu";

    // 사망 글귀 — 매번 랜덤 1개. (추후 자유롭게 추가/교체)
    static readonly string[] Epitaphs = new string[]
    {
        "신은 더 이상 당신을 봐줄 수 없었습니다.",
        "당신의 신을 향한 갈망에, 그가 대답했습니다.",
        "낙인은 끝내 주인을 집어삼켰다.",
        "응시를 마주한 자, 응시 속으로 사라지다.",
        "눈을 감아도, 그것은 여전히 당신을 보고 있었다.",
    };

    Canvas canvas;
    CanvasGroup rootFade;
    GameObject deckPopup;
    bool transitioning = false;

    void Start()
    {
        EnsureEventSystem();
        BuildUI();
        StartCoroutine(FadeIn());
    }

    // ---------------------------------------------------------------- 빌드

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Root (페이드 인 대상)
        var rootGO = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvas.transform, false);
        StretchFull(rootGO.GetComponent<RectTransform>());
        rootFade = rootGO.GetComponent<CanvasGroup>();
        rootFade.alpha = 0f;
        Transform root = rootGO.transform;

        // 배경 (어두운 한기)
        var bg = MakeImage(root, "BG", new Color(0.03f, 0.03f, 0.045f, 1f));
        StretchFull(bg.rectTransform);

        // 타이틀 "GAME OVER"
        var title = MakeText(root, "GAME OVER", 130, FontStyles.Bold,
            new Color(0.78f, 0.10f, 0.12f), new Vector2(0f, 230f), new Vector2(1400f, 200f));
        AddOutline(title, new Color(0f, 0f, 0f, 0.85f), 0.3f);

        // 글귀 (랜덤)
        string epitaph = Epitaphs[Random.Range(0, Epitaphs.Length)];
        MakeText(root, epitaph, 36, FontStyles.Italic,
            new Color(0.62f, 0.60f, 0.57f), new Vector2(0f, 95f), new Vector2(1500f, 70f));

        // 구분선
        MakeDivider(root, new Vector2(0f, 25f), 1000f);

        // 스탯 행
        int bosses = GameManager.Instance != null ? GameManager.Instance.bossesDefeated : 0;
        int gold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        int deckCount = GameManager.Instance != null ? GameManager.Instance.playerDeck.Count : 0;

        BuildStat(root, new Vector2(-360f, -70f), "처치한 보스", bosses.ToString(), null);
        BuildStat(root, new Vector2(0f, -70f), "골드", gold.ToString(), null);
        BuildStat(root, new Vector2(360f, -70f), "덱", deckCount + "장", OpenDeckPopup);

        // 구분선
        MakeDivider(root, new Vector2(0f, -200f), 1000f);

        // 타이틀로 버튼
        BuildTitleButton(root, new Vector2(0f, -310f));
    }

    void BuildStat(Transform parent, Vector2 pos, string caption, string value, System.Action onClick)
    {
        // 캡션 (작게, 위)
        MakeText(parent, caption, 26, FontStyles.Normal,
            new Color(0.55f, 0.53f, 0.50f), pos + new Vector2(0f, 34f), new Vector2(320f, 40f));

        if (onClick == null)
        {
            // 값 (크게)
            MakeText(parent, value, 50, FontStyles.Bold,
                new Color(0.86f, 0.84f, 0.80f), pos + new Vector2(0f, -16f), new Vector2(320f, 70f));
        }
        else
        {
            // 클릭 가능한 값 (덱 — 팝업 열기)
            var btnGO = new GameObject("DeckValueBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos + new Vector2(0f, -16f);
            rt.sizeDelta = new Vector2(320f, 70f);
            var img = btnGO.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // 투명 히트박스
            var btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var label = MakeText(btnGO.transform, value, 50, FontStyles.Bold,
                new Color(0.95f, 0.82f, 0.45f), Vector2.zero, new Vector2(320f, 70f));
            // 호버 시 밝아지게
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            btn.colors = colors;
            label.raycastTarget = false;

            // 클릭 affordance — 오른쪽 삼각형 (폰트 글리프 대신 메시로 그림)
            MakeTriangle(parent, pos + new Vector2(86f, -16f), new Vector2(22f, 28f),
                new Color(0.95f, 0.82f, 0.45f));
        }
    }

    void BuildTitleButton(Transform parent, Vector2 pos)
    {
        var btnGO = new GameObject("TitleButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300f, 76f);
        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.12f, 0.11f, 0.14f, 0.95f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(OnTitleClicked);

        // 테두리 느낌 (얇은 외곽)
        AddBorder(btnGO.GetComponent<RectTransform>(), new Color(0.45f, 0.42f, 0.40f, 0.8f));

        var label = MakeText(btnGO.transform, "타이틀로", 34, FontStyles.Bold,
            new Color(0.86f, 0.84f, 0.80f), Vector2.zero, new Vector2(300f, 76f));
        label.raycastTarget = false;
    }

    // ---------------------------------------------------------------- 덱 팝업

    void OpenDeckPopup()
    {
        if (deckPopup != null) { deckPopup.SetActive(true); return; }

        // 전체 화면 어두운 오버레이 (바깥 클릭 = 닫기)
        deckPopup = new GameObject("DeckPopup", typeof(RectTransform), typeof(Image), typeof(Button));
        deckPopup.transform.SetParent(canvas.transform, false);
        StretchFull(deckPopup.GetComponent<RectTransform>());
        var dim = deckPopup.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        var dimBtn = deckPopup.GetComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(CloseDeckPopup);

        // 패널 (가운데)
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
        panel.transform.SetParent(deckPopup.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(1380f, 860f);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.05f, 0.09f, 0.98f);
        // 패널 안쪽 클릭은 닫힘 전파 차단
        panel.GetComponent<Button>().onClick.AddListener(() => { });

        int deckCount = GameManager.Instance != null ? GameManager.Instance.playerDeck.Count : 0;
        var header = MakeText(panel.transform, $"현재 덱  ({deckCount}장)", 40, FontStyles.Bold,
            new Color(0.88f, 0.86f, 0.82f), new Vector2(0f, -50f), new Vector2(1200f, 60f));
        header.alignment = TextAlignmentOptions.Center;

        BuildDeckScroll(panel.transform);

        // 닫기 버튼
        var closeGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(panel.transform, false);
        var crt = closeGO.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-20f, -20f);
        crt.sizeDelta = new Vector2(52f, 52f);
        closeGO.GetComponent<Image>().color = new Color(0.5f, 0.12f, 0.12f, 0.9f);
        var closeBtn = closeGO.GetComponent<Button>();
        closeBtn.onClick.AddListener(CloseDeckPopup);
        var x = MakeText(closeGO.transform, "X", 32, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(52f, 52f));
        x.raycastTarget = false;
    }

    void CloseDeckPopup()
    {
        if (deckPopup != null) Destroy(deckPopup);
        deckPopup = null;
    }

    void BuildDeckScroll(Transform parent)
    {
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGO.transform.SetParent(parent, false);
        var srt = scrollGO.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -100f);
        srt.sizeDelta = new Vector2(1300f, 700f);
        scrollGO.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.06f, 0.8f);

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vrt = viewportGO.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = new Vector2(8f, 8f); vrt.offsetMax = new Vector2(-8f, -8f);
        viewportGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewportGO.GetComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var crt = contentGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;

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

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        if (GameManager.Instance != null && GameManager.Instance.playerDeck.Count > 0)
        {
            foreach (var card in GameManager.Instance.playerDeck)
            {
                if (card == null) continue;
                BuildDeckCardItem(contentGO.transform, card);
            }
        }
        else
        {
            MakeText(contentGO.transform, "덱에 카드가 없습니다.", 24, FontStyles.Italic,
                new Color(0.85f, 0.7f, 0.7f), Vector2.zero, new Vector2(700f, 60f));
        }
    }

    // 낙인 노드 BuildCardItem 과 동일한 표시(보기 전용, 클릭/드래그 차단)
    void BuildDeckCardItem(Transform parent, CardData card)
    {
        var itemGO = new GameObject("CardItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        itemGO.transform.SetParent(parent, false);
        itemGO.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 240f);
        itemGO.GetComponent<Image>().color = card.GetRarityColor();
        var le = itemGO.GetComponent<LayoutElement>();
        le.preferredWidth = 180f; le.preferredHeight = 240f;

        if (cardPrefab != null)
        {
            var cardGO = Instantiate(cardPrefab, itemGO.transform);
            var cr = (RectTransform)cardGO.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(180f, 240f);
            cr.localScale = Vector3.one * 0.92f;
            var cui = cardGO.GetComponent<CardUI>();
            if (cui != null) { cui.Setup(card); cui.enabled = false; }
            var cg = cardGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardGO.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
        }
        else
        {
            // 폴백 — 일러스트 + 이름
            var art = MakeImage(itemGO.transform, "Art", Color.gray);
            var artRt = art.rectTransform;
            artRt.anchorMin = Vector2.zero; artRt.anchorMax = Vector2.one;
            artRt.offsetMin = new Vector2(4f, 4f); artRt.offsetMax = new Vector2(-4f, -4f);
            if (card.cardImage != null) { art.sprite = card.cardImage; art.color = Color.white; }
            MakeText(itemGO.transform, card.cardName, 18, FontStyles.Bold, Color.white,
                new Vector2(0f, -100f), new Vector2(170f, 50f));
        }
    }

    // ---------------------------------------------------------------- 동작

    void OnTitleClicked()
    {
        if (transitioning) return;
        transitioning = true;

        // 데이터 소멸 — 죽었으므로 세이브 삭제 + 런 초기화
        if (GameManager.Instance != null)
            GameManager.Instance.ResetForTitle();

        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene(titleSceneName);
        else
            SceneManager.LoadScene(titleSceneName);
    }

    IEnumerator FadeIn()
    {
        float t = 0f, dur = 1.3f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            if (rootFade != null) rootFade.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        if (rootFade != null) rootFade.alpha = 1f;
    }

    // ---------------------------------------------------------------- 헬퍼

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }

    void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI MakeText(Transform parent, string content, int size, FontStyles style,
        Color color, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    void AddOutline(TextMeshProUGUI tmp, Color color, float width)
    {
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
        tmp.outlineColor = color;
        tmp.outlineWidth = width;
    }

    // 오른쪽을 가리키는 삼각형 (▶) — 메시로 직접 그려 폰트 글리프 의존 제거
    void MakeTriangle(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject("Triangle", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tri = go.AddComponent<UITriangle>();
        tri.color = color;
        tri.raycastTarget = false;
    }

    void MakeDivider(Transform parent, Vector2 pos, float width)
    {
        var img = MakeImage(parent, "Divider", new Color(0.4f, 0.38f, 0.42f, 0.45f));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 2f);
    }

    // 버튼 외곽 얇은 테두리 (4변)
    void AddBorder(RectTransform target, Color color)
    {
        float th = 2f;
        AddEdge(target, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, th));   // top
        AddEdge(target, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, th));   // bottom
        AddEdge(target, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(th, 0f));   // left
        AddEdge(target, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(th, 0f));   // right
    }

    void AddEdge(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 size)
    {
        var img = MakeImage(parent, "Edge", color);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }
}

// 오른쪽을 가리키는 채워진 삼각형 UI. 스프라이트 없이 메시로 그려 어떤 크기에서도 선명.
public class UITriangle : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        var v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(r.xMin, r.yMax, 0f); vh.AddVert(v);                    // 0 좌상
        v.position = new Vector3(r.xMin, r.yMin, 0f); vh.AddVert(v);                    // 1 좌하
        v.position = new Vector3(r.xMax, (r.yMin + r.yMax) * 0.5f, 0f); vh.AddVert(v);  // 2 우중

        vh.AddTriangle(0, 1, 2);
    }
}
