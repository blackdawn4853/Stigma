using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// 스타트 노드 = 외신과의 조우 (출발의 의식).
//  중앙 일러스트 + 하단 다이얼로그(외신↔플레이어 교대, 화면 클릭으로 진행)
//  → 마지막 대사 후 하단 선택지 3개 등장 → 선택 시 닫힘.
//
// 전부 코드 절차생성(프로젝트 관례). 일러스트는 Sprite 없으면 placeholder(붉은/외신색 라디얼 글로우).
// 대사·선택지 데이터는 BuildEncounter(actIndex) 에 모아둠 — 장(Act)마다 다른 외신으로 갈아끼움.
//
// ※ 선택지의 "효과"는 아직 미구현(틀만). 클릭하면 onComplete 만 호출.
public class StartEncounterUI : MonoBehaviour
{
    const string PLAYER_NAME = "낙인자";

    // ── 데이터 구조 ──────────────────────────────────────────────
    public class Line { public bool isOuterGod; public string text; }

    public class EncounterDef
    {
        public string godName;
        public string godEpithet;     // 상호명/별칭 (이름 아래 작게)
        public Color godColor;        // 화자 이름/글로우 색
        public Sprite illustration;   // null → placeholder
        public List<Line> lines = new List<Line>();
        public string[] choices;      // 지금은 placeholder
    }

    enum State { Intro, Dialogue, Choices, Closing }

    // ── 런타임 상태 ──────────────────────────────────────────────
    EncounterDef def;
    System.Action onComplete;
    State state = State.Intro;
    int lineIndex = 0;
    bool typing = false;
    Coroutine typeCo;

    CanvasGroup group;
    TMP_FontAsset font;
    TMP_Text speakerText, bodyText, hintText, godTitle;
    GameObject dialogueBox;
    Image transitionBlack;          // 화면 전환 암전 오버레이
    RectTransform illustrationRT;    // 떠오름 연출용
    CanvasGroup bannerGroup, boxGroup;
    Image namePlateBg;        // 화자 명패 배경(색으로 화자 구분)
    RectTransform namePlateRT;
    readonly List<GameObject> choiceButtons = new List<GameObject>();
    Image glowImg;            // placeholder 글로우(호흡 애니용)
    RectTransform glowRT;
    float glowBaseScale = 1f;

    // 색 팔레트
    static readonly Color godBodyColor = new Color(0.86f, 0.82f, 0.88f, 1f);
    static readonly Color playerColor = new Color(0.80f, 0.74f, 0.70f, 1f);
    static readonly Color playerBodyColor = new Color(0.78f, 0.80f, 0.82f, 1f);

    // ── 진입점 ───────────────────────────────────────────────────
    public static StartEncounterUI Spawn(int actIndex, System.Action onComplete)
    {
        var go = new GameObject("StartEncounterUI");
        var ui = go.AddComponent<StartEncounterUI>();
        ui.Begin(actIndex, onComplete);
        return ui;
    }

    void Begin(int actIndex, System.Action done)
    {
        onComplete = done;
        def = BuildEncounter(actIndex);
        font = FindSceneFont();
        BuildUI();
        StartCoroutine(IntroSequence());
    }

    // ── 흐름 ─────────────────────────────────────────────────────
    IEnumerator IntroSequence()
    {
        state = State.Intro;
        group.alpha = 1f;

        // 초기: 화면 완전 암전 + 이름 띠/대사창 숨김 (노드맵은 검은 막 아래로 가려짐)
        if (transitionBlack != null) transitionBlack.color = Color.black;
        if (bannerGroup != null) bannerGroup.alpha = 0f;
        if (boxGroup != null) boxGroup.alpha = 0f;
        yield return null;

        // 1) 암전 → 일러스트가 살짝 줌아웃하며 떠오름 + 이름 띠는 살짝 늦게 등장
        float t = 0f, dur = 0.75f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - p) * (1f - p); // ease-out
            if (transitionBlack != null) transitionBlack.color = new Color(0f, 0f, 0f, 1f - e);
            if (illustrationRT != null) illustrationRT.localScale = Vector3.one * Mathf.Lerp(1.05f, 1f, e);
            if (bannerGroup != null) bannerGroup.alpha = Mathf.Clamp01((t - 0.45f) / 0.3f);
            yield return null;
        }
        if (transitionBlack != null)
        {
            transitionBlack.color = new Color(0f, 0f, 0f, 0f);
            transitionBlack.raycastTarget = false; // 닫을 때 재사용하므로 비활성화는 안 함
        }
        if (illustrationRT != null) illustrationRT.localScale = Vector3.one;
        if (bannerGroup != null) bannerGroup.alpha = 1f;

        // 2) 대사 시작 + 대사창 페이드인
        lineIndex = 0;
        state = State.Dialogue;
        ShowLine(def.lines[0]);
        float t2 = 0f, d2 = 0.35f;
        while (t2 < d2)
        {
            t2 += Time.unscaledDeltaTime;
            if (boxGroup != null) boxGroup.alpha = Mathf.Clamp01(t2 / d2);
            yield return null;
        }
        if (boxGroup != null) boxGroup.alpha = 1f;
    }

    void Update()
    {
        // placeholder 글로우 호흡
        if (glowRT != null)
        {
            float b = 1f + Mathf.Sin(Time.unscaledTime * 1.3f) * 0.04f;
            glowRT.localScale = Vector3.one * glowBaseScale * b;
            if (glowImg != null)
            {
                Color c = glowImg.color;
                c.a = 0.45f + Mathf.Sin(Time.unscaledTime * 1.3f) * 0.10f;
                glowImg.color = c;
            }
        }

        if (state == State.Dialogue && Input.GetMouseButtonDown(0))
        {
            if (typing) CompleteLine();
            else NextLine();
        }
    }

    void ShowLine(Line line)
    {
        bool god = line.isOuterGod;
        Color baseC = god ? def.godColor : playerColor;

        // 명패: 화자색을 어둡게 깐 배경 + 밝게 띄운 글자(가독)
        speakerText.text = god ? def.godName : PLAYER_NAME;
        namePlateBg.color = new Color(baseC.r * 0.45f, baseC.g * 0.45f, baseC.b * 0.45f, 0.97f);
        speakerText.color = new Color(Mathf.Lerp(baseC.r, 1f, 0.65f),
                                      Mathf.Lerp(baseC.g, 1f, 0.65f),
                                      Mathf.Lerp(baseC.b, 1f, 0.65f), 1f);
        // 좌우 위치로 화자 구분: 외신=좌, 플레이어=우
        namePlateRT.anchoredPosition = new Vector2(god ? -620f : 620f, 130f);

        bodyText.color = god ? godBodyColor : playerBodyColor;
        // 어절(공백 단위)을 <nobr>로 묶어 단어 중간에서 줄바꿈되는 문제 방지 ("크툴루"→"크툴"/"루")
        bodyText.richText = true;
        bodyText.text = WrapNoBreak(line.text);
        bodyText.maxVisibleCharacters = 0;
        if (typeCo != null) StopCoroutine(typeCo);
        typeCo = StartCoroutine(TypeLine());
    }

    // 리치텍스트 태그가 한 글자씩 노출되며 깨지지 않도록 maxVisibleCharacters 로 타자기 구현
    IEnumerator TypeLine()
    {
        typing = true;
        hintText.text = "";
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        for (int i = 0; i <= total; i++)
        {
            bodyText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(0.055f);
        }
        typing = false;
        hintText.text = "( 화면 클릭 )";
    }

    void CompleteLine()
    {
        if (typeCo != null) StopCoroutine(typeCo);
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
        typing = false;
        hintText.text = "( 화면 클릭 )";
    }

    // 어절 단위로 <nobr> 묶기 — 공백에서만 줄바꿈, 단어 내부는 안 끊김
    string WrapNoBreak(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Split(' ');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) parts[i] = "<nobr>" + parts[i] + "</nobr>";
        return string.Join(" ", parts);
    }

    // TMP 아웃라인(인스턴스 머티리얼 → 다른 텍스트에 영향 없음)
    void ApplyOutline(TMP_Text t, float width, Color outline)
    {
        if (t == null) return;
        var mat = t.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
    }

    void NextLine()
    {
        lineIndex++;
        if (lineIndex >= def.lines.Count) { EnterChoices(); return; }
        ShowLine(def.lines[lineIndex]);
    }

    void EnterChoices()
    {
        state = State.Choices;
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(RevealChoices());
    }

    IEnumerator RevealChoices()
    {
        // 버튼들을 아래에서 위로 슬라이드 + 페이드
        var rts = new List<RectTransform>();
        var grps = new List<CanvasGroup>();
        foreach (var go in choiceButtons)
        {
            go.SetActive(true);
            rts.Add(go.GetComponent<RectTransform>());
            grps.Add(go.GetComponent<CanvasGroup>());
        }

        float dur = 0.32f;
        for (int idx = 0; idx < choiceButtons.Count; idx++)
        {
            float t = 0f;
            Vector2 home = rts[idx].anchoredPosition;
            Vector2 from = home + new Vector2(0f, -40f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / dur);
                rts[idx].anchoredPosition = Vector2.Lerp(from, home, p);
                grps[idx].alpha = p;
                yield return null;
            }
            rts[idx].anchoredPosition = home;
            grps[idx].alpha = 1f;
            yield return new WaitForSecondsRealtime(0.06f);
        }
    }

    void OnChoice(int i)
    {
        if (state != State.Choices) return;
        // TODO: 선택지 효과 적용은 다음 단계. 지금은 닫기만.
        state = State.Closing;
        StartCoroutine(CloseSequence());
    }

    IEnumerator CloseSequence()
    {
        // 1) 검은 막을 올려 화면 전체를 균일하게 암전 (일러스트→어두운패널 2단계 비침 없이 한 번에)
        if (transitionBlack != null) transitionBlack.raycastTarget = true;
        yield return FadeBlack(0f, 1f, 0.35f);

        // 2) 완전 암전 상태에서 인카운터 내용 숨김 + 결과 확정
        group.alpha = 0f;
        onComplete?.Invoke();
        yield return new WaitForSecondsRealtime(0.06f);

        // 3) 검은 막을 내려 노드맵을 부드럽게 드러냄 (진입과 대칭)
        yield return FadeBlack(1f, 0f, 0.45f);

        if (transitionBlack != null) Destroy(transitionBlack.gameObject);
        Destroy(gameObject);
    }

    IEnumerator FadeBlack(float from, float to, float dur)
    {
        if (transitionBlack == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            transitionBlack.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        transitionBlack.color = new Color(0f, 0f, 0f, to);
    }

    // ── UI 절차생성 ──────────────────────────────────────────────
    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 드로잉 UI 캔버스(MapDrawingUICanvas, sortingOrder 1100)보다 위 →
        // 풀스크린 일러스트가 조우 중 드로잉 UI를 가리고 클릭도 차단. 조우 종료 시 자동 복귀.
        canvas.sortingOrder = 2000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();

        // 풀스크린 일러스트 (맵 전체를 덮음 + 맵 클릭 차단). 가장 뒤(배경).
        BuildIllustration();

        // 외신 이름 (상단, 일러스트 위) + 그 아래 상호명(별칭)
        // 이름 배너 (상단 중앙 가는 띠) — 어두운 박스로 가독성 확보, 후광/얼굴은 안 가림
        var banner = NewImage(transform, "NameBanner", new Color(0.05f, 0.04f, 0.06f, 0.9f));
        var brt = banner.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -20f);
        brt.sizeDelta = new Vector2(920f, 150f);
        banner.raycastTarget = false;
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        Border(brt, new Color(def.godColor.r * 0.78f, def.godColor.g * 0.6f, def.godColor.b * 0.34f, 0.85f));

        // 이름 (배너 상단)
        godTitle = NewText(brt, def.godName, 70, FontStyles.Bold,
                           new Color(def.godColor.r, def.godColor.g, def.godColor.b, 1f),
                           new Vector2(0f, 24f), new Vector2(880f, 84f));
        ApplyOutline(godTitle, 0.2f, Color.black); // 검정 테두리 + 금색 안쪽

        // 상호명 (배너 하단) — 명조 이탤릭 + 자간
        var epithet = NewText(brt, "— " + def.godEpithet + " —", 32, FontStyles.Italic,
                              new Color(Mathf.Lerp(def.godColor.r, 1f, 0.6f),
                                        Mathf.Lerp(def.godColor.g, 1f, 0.6f),
                                        Mathf.Lerp(def.godColor.b, 1f, 0.6f), 1f),
                              new Vector2(0f, -42f), new Vector2(880f, 46f));
        epithet.characterSpacing = 10f;
        ApplyOutline(epithet, 0.16f, Color.black);

        // 다이얼로그 박스 (하단)
        BuildDialogueBox();

        // 선택지 버튼 3개 (숨김 상태로 미리 생성)
        BuildChoices();

        // 화면 전환용 암전 — 인카운터 캔버스(group)와 독립된 별도 오버레이 캔버스(위)에서 제어.
        // group.alpha 와 무관하게 켜고 끌 수 있어, 닫을 때 '검정 덮기 → 맵 드러내기'가 깔끔하게 됨.
        var blackGO = new GameObject("StartEncounterBlack",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        var bcanvas = blackGO.GetComponent<Canvas>();
        bcanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bcanvas.sortingOrder = 2100; // 인카운터(2000)보다 위
        var bscaler = blackGO.GetComponent<CanvasScaler>();
        bscaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bscaler.referenceResolution = new Vector2(1920f, 1080f);
        transitionBlack = blackGO.GetComponent<Image>();
        transitionBlack.color = Color.black;
        transitionBlack.raycastTarget = true;
    }

    void BuildIllustration()
    {
        // 풀스크린 솔리드 패널(맵 차단) — 실제 일러스트/placeholder 글로우를 담는 배경.
        var panel = NewImage(transform, "Illustration", new Color(0.04f, 0.03f, 0.05f, 1f));
        var rt = panel.rectTransform;
        Stretch(rt);
        illustrationRT = rt;          // 떠오름(줌아웃) 연출 대상
        panel.raycastTarget = true;   // 뒤의 맵 클릭 차단

        if (def.illustration != null)
        {
            var img = NewImage(rt, "Art", Color.white);
            Stretch(img.rectTransform);
            img.sprite = def.illustration;
            img.preserveAspect = true;
        }
        else
        {
            // placeholder — 외신색 라디얼 글로우(화면을 크게 채움) + 안내 라벨
            glowImg = NewImage(rt, "Glow", new Color(def.godColor.r, def.godColor.g, def.godColor.b, 0.5f));
            glowRT = glowImg.rectTransform;
            glowRT.anchorMin = glowRT.anchorMax = glowRT.pivot = new Vector2(0.5f, 0.5f);
            glowRT.anchoredPosition = new Vector2(0f, 90f);
            glowRT.sizeDelta = new Vector2(1150f, 1150f);
            glowImg.sprite = MakeRadial(128);
            glowImg.raycastTarget = false;

            NewText(rt, "[ 일러스트 ]", 34, FontStyles.Normal,
                    new Color(0.5f, 0.47f, 0.53f, 0.65f), new Vector2(0f, 90f), new Vector2(700f, 60f));
        }

        // 하단 가독성용 어두운 띠(다이얼로그/선택지 글자 대비 확보)
        var shade = NewImage(transform, "BottomShade", new Color(0.02f, 0.01f, 0.03f, 0.62f));
        var srt = shade.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.sizeDelta = new Vector2(0f, 470f);
        srt.anchoredPosition = Vector2.zero;
        shade.raycastTarget = false;
    }

    void BuildDialogueBox()
    {
        var box = NewImage(transform, "DialogueBox", new Color(0.05f, 0.04f, 0.06f, 0.93f));
        dialogueBox = box.gameObject;
        boxGroup = dialogueBox.AddComponent<CanvasGroup>();
        var rt = box.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -350f);
        rt.sizeDelta = new Vector2(1660f, 300f);
        Border(rt, new Color(0.34f, 0.30f, 0.36f, 0.8f));

        // 본문 (박스 안쪽, 좌측 정렬, 자동 줄바꿈) — 시작점을 명패 아래로 내려 간격 확보
        bodyText = NewText(rt, "", 38, FontStyles.Normal, godBodyColor,
                           new Vector2(0f, -34f), new Vector2(1560f, 196f));
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;

        // 클릭 힌트 (우하단) — 박스 안쪽으로 (이전엔 우측으로 삐져나감)
        hintText = NewText(rt, "", 24, FontStyles.Italic,
                           new Color(0.62f, 0.59f, 0.64f, 0.85f), new Vector2(646f, -118f), new Vector2(300f, 36f));
        hintText.alignment = TextAlignmentOptions.Right;

        // 화자 명패 — 박스 자식이라 박스 숨길 때 같이 사라짐.
        // 색(어둡게 깐 화자색) + 좌우 위치(외신=좌/플레이어=우)로 누가 말하는지 한눈에 구분.
        var plate = new GameObject("NamePlate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(rt, false);
        namePlateRT = plate.GetComponent<RectTransform>();
        namePlateRT.anchorMin = namePlateRT.anchorMax = namePlateRT.pivot = new Vector2(0.5f, 0.5f);
        namePlateRT.anchoredPosition = new Vector2(-620f, 130f);
        namePlateRT.sizeDelta = new Vector2(380f, 70f);
        namePlateBg = plate.GetComponent<Image>();
        namePlateBg.raycastTarget = false;
        Border(namePlateRT, new Color(0f, 0f, 0f, 0.55f));

        speakerText = NewText(namePlateRT, "", 36, FontStyles.Bold, Color.white,
                              Vector2.zero, new Vector2(360f, 60f));
        speakerText.alignment = TextAlignmentOptions.Center;
    }

    void BuildChoices()
    {
        float[] ys = { -150f, -290f, -430f };   // 위로 올려 하단 짤림 방지(최하단 -485 > 화면 -540)
        for (int i = 0; i < def.choices.Length && i < 3; i++)
        {
            int captured = i;
            var go = new GameObject("Choice" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, ys[i]);
            rt.sizeDelta = new Vector2(1340f, 110f);

            // 선택지 톤을 외신색 기반으로 — 니알라토텝이면 금빛
            Color g = def.godColor;
            var img = go.GetComponent<Image>();
            img.color = new Color(g.r * 0.14f, g.g * 0.11f, g.b * 0.09f, 0.96f); // 외신색 깔린 near-black
            Border(rt, new Color(g.r * 0.78f, g.g * 0.62f, g.b * 0.42f, 0.9f));   // 외신색 테두리

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => OnChoice(captured));
            var hov = go.AddComponent<HoverScale>(); hov.hoverScale = 1.018f;

            var label = NewText(rt, def.choices[i], 32, FontStyles.Normal,
                                new Color(Mathf.Lerp(g.r, 1f, 0.72f), Mathf.Lerp(g.g, 1f, 0.68f), Mathf.Lerp(g.b, 1f, 0.62f), 1f),
                                Vector2.zero, new Vector2(1280f, 100f));
            label.alignment = TextAlignmentOptions.Center;

            go.GetComponent<CanvasGroup>().alpha = 0f;
            go.SetActive(false);
            choiceButtons.Add(go);
        }
    }

    // ── 외신 데이터 (장별) ───────────────────────────────────────
    // 플레이어는 잠든 크툴루를 찾아 심연으로 내려가고, 외신들이 대가를 걸고 돕는다.
    // 플레이어 대사는 대부분 단답.
    EncounterDef BuildEncounter(int actIndex)
    {
        int idx = Mathf.Clamp(actIndex, 0, 2);
        switch (idx)
        {
            case 0:  return Nyarlathotep();
            case 1:  return ShubNiggurath();
            default: return YogSothoth();
        }
    }

    // 1장 — 니알라토텝 (기어다니는 혼돈): 외신들의 전령. 조롱하는 안내자.
    EncounterDef Nyarlathotep()
    {
        var d = new EncounterDef
        {
            godName = "니알라토텝",
            godEpithet = "기어다니는 혼돈",
            godColor = new Color(0.86f, 0.62f, 0.26f, 1f), // 일러스트의 금빛 후광 톤
            illustration = Resources.Load<Sprite>("Encounters/Nyarlathotep"),
            choices = new[] { "선택지 1", "선택지 2", "선택지 3" }
        };
        d.lines = new List<Line>
        {
            G("또 하나의 낙인자가 심연으로 기어드는구나. 너희는 늘 같은 얼굴로 와서 같은 이름을 부른다 — 크툴루."),
            P("...길을 알려줘."),
            G("길? 길은 없다. 오직 떨어지는 방향만 있을 뿐. 허나 나는 친절하지. 너를 더 깊이 떨어뜨려 줄 수 있다."),
            P("대가는."),
            G("대가는 늘 네 안에 있었다. 자, 무엇을 내게 바치겠나?"),
        };
        return d;
    }

    // 2장 — 슈브니구라스 (천 마리 새끼를 거느린 숲속의 검은 산양): 원초적 다산·부패의 어미.
    EncounterDef ShubNiggurath()
    {
        var d = new EncounterDef
        {
            godName = "슈브니구라스",
            godEpithet = "숲속의 검은 산양",
            godColor = new Color(0.52f, 0.70f, 0.26f, 1f),
            choices = new[] { "선택지 1", "선택지 2", "선택지 3" }
        };
        d.lines = new List<Line>
        {
            G("이리 오너라, 작은 낙인자. 내 천의 자식들이 네 살냄새를 맡았다."),
            P("...물러서."),
            G("두려워 마라. 어미는 제 품에 든 것을 먹지 않는다 — 아직은. 잠든 크툴루를 찾는다지? 그자는 네게 아무것도 주지 않아. 나는 다르다."),
            P("뭘 줄 수 있는데."),
            G("생명. 부패. 그리고 그 사이의 모든 것. 골라보아라, 내 새끼야."),
        };
        return d;
    }

    // 3장 — 요그소토스 (문이자 열쇠): 모든 시공의 관문. 크툴루에 닿기 직전.
    EncounterDef YogSothoth()
    {
        var d = new EncounterDef
        {
            godName = "요그소토스",
            godEpithet = "문이자 열쇠",
            godColor = new Color(0.50f, 0.80f, 0.84f, 1f),
            choices = new[] { "선택지 1", "선택지 2", "선택지 3" }
        };
        d.lines = new List<Line>
        {
            G("나는 문이며, 문을 여는 열쇠다. 너의 과거와 미래가 지금 내 안에서 한꺼번에 타오르고 있다, 낙인자여."),
            P("크툴루는 어디 있지."),
            G("어디? 그는 '언제'에 잠들어 있다. 가라앉은 르뤼에는 별이 옳게 설 때 떠오른다. 너는 그 때를 앞당기려는 게로구나."),
            P("도와줄 건가."),
            G("모든 문은 대가를 받고 열린다. 어느 문을 지나겠느냐."),
        };
        return d;
    }

    static Line G(string t) => new Line { isOuterGod = true, text = t };
    static Line P(string t) => new Line { isOuterGod = false, text = t };

    // ── UI 빌더 헬퍼 (OptionsMenu 패턴 차용) ─────────────────────
    TMP_FontAsset FindSceneFont()
    {
#if UNITY_2023_1_OR_NEWER
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
#else
        var texts = Object.FindObjectsOfType<TMP_Text>();
#endif
        foreach (var t in texts) if (t.font != null) return t.font;
        return null;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI NewText(Transform parent, string content, int size, FontStyles style,
                            Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
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
        tmp.raycastTarget = false;
        return tmp;
    }

    void Border(RectTransform target, Color color)
    {
        Edge(target, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f));
        Edge(target, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f));
    }

    void Edge(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 size)
    {
        var img = NewImage(parent, "Edge", color);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    // 절차생성 라디얼 글로우 스프라이트 (placeholder 일러스트용)
    Sprite MakeRadial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
