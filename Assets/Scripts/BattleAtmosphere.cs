using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투씬 분위기 연출 — 아트 없이 절차생성으로 음산한 톤을 입힌다.
// BattleManager.Awake 에서 EnsureForBattle() 자동 부트스트랩 (BottomHudBar 패턴).
// 1) 배경 SpriteRenderer 들을 어둡고 차갑게 곱셈 틴트 (새하얀 하늘 → 음산)
// 2) 맨 뒤 오버레이 캔버스: 비네트 + 상단 암전 + 색 워시 + 떠오르는 불티
// 렌더 파이프라인 무관(Built-in/URP 모두 동작), 클릭 비차단.
public class BattleAtmosphere : MonoBehaviour
{
    public static BattleAtmosphere Instance { get; private set; }

    [Header("카메라 (흰 스카이박스 제거 — 가장 큰 변화)")]
    [Tooltip("카메라 클리어를 스카이박스 → 어두운 솔리드로 바꿔 흰 하늘 제거")]
    public bool overrideCameraClear = true;
    public Color skyColor = new Color(0.05f, 0.045f, 0.075f, 1f);

    [Header("배경 톤다운 (곱셈 틴트 — 1=원본)")]
    [Tooltip("'Background' 하위 모든 SpriteRenderer 에 곱해질 색. 어둡고 차갑게.")]
    public Color backgroundTint = new Color(0.44f, 0.45f, 0.58f, 1f);
    [Tooltip("바닥/배경 이름에 이 단어가 들어가면 틴트 대상. 캐릭터는 제외.")]
    public string backgroundRootName = "Background";

    [Header("오버레이 (카메라/틴트 위에 살짝)")]
    public int canvasSortingOrder = -200;            // 월드 위 / 모든 UI 아래
    [Tooltip("전체 보랏빛 색 워시 (톤 통일)")]
    public Color colorWash = new Color(0.16f, 0.11f, 0.2f, 0.16f);
    [Tooltip("가장자리 비네트 진하기")]
    [Range(0f, 1f)] public float vignetteStrength = 0.7f;
    [Tooltip("상단(하늘) 암전 진하기")]
    [Range(0f, 1f)] public float topDarken = 0.45f;
    [Tooltip("떠오르는 불티 개수")]
    public int emberCount = 10;

    static Sprite _radial, _vignette, _vGradient;

    public static void EnsureForBattle()
    {
        if (Instance != null) return;
        var go = new GameObject("BattleAtmosphere");
        Instance = go.AddComponent<BattleAtmosphere>();
        Instance.Build();
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }
    }

    void Build()
    {
        BuildOverlay();
        // 카메라 클리어 + 배경 틴트는 즉시 + 1프레임 뒤에 한 번 더 적용.
        // (배경 오브젝트들의 Awake 가 색을 흰색으로 덮어쓰는 타이밍을 이기기 위함)
        ApplyWorld();
        StartCoroutine(ApplyWorldNextFrame());
    }

    IEnumerator ApplyWorldNextFrame()
    {
        yield return null;
        ApplyWorld();
    }

    void ApplyWorld()
    {
        TintBackground();   // 절대값 지정이라 여러 번 호출해도 안전(멱등)
        SetCameraDark();
    }

    // ── 흰 스카이박스 제거 ───────────────────────────────────────
    void SetCameraDark()
    {
        if (!overrideCameraClear) return;
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyColor;
    }

    // ── 배경 톤다운 ──────────────────────────────────────────────
    void TintBackground()
    {
        GameObject bg = GameObject.Find(backgroundRootName);
        if (bg == null) return;
        var renderers = bg.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            if (sr == null) continue;
            // 원본이 흰색이든 아니든 동일 결과가 되도록 틴트 색을 직접 지정 (중복 곱셈/덮어쓰기 안전)
            sr.color = new Color(backgroundTint.r, backgroundTint.g, backgroundTint.b, sr.color.a);
        }
    }

    // ── 오버레이 (비네트 + 상단 암전 + 색 워시 + 불티) ───────────
    void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;     // 월드는 덮고, UI(>이 값) 아래라 UI는 크리스프
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 1) 전체 색 워시 (톤 통일)
        var wash = NewImage("ColorWash", colorWash, null);
        Stretch(wash.rectTransform);

        // 2) 상단 암전 그라데이션 (밝은 하늘 죽이기)
        var top = NewImage("TopDarken", new Color(0.02f, 0.015f, 0.04f, topDarken), GetVGradientSprite());
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.45f); trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        // 3) 가장자리 비네트
        var vig = NewImage("Vignette", new Color(0f, 0f, 0f, vignetteStrength), GetVignetteSprite());
        Stretch(vig.rectTransform);

        // 4) 떠오르는 불티 (은은)
        for (int i = 0; i < emberCount; i++)
        {
            var e = NewImage("Ember", new Color(0.85f, 0.55f, 0.3f, 0f), GetRadialSprite());
            float s = Random.Range(5f, 12f);
            e.rectTransform.sizeDelta = new Vector2(s, s);
            StartCoroutine(EmberLoop(e));
        }
    }

    IEnumerator EmberLoop(Image e)
    {
        var rt = e.rectTransform;
        while (e != null)
        {
            float x = Random.Range(-940f, 940f);
            float startY = Random.Range(-560f, -360f);
            float rise = Random.Range(420f, 780f);
            float dur = Random.Range(5f, 9f);
            float sway = Random.Range(18f, 55f);
            float maxA = Random.Range(0.14f, 0.34f);
            float phase = Random.Range(0f, 6.28f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                rt.anchoredPosition = new Vector2(x + Mathf.Sin(phase + k * 6.28f) * sway, startY + rise * k);
                var c = e.color; c.a = Mathf.Sin(k * Mathf.PI) * maxA; e.color = c;
                yield return null;
            }
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    Image NewImage(string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null) img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 절차 스프라이트 ──────────────────────────────────────────
    Sprite GetRadialSprite() { if (_radial == null) _radial = MakeRadial(128); return _radial; }
    Sprite GetVignetteSprite() { if (_vignette == null) _vignette = MakeVignette(128); return _vignette; }
    Sprite GetVGradientSprite() { if (_vGradient == null) _vGradient = MakeVGradient(64); return _vGradient; }

    Sprite MakeRadial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f; var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
            float a = Mathf.Clamp01(1f - d); a *= a;
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite MakeVignette(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f; var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
            float a = Mathf.Clamp01((d - 0.5f) / 0.5f); a *= a;
            px[y * size + x] = new Color(0f, 0f, 0f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // 위=불투명 → 아래=투명 세로 그라데이션
    Sprite MakeVGradient(int size)
    {
        var tex = new Texture2D(1, size, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size];
        for (int y = 0; y < size; y++)
        {
            float k = y / (float)(size - 1);     // 0=아래, 1=위
            px[y] = new Color(1f, 1f, 1f, k * k); // 위로 갈수록 진함
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
