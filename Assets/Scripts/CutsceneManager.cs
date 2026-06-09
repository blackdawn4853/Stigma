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
    public RectTransform cutsceneImage;    // 이미지1 (검은 태양 세계관)
    public RectTransform cutsceneImage2;   // 이미지2 (주인공 + 물에 비친 자아)
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

    // 폭풍 (검은 태양 공개 시점부터 ON — 지지직 그레인 강화 + 주기 번개)
    private bool weatherActive;

    private float redBaseAlpha = 0.06f;   // 평상시 은은한 붉은 기운
    private float redCurrentExtra = 0f;   // 시선 스파이크로 추가되는 붉은 강도

    // 카메라 흔들림(셰이크) 오프셋
    private float shakeAmount = 0f;
    private Vector2 shakeOffset = Vector2.zero;

    private enum Beat { None, Lightning, RedSpike, Finale, RevealStorm }

    private struct Shot
    {
        public string narration;
        public Vector2 focus;     // 이미지 정규화 좌표 (중심=0,0 / x 우측+, y 상단+ / 범위 -0.5~0.5)
        public float scale;       // 줌 배율 (1 = 전체 화면)
        public float moveDuration; // 이전 샷에서 이 샷으로 이동하는 시간
        public float hold;        // 타이핑 끝난 뒤 머무는 시간
        public Beat beat;
        public int image;         // 1 = cutsceneImage, 2 = cutsceneImage2 (다른 이미지로 바뀌면 디졸브)
        public bool blink;        // 같은 이미지 내에서 무빙 대신 "깜빡(암전 블링크)"으로 컷 이동

        public Shot(string n, Vector2 f, float s, float m, float h, Beat b, int img, bool bl = false)
        {
            narration = n; focus = f; scale = s;
            moveDuration = m; hold = h; beat = b; image = img; blink = bl;
        }
    }

    // 현재 카메라가 훑고 있는 이미지(런타임에 전환)
    private RectTransform curImg;

    private Shot[] shots;

    void Start()
    {
        // 동선: [이미지1] 와이드→인물→검은태양→눈→성채  ⇒ 플래시 컷 ⇒  [이미지2] 인물→물웅덩이→비친 자아(피날레)
        // 카메라 무빙 최소화: 큰 줌/팬 제거 → 거의 전체 프레임을 유지한 채 아주 미세한 푸시·팬만.
        // (이전엔 2.0~2.4배 줌 + 큰 포커스 점프라 "짜쳤음")
        shots = new Shot[]
        {
            // ===== 이미지1 =====
            // 왕국(눈 아래 = 중앙~우측의 도시) 클로즈업 — 비 X, 번개 X — 1·2번
            // 캐릭터는 왼쪽 전경이라 오른쪽으로 잡아 제외, 눈은 위로 빼서 3번 전까지 숨김.
            new Shot("한때, 이 땅은 찬란했다.",
                new Vector2(0.22f, -0.08f), 2.0f, 0f, 1.8f, Beat.None, 1),
            new Shot("왕국과 신앙이, 영원을 노래했다.",
                new Vector2(0.22f, -0.08f), 2.0f, 0f, 1.8f, Beat.None, 1),

            // 검은 태양 — 번개 번쩍하며 전체 일러스트로 확 빠짐 + 비/주기번개 시작 — 3번
            new Shot("허나 하늘이 갈라지고, 검은 태양이 눈을 뜨니,",
                new Vector2(0f, 0f), 1.0f, 0f, 2.0f, Beat.RevealStorm, 1),

            // 전체 일러 유지 (비 + 주기 번개) — 4·5·6번
            new Shot("그 무엇도 남지 않았다.",
                new Vector2(0f, 0f), 1.0f, 0f, 1.8f, Beat.None, 1),
            new Shot("오직 낙인자들의 영혼만이 떠돌 뿐이다.",
                new Vector2(0f, 0f), 1.0f, 0f, 2.0f, Beat.None, 1),
            new Shot("낙인을 지고,",
                new Vector2(0f, 0f), 1.0f, 0f, 1.2f, Beat.None, 1),

            // ===== 이미지2 — 물에 비친 자아 (암전 디졸브) — 7번 =====
            new Shot("삶과 죽음을 영원히 반복하리……",
                new Vector2(-0.03f, -0.03f), 1.06f, 0f, 3.0f, Beat.None, 2),
        };

        canvasRT = cutsceneImage != null ? cutsceneImage.transform.parent as RectTransform : null;

        BuildCinematicOverlays();

        // 이미지2는 시작 시 숨김 (플래시 컷 때 등장)
        if (cutsceneImage2 != null) SetImageAlpha(cutsceneImage2, 0f);
        curImg = cutsceneImage;

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
    Vector2 FocusToPos(RectTransform img, Vector2 focus, float scale)
    {
        float w = img.rect.width;
        float h = img.rect.height;
        // 이미지를 (-focus*size*scale) 만큼 움직이면 해당 지점이 화면 중앙에 옴
        return new Vector2(-focus.x * w * scale, -focus.y * h * scale);
    }

    void ApplyTransform(RectTransform img, Vector2 basePos, float scale)
    {
        // 이미지가 항상 화면을 덮도록 위치 클램프 → 무빙 시 가장자리 공백 방지.
        // (이미지 크기*scale 가 화면보다 큰 만큼만 이동 허용)
        img.anchoredPosition = ClampToCover(img, basePos, scale) + shakeOffset;
        img.localScale = Vector3.one * scale;
    }

    Vector2 ClampToCover(RectTransform img, Vector2 pos, float scale)
    {
        if (canvasRT == null) return pos;
        const float margin = 8f; // 셰이크/서브픽셀 여유
        float maxX = Mathf.Max(0f, (img.rect.width  * scale - canvasRT.rect.width ) * 0.5f - margin);
        float maxY = Mathf.Max(0f, (img.rect.height * scale - canvasRT.rect.height) * 0.5f - margin);
        return new Vector2(Mathf.Clamp(pos.x, -maxX, maxX), Mathf.Clamp(pos.y, -maxY, maxY));
    }

    void SetImageAlpha(RectTransform img, float a)
    {
        if (img == null) return;
        Image im = img.GetComponent<Image>();
        if (im != null) { Color c = im.color; c.a = a; im.color = c; }
    }

    // ===================== 메인 시퀀스 =====================

    IEnumerator PlayCutscene()
    {
        // 레이아웃이 잡힌 뒤 rect 크기를 읽어야 정확
        yield return null;

        // 시작 상태: 첫 샷 위치/배율로 세팅
        curImg = cutsceneImage;
        ApplyTransform(curImg, FocusToPos(curImg, shots[0].focus, shots[0].scale), shots[0].scale);

        // 레터박스 페이드 인
        StartCoroutine(FadeLetterbox(true, 1.2f));

        for (int i = 0; i < shots.Length; i++)
        {
            if (isSkipping) break;
            Shot shot = shots[i];
            RectTransform shotImg = (shot.image == 2 && cutsceneImage2 != null) ? cutsceneImage2 : cutsceneImage;

            // 1) 전환 (전부 정적 — 드리프트/줌 무빙 없음. 깜빡 컷 or 암전 디졸브)
            if (shotImg != curImg)
            {
                // ---- 다른 이미지로 전환: 암전 디졸브(페이드) ----
                ApplyTransform(shotImg, FocusToPos(shotImg, shot.focus, shot.scale), shot.scale);
                yield return StartCoroutine(DarkDip(curImg, shotImg));
                curImg = shotImg;
            }
            else if (shot.beat == Beat.RevealStorm)
            {
                // ---- 번개 번쩍하며 전체 일러스트로 컷 + 비/주기번개 시작 ----
                yield return StartCoroutine(LightningReveal(curImg, shot.focus, shot.scale));
                StartWeather();
            }
            else if (shot.blink)
            {
                // ---- 같은 이미지 내 깜빡(암전 블링크)으로 구도 점프 ----
                yield return StartCoroutine(BlinkCut(curImg, shot.focus, shot.scale));
            }
            else
            {
                // 같은 구도(다음 대사) — 즉시 적용, 움직임 없음
                ApplyTransform(curImg, FocusToPos(curImg, shot.focus, shot.scale), shot.scale);
            }

            // 2) 한 박자 멈춤
            yield return WaitWithLife(0.3f);

            // 3) 연출 비트
            switch (shot.beat)
            {
                case Beat.Lightning: StartCoroutine(LightningFlash()); break;
                case Beat.RedSpike:  redCurrentExtra = 0.4f; shakeAmount = 2.5f; break;
                case Beat.Finale:    /* 타이핑 후 처리 */ break;
            }

            // 4) 나레이션 페이드 인(DD 톤) + 머무름 (정적)
            float reading = Mathf.Min(3.5f, shot.narration.Length * 0.085f);
            yield return StartCoroutine(RevealText(shot.narration));
            yield return WaitWithLife(reading + shot.hold);

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
    IEnumerator MoveTo(RectTransform img, Vector2 focus, float targetScale, float duration)
    {
        Vector2 startPos = img.anchoredPosition - shakeOffset;
        float startScale = img.localScale.x;
        Vector2 endPos = FocusToPos(img, focus, targetScale);

        float elapsed = 0f;
        while (elapsed < duration && !isSkipping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float s = Mathf.Lerp(startScale, targetScale, t);
            ApplyTransform(img, Vector2.Lerp(startPos, endPos, t), s);
            yield return null;
        }
        ApplyTransform(img, endPos, targetScale);
    }

    /// <summary>같은 포커스를 유지한 채 배율만 천천히 키워 켄 번스 푸시 효과.</summary>
    IEnumerator Drift(RectTransform img, Vector2 focus, float fromScale, float toScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !isSkipping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Lerp(fromScale, toScale, t);
            ApplyTransform(img, FocusToPos(img, focus, s), s);
            yield return null;
        }
    }

    /// <summary>드리프트 코루틴이 도는 동안 지정 시간만 대기.</summary>
    IEnumerator WaitWithLife(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !isSkipping)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>두 이미지 사이 전환 — 흰 플래시 절정에서 보이는 이미지를 교체(번쩍 컷).</summary>
    IEnumerator FlashCut(RectTransform from, RectTransform to)
    {
        GameObject flashObj = new GameObject("FlashCut");
        flashObj.transform.SetParent(canvasRT, false);
        Image flash = flashObj.AddComponent<Image>();
        Stretch(flash.rectTransform);
        flash.transform.SetAsLastSibling();
        flash.raycastTarget = false;

        Color col = new Color(1f, 0.97f, 0.95f);
        float inTime = 0.12f, outTime = 0.5f, maxAlpha = 1f;

        float e = 0f;
        while (e < inTime) { e += Time.deltaTime; flash.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0f, maxAlpha, e / inTime)); yield return null; }

        // 절정 — 이미지 스왑 + 살짝 셰이크
        SetImageAlpha(from, 0f);
        SetImageAlpha(to, 1f);
        shakeAmount = 2f;

        e = 0f;
        while (e < outTime) { e += Time.deltaTime; flash.color = new Color(col.r, col.g, col.b, Mathf.Lerp(maxAlpha, 0f, e / outTime)); yield return null; }
        Destroy(flashObj);
    }

    /// <summary>웅덩이로 가라앉았다 떠오르듯 — 암전으로 덮은 뒤 이미지 교체(흰 플래시 X, 다크소울 톤).</summary>
    IEnumerator DarkDip(RectTransform from, RectTransform to)
    {
        GameObject dipObj = new GameObject("DarkDip");
        dipObj.transform.SetParent(canvasRT, false);
        Image dip = dipObj.AddComponent<Image>();
        Stretch(dip.rectTransform);
        dip.transform.SetAsLastSibling();
        dip.raycastTarget = false;

        Color col = new Color(0.02f, 0.02f, 0.035f);
        float inTime = 0.55f, hold = 0.12f, outTime = 0.8f;

        float e = 0f;
        while (e < inTime) { e += Time.deltaTime; dip.color = new Color(col.r, col.g, col.b, Mathf.SmoothStep(0f, 1f, e / inTime)); yield return null; }
        dip.color = new Color(col.r, col.g, col.b, 1f);

        // 암전 절정에서 이미지 교체
        SetImageAlpha(from, 0f);
        SetImageAlpha(to, 1f);
        yield return new WaitForSeconds(hold);

        e = 0f;
        while (e < outTime) { e += Time.deltaTime; dip.color = new Color(col.r, col.g, col.b, Mathf.SmoothStep(1f, 0f, e / outTime)); yield return null; }
        Destroy(dipObj);
    }

    /// <summary>같은 이미지 내에서 "눈 깜빡"처럼 빠른 암전으로 구도를 즉시 점프(무빙 X).</summary>
    IEnumerator BlinkCut(RectTransform img, Vector2 focus, float scale)
    {
        GameObject bObj = new GameObject("Blink");
        bObj.transform.SetParent(canvasRT, false);
        Image bi = bObj.AddComponent<Image>();
        Stretch(bi.rectTransform);
        bi.transform.SetAsLastSibling();
        bi.raycastTarget = false;
        Color col = new Color(0.01f, 0.01f, 0.02f);

        float inT = 0.12f, outT = 0.16f;
        float e = 0f;
        while (e < inT) { e += Time.deltaTime; bi.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(e / inT)); yield return null; }
        bi.color = new Color(col.r, col.g, col.b, 1f);

        // 암전 절정에서 카메라 즉시 이동 (무빙 없이 컷)
        ApplyTransform(img, FocusToPos(img, focus, scale), scale);
        yield return null;

        e = 0f;
        while (e < outT) { e += Time.deltaTime; bi.color = new Color(col.r, col.g, col.b, 1f - Mathf.Clamp01(e / outT)); yield return null; }
        Destroy(bObj);
    }

    // ── 날씨: 비 + 주기 번개 (검은 태양 공개 시점부터 ON) ────────
    void StartWeather()
    {
        if (weatherActive) return;
        weatherActive = true;
        StartCoroutine(LightningLoop());   // 주기 번개만. 그레인(지지직)은 원래 그대로.
    }

    IEnumerator LightningLoop()
    {
        // 공개 직후 바로 안 침(LightningReveal 이 이미 번쩍함). 이후 가끔 한 번씩만.
        while (weatherActive && !isSkipping)
        {
            float wait = Random.Range(7f, 13f), t = 0f;
            while (t < wait && weatherActive && !isSkipping) { t += Time.deltaTime; yield return null; }
            if (!weatherActive || isSkipping) yield break;
            yield return StartCoroutine(StormLightning(Random.Range(0.45f, 0.7f)));
        }
    }

    IEnumerator StormLightning(float intensity)
    {
        shakeAmount = Mathf.Max(shakeAmount, 2.2f * intensity);
        redCurrentExtra = Mathf.Max(redCurrentExtra, 0.1f);
        // 한 번만, 느리게(오래 머물게) 번쩍
        yield return StartCoroutine(QuickFlash(new Color(0.9f, 0.93f, 1f), 0.08f, 0.6f, intensity));
    }

    /// <summary>번개가 번쩍하며 새 구도(전체 일러스트)로 컷.</summary>
    IEnumerator LightningReveal(RectTransform img, Vector2 focus, float scale)
    {
        GameObject fObj = new GameObject("LightningReveal");
        fObj.transform.SetParent(canvasRT, false);
        Image fl = fObj.AddComponent<Image>();
        Stretch(fl.rectTransform);
        fl.transform.SetAsLastSibling();
        fl.raycastTarget = false;
        Color col = new Color(0.92f, 0.95f, 1f);

        float inT = 0.06f, e = 0f;
        while (e < inT) { e += Time.deltaTime; fl.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(e / inT)); yield return null; }
        fl.color = new Color(col.r, col.g, col.b, 1f);

        // 절정에서 전체 일러로 컷 + 셰이크
        ApplyTransform(img, FocusToPos(img, focus, scale), scale);
        shakeAmount = 5f;
        redCurrentExtra = 0.3f;
        yield return new WaitForSeconds(0.04f);

        float outT = 0.35f; e = 0f;
        while (e < outT) { e += Time.deltaTime; fl.color = new Color(col.r, col.g, col.b, 1f - Mathf.Clamp01(e / outT)); yield return null; }
        Destroy(fObj);
    }


    // ===================== 연출 효과 =====================

    IEnumerator LightningFlash()
    {
        // 은은한 흰 번쩍 1회 (이전엔 2회 날카롭게 → 정적인 무드로 톤다운)
        yield return StartCoroutine(QuickFlash(new Color(1f, 1f, 1f), 0.08f, 0.35f, 0.32f));
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

    // 다키스트 던전 톤 — 한 줄을 통째로 부드럽게 페이드 인 (타자기 X)
    IEnumerator RevealText(string text)
    {
        narrationText.text = text;
        Color c = narrationText.color;
        if (isSkipping) { c.a = 1f; narrationText.color = c; yield break; }

        c.a = 0f; narrationText.color = c;   // 페이드 시작 전 즉시 투명 (1프레임 풀알파 튐 방지)
        float e = 0f, dur = 0.45f;
        while (e < dur)
        {
            e += Time.deltaTime;
            c.a = Mathf.SmoothStep(0f, 1f, e / dur);
            narrationText.color = c;
            yield return null;
        }
        c.a = 1f; narrationText.color = c;
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

        // 두 일러스트를 맨 뒤로 (오버레이/텍스트보다 아래). 이미지1이 이미지2 앞에 오도록 순서 정리.
        if (cutsceneImage2 != null) cutsceneImage2.SetAsFirstSibling();
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

        // 나레이션을 하단 자막 밴드로 (캐릭터 안 가림) + 어두운 스크림
        SetupNarrationBand();

        // 나레이션 텍스트를 맨 위로
        if (narrationText != null) narrationText.transform.SetAsLastSibling();
    }

    // 나레이션을 하단 중앙 밴드로 재배치 + 뒤에 어두운 그라데이션 스크림 (다키스트 던전 톤)
    void SetupNarrationBand()
    {
        if (canvasRT == null || narrationText == null) return;

        // 하단 스크림 (어두운 그라데이션) — 글자가 일러 위에 둥둥 뜨지 않게 받쳐줌
        GameObject scrimObj = new GameObject("NarrationScrim");
        scrimObj.transform.SetParent(canvasRT, false);
        Image scrim = scrimObj.AddComponent<Image>();
        scrim.sprite = MakeBottomScrimSprite();
        scrim.color = new Color(0f, 0f, 0f, 0.8f);
        scrim.raycastTarget = false;
        RectTransform srt = scrim.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0.4f);
        srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
        scrim.transform.SetAsLastSibling();

        // 나레이션 → 캔버스 직속 + 하단 중앙으로 재배치 (기존 BottomBar 위치 의존 제거)
        narrationText.rectTransform.SetParent(canvasRT, false);
        RectTransform nrt = narrationText.rectTransform;
        nrt.anchorMin = new Vector2(0.5f, 0f);
        nrt.anchorMax = new Vector2(0.5f, 0f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, 120f);
        nrt.sizeDelta = new Vector2(1480f, 240f);

        // 스타일 — 명조 이탤릭, 재빛 오프화이트, 자간/행간 여유, 가독성 아웃라인
        narrationText.fontStyle = FontStyles.Italic;
        narrationText.fontSize = 44f;
        Color tc = new Color(0.9f, 0.88f, 0.83f, narrationText.color.a);
        narrationText.color = tc;
        narrationText.alignment = TextAlignmentOptions.Bottom;
        narrationText.characterSpacing = 2f;
        narrationText.lineSpacing = 8f;
        narrationText.textWrappingMode = TextWrappingModes.Normal;
        narrationText.outlineColor = new Color(0f, 0f, 0f, 1f);
        narrationText.outlineWidth = 0.16f;
    }

    Sprite MakeBottomScrimSprite()
    {
        int h = 64;
        Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)(h - 1);          // 0=아래, 1=위
            float a = Mathf.Pow(1f - k, 1.5f);     // 아래 진하게 → 위로 투명
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
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
