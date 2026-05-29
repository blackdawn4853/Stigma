using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 인트로 → 노드맵 전환 컷씬.
/// 한 장짜리 일러스트(CutScene_1)를 시네마틱하게 훑는다.
/// - 카메라 무빙은 모두 ease-in-out + 정지 샷에도 느린 드리프트(켄 번스)로 항상 살아있음.
/// - 레터박스 / 비네트 / 필름 그레인 / 붉은 시선 펄스 / 번개·피날레 플래시 전부 코드 생성(씬 수동 배선 불필요).
/// - 포커스 지점은 "이미지 정규화 좌표(중심=0, 범위 -0.5~0.5)"로 지정 → 런타임에 rect 크기로 환산하므로 해상도 독립적.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    [Header("UI 연결")]
    public RectTransform cutsceneImage;
    public TextMeshProUGUI narrationText;

    [Header("스킵 UI (자동 생성 — Inspector에서 직접 연결도 가능)")]
    public Image skipGaugeFill;
    private RectTransform skipGaugeFillRT;

    [Header("설정")]
    public float typingSpeed = 0.045f;
    public string nextScene = "NodeMap";
    public float skipHoldDuration = 2f;
    [Tooltip("ESC 길게 눌러 스킵 시 페이드 인/아웃 시간 (초).")]
    public float skipFadeDuration = 0.2f;

    private bool isSkipping = false;
    private float skipProgress = 0f;

    // 연출 오버레이 (코드 생성)
    private RectTransform canvasRT;
    private Image vignette;
    private RawImage grain;
    private Image redPulse;          // 붉은 시선 펄스 (검은 태양 분위기)
    private RectTransform letterTop, letterBottom;

    private float redBaseAlpha = 0.06f;   // 평상시 은은한 붉은 기운
    private float redCurrentExtra = 0f;   // 시선 스파이크로 추가되는 붉은 강도

    // 카메라 흔들림(셰이크) 오프셋
    private float shakeAmount = 0f;
    private Vector2 shakeOffset = Vector2.zero;

    private enum Beat { None, Lightning, RedSpike, Finale }

    private struct Shot
    {
        public string narration;
        public Vector2 focus;     // 이미지 정규화 좌표 (중심=0,0 / x 우측+, y 상단+ / 범위 -0.5~0.5)
        public float scale;       // 줌 배율 (1 = 전체 화면)
        public float moveDuration; // 이전 샷에서 이 샷으로 이동하는 시간
        public float hold;        // 타이핑 끝난 뒤 머무는 시간
        public Beat beat;

        public Shot(string n, Vector2 f, float s, float m, float h, Beat b)
        {
            narration = n; focus = f; scale = s;
            moveDuration = m; hold = h; beat = b;
        }
    }

    private Shot[] shots;

    void Start()
    {
        // 카메라 동선: 와이드 → 인물 → 검은 태양 → 눈(핵) → 성채 → 와이드+피날레
        shots = new Shot[]
        {
            // ① 와이드 — 죽은 세계 전경 (느린 푸시 인)
            new Shot("세계는 이미 끝나 있었다.",
                new Vector2(0f, 0f), 1.08f, 0f, 1.8f, Beat.None),

            // ② 후드 인물(좌하단) 클로즈업
            new Shot("잿더미가 된 땅 위에, 단 하나의 그림자가 서 있었다.",
                new Vector2(-0.16f, -0.06f), 2.0f, 3.8f, 1.8f, Beat.None),

            // ③ 검은 태양(우상단)으로 상승
            new Shot("신을 삼킨 검은 태양이, 하늘을 갈랐다.",
                new Vector2(0.16f, 0.20f), 1.9f, 4.0f, 1.6f, Beat.Lightning),

            // ④ 태양의 핵 = 「눈」 클로즈업 — 응시
            new Shot("그것은 눈을 뜨고, 세상을 응시하기 시작했다.",
                new Vector2(0.15f, 0.18f), 2.4f, 3.2f, 2.0f, Beat.RedSpike),

            // ⑤ 무너진 성채(우하단)로 하강
            new Shot("왕도, 신앙도, 빛도 — 모두 그 시선 아래 스러졌다.",
                new Vector2(0.19f, -0.13f), 1.9f, 3.8f, 1.8f, Beat.None),

            // ⑥ 다시 와이드 — 낙인을 짊어진 자 (피날레 플래시 → 암전)
            new Shot("그리고 낙인을 짊어진 자가, 그 응시에 맞선다.",
                new Vector2(0.02f, 0f), 1.06f, 4.0f, 2.2f, Beat.Finale),
        };

        canvasRT = cutsceneImage != null ? cutsceneImage.transform.parent as RectTransform : null;

        BuildCinematicOverlays();

        if (skipGaugeFill == null && canvasRT != null)
            CreateSkipUI(canvasRT);

        StartCoroutine(PlayCutscene());
    }

    void Update()
    {
        // 붉은 펄스: 평상시 천천히 호흡 + 스파이크 감쇠
        if (redPulse != null)
        {
            float breathe = redBaseAlpha + Mathf.Sin(Time.time * 1.3f) * 0.02f;
            redCurrentExtra = Mathf.MoveTowards(redCurrentExtra, 0f, Time.deltaTime * 0.9f);
            Color c = redPulse.color;
            c.a = Mathf.Clamp01(breathe + redCurrentExtra);
            redPulse.color = c;
        }

        // 필름 그레인: 매 프레임 살짝 흘려서 정적인 노이즈처럼
        if (grain != null)
        {
            float ox = Mathf.Repeat(Time.time * 7.3f, 1f);
            float oy = Mathf.Repeat(Time.time * 5.1f, 1f);
            grain.uvRect = new Rect(ox, oy, grain.uvRect.width, grain.uvRect.height);
        }

        // 카메라 셰이크 감쇠
        if (shakeAmount > 0f)
        {
            shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.deltaTime * 60f);
            shakeOffset = new Vector2(
                Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f) * shakeAmount * 2f;
        }
        else shakeOffset = Vector2.zero;

        // ---- 스킵 처리 ----
        if (isSkipping) return;

        if (Input.GetKey(KeyCode.Escape))
        {
            skipProgress = Mathf.MoveTowards(skipProgress, 1f, Time.deltaTime / skipHoldDuration);
            UpdateGaugeVisual(skipProgress);
            if (skipProgress >= 1f)
                isSkipping = true;
        }
        else if (skipProgress > 0f)
        {
            skipProgress = 0f;
            UpdateGaugeVisual(0f);
        }
    }

    // ===================== 카메라 좌표 환산 =====================

    /// <summary>정규화 포커스(중심 기준 -0.5~0.5)와 배율을 anchoredPosition으로 환산.</summary>
    Vector2 FocusToPos(Vector2 focus, float scale)
    {
        float w = cutsceneImage.rect.width;
        float h = cutsceneImage.rect.height;
        // 이미지를 (-focus*size*scale) 만큼 움직이면 해당 지점이 화면 중앙에 옴
        return new Vector2(-focus.x * w * scale, -focus.y * h * scale);
    }

    void ApplyTransform(Vector2 basePos, float scale)
    {
        cutsceneImage.anchoredPosition = basePos + shakeOffset;
        cutsceneImage.localScale = Vector3.one * scale;
    }

    // ===================== 메인 시퀀스 =====================

    IEnumerator PlayCutscene()
    {
        // 레이아웃이 잡힌 뒤 rect 크기를 읽어야 정확
        yield return null;

        // 시작 상태: 첫 샷 위치/배율로 세팅
        ApplyTransform(FocusToPos(shots[0].focus, shots[0].scale), shots[0].scale);

        // 레터박스 페이드 인
        StartCoroutine(FadeLetterbox(true, 1.2f));

        for (int i = 0; i < shots.Length; i++)
        {
            if (isSkipping) break;
            Shot shot = shots[i];

            // 1) 이전 샷에서 이 샷으로 부드럽게 이동
            if (shot.moveDuration > 0f)
                yield return StartCoroutine(MoveTo(shot.focus, shot.scale, shot.moveDuration));
            else
                ApplyTransform(FocusToPos(shot.focus, shot.scale), shot.scale);

            // 2) 도착 후 한 박자 멈춤
            yield return WaitWithLife(0.35f, shot.focus, shot.scale, shot.scale);

            // 3) 연출 비트
            switch (shot.beat)
            {
                case Beat.Lightning: StartCoroutine(LightningFlash()); break;
                case Beat.RedSpike:  redCurrentExtra = 0.45f; shakeAmount = 6f; break;
                case Beat.Finale:    /* 타이핑 후 처리 */ break;
            }

            // 4) 타이핑 + 머무름 — 그 동안 천천히 줌인 드리프트로 화면이 숨 쉬게
            float driftScale = shot.scale * 1.05f;
            float lifeTime = shot.narration.Length * typingSpeed + shot.hold + 0.6f;
            StartCoroutine(Drift(shot.focus, shot.scale, driftScale, lifeTime));

            yield return StartCoroutine(TypeText(shot.narration));
            yield return WaitWithLife(shot.hold, Vector2.zero, 0f, 0f); // 드리프트는 별도 코루틴이 처리

            // 5) 피날레: 화이트→레드 플래시 후 암전 전환
            if (shot.beat == Beat.Finale)
            {
                yield return StartCoroutine(FinaleFlash());
                break;
            }

            // 6) 텍스트 페이드 아웃
            yield return StartCoroutine(FadeText(1f, 0f));
        }

        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene(nextScene, isSkipping ? skipFadeDuration : -1f);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    /// <summary>현재 위치/배율에서 목표 포커스/배율로 ease-in-out 이동.</summary>
    IEnumerator MoveTo(Vector2 focus, float targetScale, float duration)
    {
        Vector2 startPos = cutsceneImage.anchoredPosition - shakeOffset;
        float startScale = cutsceneImage.localScale.x;
        Vector2 endPos = FocusToPos(focus, targetScale);

        float elapsed = 0f;
        while (elapsed < duration && !isSkipping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float s = Mathf.Lerp(startScale, targetScale, t);
            ApplyTransform(Vector2.Lerp(startPos, endPos, t), s);
            yield return null;
        }
        ApplyTransform(endPos, targetScale);
    }

    /// <summary>같은 포커스를 유지한 채 배율만 천천히 키워 켄 번스 푸시 효과.</summary>
    IEnumerator Drift(Vector2 focus, float fromScale, float toScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !isSkipping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Lerp(fromScale, toScale, t);
            ApplyTransform(FocusToPos(focus, s), s);
            yield return null;
        }
    }

    /// <summary>드리프트 코루틴이 도는 동안 지정 시간만 대기.</summary>
    IEnumerator WaitWithLife(float seconds, Vector2 focus, float fromScale, float toScale)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !isSkipping)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ===================== 연출 효과 =====================

    IEnumerator LightningFlash()
    {
        // 짧고 날카로운 흰 번쩍 2회
        for (int n = 0; n < 2; n++)
        {
            yield return StartCoroutine(QuickFlash(new Color(1f, 1f, 1f), 0.06f, 0.18f, 0.5f));
            yield return new WaitForSeconds(0.08f);
        }
    }

    IEnumerator FinaleFlash()
    {
        shakeAmount = 8f;
        redCurrentExtra = 0.6f;
        yield return StartCoroutine(QuickFlash(new Color(1f, 0.85f, 0.85f), 0.12f, 0.45f, 1f));
        // 흰빛이 잦아들며 다음 씬 페이드로 자연 연결
        yield return new WaitForSeconds(0.1f);
    }

    /// <summary>전체 화면 컬러 플래시 (페이드 인/아웃).</summary>
    IEnumerator QuickFlash(Color color, float inTime, float outTime, float maxAlpha)
    {
        GameObject flashObj = new GameObject("Flash");
        flashObj.transform.SetParent(canvasRT, false);
        Image flash = flashObj.AddComponent<Image>();
        RectTransform rt = flash.rectTransform;
        Stretch(rt);
        flash.transform.SetAsLastSibling();
        flash.raycastTarget = false;

        float e = 0f;
        while (e < inTime) { e += Time.deltaTime; flash.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0f, maxAlpha, e / inTime)); yield return null; }
        e = 0f;
        while (e < outTime) { e += Time.deltaTime; flash.color = new Color(color.r, color.g, color.b, Mathf.Lerp(maxAlpha, 0f, e / outTime)); yield return null; }
        Destroy(flashObj);
    }

    IEnumerator TypeText(string text)
    {
        narrationText.text = "";
        Color c = narrationText.color; c.a = 1f; narrationText.color = c;

        foreach (char ch in text)
        {
            if (isSkipping) { narrationText.text = text; yield break; }
            narrationText.text += ch;
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    IEnumerator FadeText(float from, float to)
    {
        float elapsed = 0f;
        Color color = narrationText.color;
        while (elapsed < 0.5f && !isSkipping)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, to, elapsed / 0.5f);
            narrationText.color = color;
            yield return null;
        }
        color.a = to; narrationText.color = color;
    }

    // ===================== 오버레이 생성 =====================

    void BuildCinematicOverlays()
    {
        if (canvasRT == null) return;

        // 이미지를 맨 뒤로
        cutsceneImage.SetAsFirstSibling();

        // 비네트 (가장자리 어둡게) — 라디얼 그라데이션 텍스처 생성
        vignette = CreateOverlayImage("Vignette", MakeVignetteSprite(), new Color(1f, 1f, 1f, 0.9f));

        // 붉은 시선 펄스 — 라디얼(중앙이 붉게)
        redPulse = CreateOverlayImage("RedPulse", MakeRadialSprite(new Color(0.7f, 0.05f, 0.05f)), new Color(1f, 1f, 1f, redBaseAlpha));

        // 필름 그레인 — RawImage + 노이즈 텍스처 타일
        GameObject grainObj = new GameObject("Grain");
        grainObj.transform.SetParent(canvasRT, false);
        grain = grainObj.AddComponent<RawImage>();
        grain.texture = MakeNoiseTexture(64);
        grain.color = new Color(1f, 1f, 1f, 0.04f);
        grain.raycastTarget = false;
        grain.uvRect = new Rect(0f, 0f, 4f, 4f); // 작게 타일
        Stretch(grain.rectTransform);

        // 레터박스 (상/하 검은 띠) — 처음엔 투명
        letterTop = CreateBar("LetterTop", true);
        letterBottom = CreateBar("LetterBottom", false);

        // 나레이션 텍스트를 맨 위로
        if (narrationText != null) narrationText.transform.SetAsLastSibling();
    }

    Image CreateOverlayImage(string name, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(canvasRT, false);
        Image img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        return img;
    }

    RectTransform CreateBar(string name, bool top)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(canvasRT, false);
        Image img = obj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // 페이드 인 전 투명
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rt.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 0f); // 페이드 시 높이 부여
        return rt;
    }

    IEnumerator FadeLetterbox(bool show, float duration)
    {
        // 2.35:1 시네마스코프 느낌의 띠 높이 (화면 높이의 약 11%)
        float targetH = canvasRT.rect.height * 0.11f;
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, e / duration);
            float h = Mathf.Lerp(0f, targetH, show ? t : 1f - t);
            float a = show ? t : 1f - t;
            SetBar(letterTop, h, a);
            SetBar(letterBottom, h, a);
            yield return null;
        }
    }

    void SetBar(RectTransform bar, float height, float alpha)
    {
        if (bar == null) return;
        bar.sizeDelta = new Vector2(0f, height);
        Image img = bar.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, alpha);
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ===================== 절차적 텍스처 =====================

    Sprite MakeVignetteSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size * 0.72f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);   // 중앙 투명 → 가장자리 어둠
                a = Mathf.Pow(a, 1.6f);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Sprite MakeRadialSprite(Color tint)
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.Clamp01(1f - d);     // 중앙이 진하고 바깥은 0
                a = Mathf.Pow(a, 1.8f);
                tex.SetPixel(x, y, new Color(tint.r, tint.g, tint.b, a));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Texture2D MakeNoiseTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Random.value는 deterministic seed로 — 빌드/플레이마다 동일 노이즈
        Random.State prev = Random.state;
        Random.InitState(12345);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float g = Random.value;
                tex.SetPixel(x, y, new Color(g, g, g, 1f));
            }
        Random.state = prev;
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    // ===================== 스킵 UI =====================

    void UpdateGaugeVisual(float t)
    {
        if (skipGaugeFillRT == null) return;
        skipGaugeFillRT.anchorMax = new Vector2(t, 1f);
        skipGaugeFillRT.offsetMax = Vector2.zero;
    }

    void CreateSkipUI(RectTransform parent)
    {
        if (parent == null) return;

        GameObject container = new GameObject("SkipUI");
        container.transform.SetParent(parent, false);
        RectTransform cRT = container.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1f, 0f);
        cRT.anchorMax = new Vector2(1f, 0f);
        cRT.pivot = new Vector2(1f, 0f);
        cRT.anchoredPosition = new Vector2(-30f, 30f);
        cRT.sizeDelta = new Vector2(210f, 52f);

        Image panelBG = container.AddComponent<Image>();
        panelBG.color = new Color(0f, 0f, 0f, 0.45f);
        panelBG.raycastTarget = false;

        GameObject labelObj = new GameObject("SkipLabel");
        labelObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "ESC - 스킵";
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.raycastTarget = false;
        RectTransform lRT = labelObj.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.42f);
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = new Vector2(6f, 0f);
        lRT.offsetMax = new Vector2(-6f, -4f);

        GameObject bgObj = new GameObject("GaugeBG");
        bgObj.transform.SetParent(container.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        bgImage.raycastTarget = false;
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0f);
        bgRT.anchorMax = new Vector2(1f, 0.38f);
        bgRT.offsetMin = new Vector2(6f, 5f);
        bgRT.offsetMax = new Vector2(-6f, 0f);

        GameObject fillObj = new GameObject("GaugeFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        skipGaugeFill = fillObj.AddComponent<Image>();
        skipGaugeFill.color = new Color(0.85f, 0.85f, 1f, 1f);
        skipGaugeFill.raycastTarget = false;
        skipGaugeFillRT = fillObj.GetComponent<RectTransform>();
        skipGaugeFillRT.anchorMin = Vector2.zero;
        skipGaugeFillRT.anchorMax = new Vector2(0f, 1f);
        skipGaugeFillRT.offsetMin = Vector2.zero;
        skipGaugeFillRT.offsetMax = Vector2.zero;

        container.transform.SetAsLastSibling();
    }
}
