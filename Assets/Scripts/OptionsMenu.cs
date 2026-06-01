using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// MainMenu 의 Options_Button 을 런타임에 와이어링해 옵션 패널을 띄운다.
// 기능: ① 마스터 볼륨 슬라이더, ② 음소거 토글, ③ 전체화면 토글.
// UI 는 전부 코드 절차생성(프로젝트 관례). 별도 GameObject 배치 없이
// [RuntimeInitializeOnLoadMethod] 로 자동 동작한다.
//
// 사운드 에셋이 아직 없어도 AudioListener.volume / PlayerPrefs 값은 정상 저장·적용되며,
// 추후 BGM/SFX 를 추가하면 그대로 마스터 볼륨이 먹는다.
public static class OptionsMenu
{
    const string K_VOL = "opt_masterVolume"; // 0~1
    const string K_MUTE = "opt_muted";       // 0/1
    const string K_FULL = "opt_fullscreen";  // 0/1

    static GameObject canvasRoot;            // 옵션 전용 캔버스(씬 전환 시 파괴됨)
    static CanvasGroup group;
    static OptionsRunner runner;
    static Slider volumeSlider;
    static TMP_Text muteLabel;
    static Image muteBtnImg;
    static TMP_Text fullLabel;
    static Image fullBtnImg;
    static TMP_FontAsset font;

    // ── 저장값 접근 ─────────────────────────────────────────────
    static float SavedVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(K_VOL, 0.8f));
    static bool SavedMuted => PlayerPrefs.GetInt(K_MUTE, 0) == 1;

    // ── 부팅 시: 저장된 오디오/화면 설정 적용 (전 씬 공통) ───────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedSettings()
    {
        AudioListener.volume = SavedMuted ? 0f : SavedVolume;
        if (PlayerPrefs.HasKey(K_FULL))
            Screen.fullScreen = PlayerPrefs.GetInt(K_FULL, 0) == 1;
    }

    // ── 씬 로드 훅: MainMenu 에서 Options_Button 연결 ────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "MainMenu") WireButton();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 이전 패널 참조는 파괴되므로 초기화
        canvasRoot = null; group = null; runner = null;
        if (scene.name == "MainMenu") WireButton();
    }

    static void WireButton()
    {
#if UNITY_2023_1_OR_NEWER
        var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
#else
        var buttons = Object.FindObjectsOfType<Button>();
#endif
        Button optBtn = null;
        foreach (var b in buttons)
            if (b.name == "Options_Button") { optBtn = b; break; }

        if (optBtn == null) { Debug.LogWarning("[OptionsMenu] Options_Button 못찾음"); return; }

        optBtn.onClick.RemoveAllListeners();
        optBtn.onClick.AddListener(Open);
        Debug.Log("[OptionsMenu] Options_Button 연결됨");
    }

    // ── 열기/닫기 ───────────────────────────────────────────────
    static void Open()
    {
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true);
        SyncUI();
        runner.StopAllCoroutines();
        runner.StartCoroutine(Fade(0f, 1f, 0.18f, null));
    }

    static void Close()
    {
        if (canvasRoot == null) return;
        runner.StopAllCoroutines();
        runner.StartCoroutine(Fade(group.alpha, 0f, 0.15f, () => canvasRoot.SetActive(false)));
    }

    static IEnumerator Fade(float from, float to, float dur, System.Action done)
    {
        group.interactable = false; group.blocksRaycasts = true;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
        group.interactable = to > 0.5f;
        group.blocksRaycasts = to > 0.5f;
        done?.Invoke();
    }

    // ── 패널 절차생성 ──────────────────────────────────────────
    static void Build()
    {
        font = FindSceneFont();

        var go = new GameObject("OptionsCanvas", typeof(Canvas), typeof(CanvasScaler),
                                typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasRoot = go;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // 메인메뉴 UI 위에
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        runner = go.AddComponent<OptionsRunner>();

        // 딤 배경(클릭 시 닫기)
        var dim = NewImage(go.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        // 중앙 카드
        var panel = NewImage(go.transform, "Panel", new Color(0.07f, 0.05f, 0.09f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 420f);
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // 카드 클릭은 닫기 전파 차단
        Border(prt, new Color(0.45f, 0.42f, 0.40f, 0.8f));

        // 제목
        var title = NewText(prt, "설정", 46, FontStyles.Bold,
                            new Color(0.92f, 0.88f, 0.84f, 1f), new Vector2(0f, 150f), new Vector2(560f, 60f));
        Divider(prt, new Vector2(0f, 116f), 520f);

        // ── 행 1: 음량 ──
        NewText(prt, "음량", 30, FontStyles.Normal, new Color(0.85f, 0.82f, 0.8f, 1f),
                new Vector2(-220f, 55f), new Vector2(160f, 50f)).alignment = TextAlignmentOptions.Left;
        volumeSlider = BuildSlider(prt, new Vector2(20f, 55f), new Vector2(300f, 30f));
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        // 음소거 네모 버튼
        var (mb, ml, mi) = NewButton(prt, "ON", new Vector2(245f, 55f), new Vector2(64f, 48f));
        muteLabel = ml; muteBtnImg = mi;
        mb.onClick.AddListener(ToggleMute);

        // ── 행 2: 전체화면 ──
        NewText(prt, "전체화면", 30, FontStyles.Normal, new Color(0.85f, 0.82f, 0.8f, 1f),
                new Vector2(-180f, -30f), new Vector2(240f, 50f)).alignment = TextAlignmentOptions.Left;
        var (fb, fl, fi) = NewButton(prt, "OFF", new Vector2(200f, -30f), new Vector2(110f, 48f));
        fullLabel = fl; fullBtnImg = fi;
        fb.onClick.AddListener(ToggleFullscreen);

        // 닫기 버튼
        var (cb, cl, ci) = NewButton(prt, "닫기", new Vector2(0f, -150f), new Vector2(200f, 56f));
        ci.color = new Color(0.5f, 0.12f, 0.12f, 0.95f);
        cb.onClick.AddListener(Close);

        // 우상단 X
        var (xb, xl, xi) = NewButton(prt, "X", new Vector2(288f, 178f), new Vector2(44f, 44f));
        xi.color = new Color(0.5f, 0.12f, 0.12f, 0.9f);
        xb.onClick.AddListener(Close);
    }

    // 저장값을 UI 에 반영
    static void SyncUI()
    {
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(SavedVolume);
        }
        RefreshMuteVisual();
        RefreshFullscreenVisual();
    }

    // ── 콜백 ────────────────────────────────────────────────────
    static void OnVolumeChanged(float v)
    {
        PlayerPrefs.SetFloat(K_VOL, v);
        PlayerPrefs.Save();
        if (!SavedMuted) AudioListener.volume = v; // 음소거 중이면 적용 보류(해제 시 복원)
    }

    static void ToggleMute()
    {
        bool muted = !SavedMuted;
        PlayerPrefs.SetInt(K_MUTE, muted ? 1 : 0);
        PlayerPrefs.Save();
        AudioListener.volume = muted ? 0f : SavedVolume;
        RefreshMuteVisual();
    }

    static void ToggleFullscreen()
    {
        bool full = !Screen.fullScreen;
        Screen.fullScreen = full;
        PlayerPrefs.SetInt(K_FULL, full ? 1 : 0);
        PlayerPrefs.Save();
        RefreshFullscreenVisual();
    }

    static void RefreshMuteVisual()
    {
        if (muteLabel == null) return;
        bool muted = SavedMuted;
        muteLabel.text = muted ? "OFF" : "ON";
        muteBtnImg.color = muted ? new Color(0.45f, 0.12f, 0.12f, 0.95f)  // 음소거: 적색
                                  : new Color(0.16f, 0.4f, 0.2f, 0.95f);  // 켜짐: 녹색
    }

    static void RefreshFullscreenVisual()
    {
        if (fullLabel == null) return;
        bool full = Screen.fullScreen;
        fullLabel.text = full ? "ON" : "OFF";
        fullBtnImg.color = full ? new Color(0.16f, 0.4f, 0.2f, 0.95f)
                                : new Color(0.22f, 0.2f, 0.24f, 0.95f);
    }

    // ── UI 빌더 헬퍼 ────────────────────────────────────────────
    static TMP_FontAsset FindSceneFont()
    {
#if UNITY_2023_1_OR_NEWER
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
#else
        var texts = Object.FindObjectsOfType<TMP_Text>();
#endif
        foreach (var t in texts) if (t.font != null) return t.font;
        return null;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI NewText(Transform parent, string content, int size, FontStyles style,
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

    static (Button, TMP_Text, Image) NewButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.2f, 0.24f, 0.95f);
        var btn = go.GetComponent<Button>();
        Border(rt, new Color(0.45f, 0.42f, 0.40f, 0.7f));
        var txt = NewText(rt, label, 26, FontStyles.Bold, new Color(0.95f, 0.93f, 0.9f, 1f),
                          Vector2.zero, size);
        return (btn, txt, img);
    }

    static Slider BuildSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("VolumeSlider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var slider = go.AddComponent<Slider>();

        // 배경 바
        var bg = NewImage(go.transform, "Background", new Color(0.16f, 0.16f, 0.2f, 1f));
        var brt = bg.rectTransform;
        brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(0f, 8f);

        // Fill Area > Fill
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fart = fillArea.GetComponent<RectTransform>();
        fart.anchorMin = new Vector2(0f, 0.5f); fart.anchorMax = new Vector2(1f, 0.5f);
        fart.pivot = new Vector2(0.5f, 0.5f);
        fart.sizeDelta = new Vector2(-20f, 8f);
        var fill = NewImage(fillArea.transform, "Fill", new Color(0.72f, 0.22f, 0.22f, 1f));
        fill.rectTransform.sizeDelta = Vector2.zero;
        fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

        // Handle Slide Area > Handle
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var hart = handleArea.GetComponent<RectTransform>();
        hart.anchorMin = new Vector2(0f, 0f); hart.anchorMax = new Vector2(1f, 1f);
        hart.offsetMin = new Vector2(10f, 0f); hart.offsetMax = new Vector2(-10f, 0f);
        var handle = NewImage(handleArea.transform, "Handle", new Color(0.92f, 0.88f, 0.82f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.value = SavedVolume;
        return slider;
    }

    static void Border(RectTransform target, Color color)
    {
        Edge(target, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f));
        Edge(target, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f));
    }

    static void Edge(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 size)
    {
        var img = NewImage(parent, "Edge", color);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    static void Divider(Transform parent, Vector2 pos, float width)
    {
        var img = NewImage(parent, "Divider", new Color(0.4f, 0.38f, 0.42f, 0.45f));
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 2f);
    }
}

// 정적 클래스가 코루틴을 돌리기 위한 최소 MonoBehaviour 러너.
public class OptionsRunner : MonoBehaviour { }
