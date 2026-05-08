using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 다키스트 던전 스타일 피격 연출 매니저.
// - PlayerHitCloseup: 빠른 줌인 + 플레이어 sortingOrder 끌어올림 + 살짝 확대 → 짧게 머무르고 복귀.
// - MonsterHitShake: 씬의 ParallaxLayer 들을 데미지 강도별로 흔듦. 카메라 본체는 흔들지 않음.
// BattleScene 진입 시 자동 부트스트랩.
[DisallowMultipleComponent]
public class CombatCameraEffect : MonoBehaviour
{
    public static CombatCameraEffect Instance { get; private set; }

    [Header("카메라 클로즈업 (플레이어 피격용)")]
    [Tooltip("클로즈업 시 카메라 ortho size 비율 (0.6 = 60% 크기로 줌인)")]
    [Range(0.3f, 1f)] public float closeupZoom = 0.55f;
    [Tooltip("클로즈업 진입 시간 (초) — 짧을수록 펀치감 ↑")]
    [Range(0.01f, 0.3f)] public float closeupDuration = 0.05f;
    [Tooltip("클로즈업 후 머무르는 시간 (초)")]
    [Range(0f, 0.5f)] public float holdTime = 0.18f;
    [Tooltip("원래 위치로 복귀하는 시간 (초)")]
    [Range(0.05f, 0.6f)] public float returnDuration = 0.18f;

    [Header("플레이어 타격감 — 스프라이트 위로 튀어나옴")]
    [Tooltip("클로즈업 동안 플레이어 sortingOrder 를 이 값으로 끌어올림 (배경 위로)")]
    public int popSortingOrder = 100;
    [Tooltip("클로즈업 동안 플레이어 스케일 배수 (1=원본)")]
    [Range(1f, 1.4f)] public float popScale = 1.08f;

    [Header("레이어 쉐이크 (몬스터 피격용)")]
    [Tooltip("쉐이크 지속 시간 (초)")]
    [Range(0.05f, 0.6f)] public float shakeDuration = 0.25f;
    [Tooltip("1~5 데미지 — 약한 흔들림 (월드 단위)")]
    [Range(0f, 0.5f)] public float intensityTier1 = 0.05f;
    [Tooltip("6~15 데미지 — 중간 흔들림")]
    [Range(0f, 0.6f)] public float intensityTier2 = 0.13f;
    [Tooltip("16~25 데미지 — 강한 흔들림")]
    [Range(0f, 0.8f)] public float intensityTier3 = 0.25f;
    [Tooltip("26+ 데미지 — 매우 강한 흔들림")]
    [Range(0f, 1f)] public float intensityTier4 = 0.4f;

    // ─── 런타임 ─────────────────────────────────────────────────
    Camera cam;
    Coroutine closeupCo;
    bool closeupActive;
    public bool IsCloseupActive => closeupActive;
    Vector3 snapCamPos;
    float snapOrthoSize;
    SpriteRenderer snapSr;
    int snapSortingOrder;
    Vector3 snapPlayerScale;

    // ─── 부트스트랩 ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "BattleScene" && Instance == null) Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleScene" && Instance == null) Spawn();
    }

    static void Spawn()
    {
        var go = new GameObject("CombatCameraEffect");
        go.AddComponent<CombatCameraEffect>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (closeupActive) RestoreSnapshot();
    }

    Camera ResolveCamera()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.battleCamera != null)
            return BattleManager.Instance.battleCamera;
        return Camera.main;
    }

    // ─── 플레이어 피격 클로즈업 ──────────────────────────────────
    public void PlayerHitCloseup(Transform target, SpriteRenderer sr)
    {
        cam = ResolveCamera();
        if (cam == null || target == null) return;

        if (closeupCo != null)
        {
            StopCoroutine(closeupCo);
            if (closeupActive) RestoreSnapshot();
        }

        TakeSnapshot(sr);
        closeupActive = true;
        closeupCo = StartCoroutine(CloseupRoutine(target));
    }

    void TakeSnapshot(SpriteRenderer sr)
    {
        snapCamPos = cam.transform.position;
        snapOrthoSize = cam.orthographicSize;
        snapSr = sr;
        if (sr != null)
        {
            snapSortingOrder = sr.sortingOrder;
            snapPlayerScale = sr.transform.localScale;
        }
    }

    void RestoreSnapshot()
    {
        if (cam != null)
        {
            cam.transform.position = snapCamPos;
            cam.orthographicSize = snapOrthoSize;
        }
        if (snapSr != null)
        {
            snapSr.sortingOrder = snapSortingOrder;
            snapSr.transform.localScale = snapPlayerScale;
        }
        closeupActive = false;
    }

    IEnumerator CloseupRoutine(Transform target)
    {
        Vector3 startPos = snapCamPos;
        float startSize = snapOrthoSize;
        Vector3 endPos = new Vector3(target.position.x, target.position.y, startPos.z);
        float endSize = startSize * closeupZoom;

        // 스프라이트 pop — sortingOrder 최상단으로, 살짝 확대
        if (snapSr != null)
        {
            snapSr.sortingOrder = popSortingOrder;
            snapSr.transform.localScale = snapPlayerScale * popScale;
        }

        // 줌인 (snappy)
        yield return Lerp(closeupDuration, k =>
        {
            cam.transform.position = Vector3.Lerp(startPos, endPos, k);
            cam.orthographicSize = Mathf.Lerp(startSize, endSize, k);
        });

        // 짧게 머무름
        if (holdTime > 0f) yield return new WaitForSeconds(holdTime);

        // 복귀
        yield return Lerp(returnDuration, k =>
        {
            cam.transform.position = Vector3.Lerp(endPos, startPos, k);
            cam.orthographicSize = Mathf.Lerp(endSize, startSize, k);
        });

        RestoreSnapshot();
        closeupCo = null;
    }

    // ─── 몬스터 피격 — 배경 레이어 쉐이크 ────────────────────────
    public void MonsterHitShake(int damage)
    {
        float intensity = GetIntensityForDamage(damage);
        if (intensity <= 0f) return;
        var layers = FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None);
        for (int i = 0; i < layers.Length; i++)
            layers[i].Shake(intensity, shakeDuration);
    }

    public float GetIntensityForDamage(int damage)
    {
        if (damage <= 0) return 0f;
        if (damage <= 5) return intensityTier1;
        if (damage <= 15) return intensityTier2;
        if (damage <= 25) return intensityTier3;
        return intensityTier4;
    }

    static float SmoothStep01(float t) => t * t * (3f - 2f * t);

    static IEnumerator Lerp(float duration, System.Action<float> step)
    {
        if (duration <= 0f) { step(1f); yield break; }
        float e = 0f;
        while (e < duration)
        {
            step(SmoothStep01(Mathf.Clamp01(e / duration)));
            e += Time.deltaTime;
            yield return null;
        }
        step(1f);
    }
}
