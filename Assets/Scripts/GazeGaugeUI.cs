using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 시선(Gaze) 게이지.
//  · 프레임(눈+라인+장식)은 항상 통째로 표시 (검정, 시선 100 도달 시 흰색 스왑).
//  · 그 라인 안쪽을 따라 "채움 bar"가 가운데(눈)에서 양옆으로 대칭으로 차오른다.
//    - 채움은 붉은(중앙)→흰(양끝) 그라데이션. 프레임 뒤에 깔려 검은 라인이 그 위에 실루엣처럼 얹힘.
//  · backing: 게이지 뒤 은은한 어두운 글로우 → 밝은 하늘 위에서도 가독성 확보.
// 레이어 순서(아래→위): Backing → FillMask>Fill → Frame
[DisallowMultipleComponent]
public class GazeGaugeUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("장식 프레임 (항상 풀, 검정/흰색 스왑)")]
    public Image frameImage;
    [Tooltip("RectMask2D — 폭을 가운데 기준으로 키워 채움 bar 를 reveal")]
    public RectTransform fillMask;
    [Tooltip("채움 bar (붉은→흰 그라데이션, 스프라이트는 코드 생성)")]
    public Image fillImage;
    [Tooltip("게이지 뒤 어두운 backing (가독성). 코드 생성")]
    public Image backingImage;
    [Tooltip("증가 시 펀치 스케일을 줄 루트 (없으면 자기 자신)")]
    public RectTransform rootRect;

    [Header("프레임 스프라이트")]
    public Sprite blackSprite;   // 평소
    public Sprite whiteSprite;   // 시선 100

    [Header("채움 크기")]
    [Tooltip("시선 100% 일 때 채움 폭 (라인 전체 길이)")]
    public float fillFullWidth = 1280f;

    [Header("연출")]
    [Tooltip("채움 폭이 목표로 따라가는 속도")]
    public float fillLerpSpeed = 9f;
    [Tooltip("시선 증가 시 펀치 배수")]
    public float punchScale = 1.08f;
    public float punchDuration = 0.18f;

    int currentGaze = 0;
    float targetWidth = 0f;
    bool isWhite = false;
    Coroutine punchCo;

    void Awake()
    {
        if (rootRect == null) rootRect = transform as RectTransform;
        if (fillImage != null && fillImage.sprite == null)
            fillImage.sprite = BuildFillGradient();
        if (backingImage != null && backingImage.sprite == null)
            backingImage.sprite = BuildBackingGlow();
        if (fillMask != null) ApplyFillWidth(0f);
    }

    void Update()
    {
        if (fillMask == null) return;
        float cur = fillMask.sizeDelta.x;
        if (Mathf.Abs(cur - targetWidth) > 0.5f)
            ApplyFillWidth(Mathf.Lerp(cur, targetWidth, Time.unscaledDeltaTime * fillLerpSpeed));
    }

    void ApplyFillWidth(float w)
    {
        var sd = fillMask.sizeDelta;
        sd.x = w;
        fillMask.sizeDelta = sd;
    }

    // BattleUI 에서 시선값 0~100 을 넘겨주면 갱신.
    public void SetGaze(int level)
    {
        level = Mathf.Clamp(level, 0, 100);
        bool increased = level > currentGaze;
        currentGaze = level;
        targetWidth = Mathf.Lerp(0f, fillFullWidth, level / 100f);

        bool wantWhite = level >= 100;
        if (wantWhite != isWhite)
        {
            isWhite = wantWhite;
            Sprite want = wantWhite ? whiteSprite : blackSprite;
            if (frameImage != null && want != null) frameImage.sprite = want;
        }

        if (increased && isActiveAndEnabled) Punch();
    }

    void Punch()
    {
        if (rootRect == null) return;
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(PunchRoutine());
    }

    IEnumerator PunchRoutine()
    {
        float t = 0f;
        while (t < punchDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / punchDuration);
            float s = 1f + (punchScale - 1f) * Mathf.Sin(k * Mathf.PI);
            rootRect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rootRect.localScale = Vector3.one;
        punchCo = null;
    }

    // ── 절차적 스프라이트 ───────────────────────────────────────────
    // 채움 bar: 가로 붉은(중앙)→흰(양끝) + 세로 글로우(중앙 밝고 위아래 페이드).
    Sprite BuildFillGradient()
    {
        int w = 256, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color red = new Color(0.9f, 0.07f, 0.07f);
        Color white = new Color(1f, 0.96f, 0.92f);
        for (int x = 0; x < w; x++)
        {
            float dx = Mathf.Abs((x / (float)(w - 1)) - 0.5f) * 2f; // 0(center)~1(ends)
            Color c = Color.Lerp(red, white, dx);
            for (int y = 0; y < h; y++)
            {
                float dy = Mathf.Abs((y / (float)(h - 1)) - 0.5f) * 2f; // 0(center)~1(top/bottom)
                float a = Mathf.Clamp01(1f - dy * dy); // 세로 소프트 글로우
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // backing: 가로로 긴 어두운 글로우 (중앙 진하고 가장자리 투명).
    Sprite BuildBackingGlow()
    {
        int w = 256, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < w; x++)
        {
            float dx = Mathf.Abs((x / (float)(w - 1)) - 0.5f) * 2f;
            float ax = Mathf.Clamp01(1f - dx * dx * dx);
            for (int y = 0; y < h; y++)
            {
                float dy = Mathf.Abs((y / (float)(h - 1)) - 0.5f) * 2f;
                float ay = Mathf.Clamp01(1f - dy * dy);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.55f * ax * ay));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }
}
