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

        // 배경 (어두운 한기) — 즉시 솔리드(페이드 대상 아님), 맨 뒤로
        var bg = MakeImage(canvas.transform, "BG", new Color(0.03f, 0.03f, 0.045f, 1f));
        StretchFull(bg.rectTransform);
        bg.transform.SetAsFirstSibling();

        // 타이틀 "GAME OVER" — 페이드 대신 피-reveal + 떨림 연출로 등장(GameOverTitleFX)
        BuildTitleFX(canvas.transform);

        // 글귀 (랜덤) — 타자기 효과로 한 글자씩 새겨짐 + 끝에 깜빡이는 커서
        string epitaph = Epitaphs[Random.Range(0, Epitaphs.Length)];
        var epi = MakeText(root, epitaph, 36, FontStyles.Italic,
            new Color(0.62f, 0.60f, 0.57f), new Vector2(0f, 95f), new Vector2(1500f, 70f));
        epi.maxVisibleCharacters = 0;   // 전부 숨김 → 타자기로 하나씩 공개

        // 커서 (작은 사각형 — 글리프 두부 방지로 메시 대신 Image). 평소 숨김, 타이핑 시작 시 등장.
        var cursorImg = MakeImage(epi.transform, "Cursor", new Color(0.72f, 0.70f, 0.66f, 0.85f));
        var curRt = cursorImg.rectTransform;
        curRt.anchorMin = curRt.anchorMax = curRt.pivot = new Vector2(0.5f, 0.5f);
        curRt.sizeDelta = new Vector2(5f, 34f);
        curRt.anchoredPosition = Vector2.zero;
        cursorImg.raycastTarget = false;
        cursorImg.enabled = false;

        StartCoroutine(TypeEpitaph(epi, cursorImg));

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

        // 분위기 오버레이 (비네트 + 가장자리 붉은 펄스 + 옅은 필름 그레인) — 최상단, 클릭 비차단
        BuildAtmosphere();
    }

    // 타이틀 + 뒤편 붉은 글로우 생성 후 연출 컴포넌트(GameOverTitleFX) 부착.
    // 타이틀 레이어는 Root(CanvasGroup 페이드) 밖에 둬서 이중 페이드 없이 자체 reveal 로만 등장.
    void BuildTitleFX(Transform canvasParent)
    {
        // 타이틀 뒤 붉은 라디얼 글로우 (호흡)
        var glow = MakeImage(canvasParent, "TitleGlow", new Color(0.62f, 0.06f, 0.07f, 0f));
        var grt = glow.rectTransform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0f, 230f);
        grt.sizeDelta = new Vector2(1320f, 540f);
        glow.sprite = RadialSprite();
        glow.raycastTarget = false;

        // 타이틀 "GAME OVER"
        var title = MakeText(canvasParent, "GAME OVER", 130, FontStyles.Bold,
            new Color(0.78f, 0.10f, 0.12f), new Vector2(0f, 230f), new Vector2(1400f, 200f));
        AddOutline(title, new Color(0f, 0f, 0f, 0.85f), 0.3f);
        title.raycastTarget = false;

        var fx = title.gameObject.AddComponent<GameOverTitleFX>();
        fx.Init(title, glow);
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

    // ---------------------------------------------------------------- C. 글귀 타자기

    // 타이틀 reveal(약 1.6s) 끝난 뒤 글귀를 한 글자씩 공개. 커서는 타이핑 시작 시 등장해 끝에서 깜빡임.
    IEnumerator TypeEpitaph(TextMeshProUGUI tmp, Image cursor)
    {
        if (tmp == null) yield break;

        // 총 글자 수 확보 (maxVisibleCharacters 와 무관하게 layout 은 전체 계산됨)
        tmp.maxVisibleCharacters = 99999;
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();

        yield return new WaitForSecondsRealtime(1.7f);   // 타이틀 연출 끝난 뒤

        if (cursor != null) cursor.enabled = true;
        StartCoroutine(BlinkCursor(cursor));

        for (int i = 1; i <= total; i++)
        {
            tmp.maxVisibleCharacters = i;
            tmp.ForceMeshUpdate();
            PlaceCursor(tmp, cursor, i);
            yield return new WaitForSecondsRealtime(0.06f);
        }
        tmp.maxVisibleCharacters = total;
        tmp.ForceMeshUpdate();
        PlaceCursor(tmp, cursor, total);
    }

    // 커서를 마지막으로 보이는 글자 오른쪽에 배치 (가운데 정렬이라 매 글자 위치가 바뀜)
    void PlaceCursor(TextMeshProUGUI tmp, Image cursor, int visibleCount)
    {
        if (cursor == null) return;
        var ti = tmp.textInfo;
        int idx = -1;
        int upper = Mathf.Min(visibleCount, ti.characterCount);
        for (int c = upper - 1; c >= 0; c--)
            if (ti.characterInfo[c].isVisible) { idx = c; break; }

        var rt = cursor.rectTransform;
        if (idx < 0) { rt.anchoredPosition = Vector2.zero; return; }   // 아직 보이는 글자 없음

        var ci = ti.characterInfo[idx];
        float x = ci.topRight.x + 5f;
        float y = (ci.ascender + ci.descender) * 0.5f;
        rt.anchoredPosition = new Vector2(x, y);
    }

    IEnumerator BlinkCursor(Image cursor)
    {
        float t = 0f;
        while (cursor != null)
        {
            t += Time.unscaledDeltaTime;
            var c = cursor.color;
            c.a = 0.85f * (0.5f + 0.5f * Mathf.Sin(t * 6.5f));
            cursor.color = c;
            yield return null;
        }
    }

    // ---------------------------------------------------------------- D. 분위기 오버레이

    void BuildAtmosphere()
    {
        var holderGO = new GameObject("Atmosphere", typeof(RectTransform), typeof(CanvasGroup));
        holderGO.transform.SetParent(canvas.transform, false);
        StretchFull(holderGO.GetComponent<RectTransform>());
        holderGO.transform.SetAsLastSibling();   // 화면 최상단 (단 클릭은 통과)
        var hcg = holderGO.GetComponent<CanvasGroup>();
        hcg.blocksRaycasts = false; hcg.interactable = false; hcg.alpha = 0f;
        Transform h = holderGO.transform;

        // 비네트 (가장자리 어둡게)
        var vig = MakeImage(h, "Vignette", new Color(0f, 0f, 0f, 0.55f));
        StretchFull(vig.rectTransform);
        vig.sprite = VignetteSprite();
        vig.raycastTarget = false;

        // 가장자리 붉은 펄스 (은은한 호흡)
        var pulse = MakeImage(h, "RedPulse", new Color(0.5f, 0.04f, 0.05f, 0.12f));
        StretchFull(pulse.rectTransform);
        pulse.sprite = VignetteSprite();
        pulse.raycastTarget = false;
        StartCoroutine(RedPulse(pulse));

        // 필름 그레인 (옅게, 깜빡) — RawImage uvRect 를 매 프레임 랜덤 오프셋해 지지직
        var grainGO = new GameObject("Grain", typeof(RectTransform), typeof(RawImage));
        grainGO.transform.SetParent(h, false);
        StretchFull(grainGO.GetComponent<RectTransform>());
        var raw = grainGO.GetComponent<RawImage>();
        raw.texture = NoiseTexture();
        raw.color = new Color(1f, 1f, 1f, 0.022f);   // 그레인 옅게 (0.05 → 0.022)
        raw.uvRect = new Rect(0f, 0f, 8f, 8f);   // 8배 타일
        raw.raycastTarget = false;
        StartCoroutine(GrainFlicker(raw));

        StartCoroutine(FadeCanvasGroup(hcg, 1f, 1.2f));
    }

    IEnumerator RedPulse(Image img)
    {
        float t = 0f;
        while (img != null)
        {
            t += Time.unscaledDeltaTime;
            var c = img.color;
            c.a = 0.07f + 0.10f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f));
            img.color = c;
            yield return null;
        }
    }

    IEnumerator GrainFlicker(RawImage raw)
    {
        float acc = 0f;
        while (raw != null)
        {
            acc += Time.unscaledDeltaTime;
            if (acc >= 0.045f)   // ~22fps 깜빡 → 필름 느낌
            {
                acc = 0f;
                raw.uvRect = new Rect(Random.value, Random.value, 8f, 8f);
            }
            yield return null;
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float to, float dur)
    {
        float from = cg.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        cg.alpha = to;
    }

    // 가장자리로 갈수록 진해지는 비네트 스프라이트 (흰색·알파만, 색은 Image.color)
    Sprite vignetteCache;
    Sprite VignetteSprite()
    {
        if (vignetteCache != null) return vignetteCache;
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float maxR = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        vignetteCache = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return vignetteCache;
    }

    // 필름 그레인용 노이즈 텍스처 (회색 랜덤, Repeat)
    Texture2D noiseCache;
    Texture2D NoiseTexture()
    {
        if (noiseCache != null) return noiseCache;
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[s * s];
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)(Random.value * 255f);
            px[i] = new Color32(v, v, v, v);
        }
        tex.SetPixels32(px); tex.Apply();
        noiseCache = tex;
        return noiseCache;
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

    // 부드러운 라디얼 글로우 스프라이트(흰색, 알파만 중앙→가장자리 감쇠). 색은 Image.color 로 입힘.
    Sprite radialCache;
    Sprite RadialSprite()
    {
        if (radialCache != null) return radialCache;
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float maxR = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // 부드러운 감쇠
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        radialCache = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return radialCache;
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

// "GAME OVER" 타이틀 연출:
//  ① 피-reveal — 글자가 위→아래로 붉게 스며들 듯 나타남(정점 알파 그라데이션), 진행선은 더 밝은 붉은 기.
//  ② 떨림 — reveal 후 글자마다(per-vertex) 미세하게 따로 흔들림(불안한 톤).
//  ③ 글로우 호흡 — 타이틀 뒤 붉은 라디얼 글로우가 천천히 밝아졌다 어두워짐.
// TMP 정점을 직접 만져 추가 에셋 없이 구현. Time.unscaled* 사용(게임오버는 timeScale 무관 보장).
public class GameOverTitleFX : MonoBehaviour
{
    TextMeshProUGUI title;
    Image glow;
    Vector3[][] baseVerts;
    Color32[][] baseCols;
    float yMin, yMax;
    float t;
    bool ready;

    const float RevealDur = 1.6f;   // 피-reveal 시간
    const float Band = 0.18f;       // reveal 경계 부드러움(정규화 높이 기준)
    const float Jitter = 1.7f;      // 떨림 픽셀 진폭
    static readonly Color BaseColor = new Color(0.78f, 0.10f, 0.12f);
    static readonly Color BrightColor = new Color(1f, 0.32f, 0.22f); // 진행선 번지는 붉은 기

    public void Init(TextMeshProUGUI titleText, Image glowImage)
    {
        title = titleText;
        glow = glowImage;
    }

    void Start()
    {
        title.ForceMeshUpdate();
        var ti = title.textInfo;
        baseVerts = new Vector3[ti.meshInfo.Length][];
        baseCols = new Color32[ti.meshInfo.Length][];
        for (int i = 0; i < ti.meshInfo.Length; i++)
        {
            baseVerts[i] = (Vector3[])ti.meshInfo[i].vertices.Clone();
            baseCols[i] = (Color32[])ti.meshInfo[i].colors32.Clone();
        }

        // 글자 정점 Y 범위(정규화 기준)
        yMin = float.MaxValue; yMax = float.MinValue;
        for (int c = 0; c < ti.characterCount; c++)
        {
            if (!ti.characterInfo[c].isVisible) continue;
            int mi = ti.characterInfo[c].materialReferenceIndex;
            int vi = ti.characterInfo[c].vertexIndex;
            for (int k = 0; k < 4; k++)
            {
                float y = baseVerts[mi][vi + k].y;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
        }
        if (yMax <= yMin) yMax = yMin + 1f;

        ready = true;
        ApplyFrame(0f); // 시작 시 완전히 숨김
    }

    void Update()
    {
        if (!ready) return;
        t += Time.unscaledDeltaTime;
        ApplyFrame(t);
    }

    void ApplyFrame(float time)
    {
        var ti = title.textInfo;
        float p = Mathf.Clamp01(time / RevealDur);
        float front = Mathf.Lerp(1f + Band, -Band, p);                 // 진행선 위→아래
        float jitterAmt = Jitter * Mathf.Clamp01((p - 0.7f) / 0.3f);   // reveal 후반부터 떨림 램프인
        float breath = 0.5f + 0.5f * Mathf.Sin(time * 2.0f);
        float invSpan = 1f / (yMax - yMin);

        for (int c = 0; c < ti.characterCount; c++)
        {
            if (!ti.characterInfo[c].isVisible) continue;
            int mi = ti.characterInfo[c].materialReferenceIndex;
            int vi = ti.characterInfo[c].vertexIndex;

            // 글자마다 다른 떨림(Perlin 으로 부드럽게)
            float jx = (Mathf.PerlinNoise(c * 0.37f, time * 9f) - 0.5f) * 2f * jitterAmt;
            float jy = (Mathf.PerlinNoise(c * 0.37f + 5.2f, time * 9f) - 0.5f) * 2f * jitterAmt;

            var verts = ti.meshInfo[mi].vertices;
            var cols = ti.meshInfo[mi].colors32;
            for (int k = 0; k < 4; k++)
            {
                Vector3 bv = baseVerts[mi][vi + k];
                verts[vi + k] = bv + new Vector3(jx, jy, 0f);

                float yNorm = (bv.y - yMin) * invSpan;
                float a = Mathf.Clamp01((yNorm - (front - Band)) / (2f * Band));
                float boost = (p >= 1f) ? 0f : 1f - Mathf.Clamp01(Mathf.Abs(yNorm - front) / Band);

                Color col = Color.Lerp(BaseColor, BrightColor, boost * 0.85f);
                col.a = (baseCols[mi][vi + k].a / 255f) * a;
                cols[vi + k] = col;
            }
        }

        title.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

        if (glow != null)
        {
            var gc = glow.color;
            gc.a = (0.10f + 0.14f * breath) * p;
            glow.color = gc;
            float sc = 1f + 0.04f * breath;
            glow.rectTransform.localScale = new Vector3(sc, sc, 1f);
        }
    }
}
