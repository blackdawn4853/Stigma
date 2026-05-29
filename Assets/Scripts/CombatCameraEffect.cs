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
    [Range(0f, 1f)] public float intensityTier1 = 0.1f;
    [Tooltip("6~15 데미지 — 중간 흔들림")]
    [Range(0f, 1f)] public float intensityTier2 = 0.2f;
    [Tooltip("16~25 데미지 — 강한 흔들림")]
    [Range(0f, 1f)] public float intensityTier3 = 0.35f;
    [Tooltip("26+ 데미지 — 매우 강한 흔들림")]
    [Range(0f, 1f)] public float intensityTier4 = 0.5f;

    [Header("히트스톱 (피격/타격 임팩트)")]
    [Tooltip("타격 순간 시간을 멈추는 길이 (초, realtime). 0이면 비활성")]
    [Range(0f, 0.2f)] public float hitStopDuration = 0.06f;

    [Header("플레이어 피격 반응 (넉백 움찔)")]
    [Tooltip("플레이어 피격 시 반응 지속 시간 (초)")]
    [Range(0f, 0.6f)] public float flinchDuration = 0.28f;
    [Tooltip("피격 시 뒤로 밀리는 거리 + 잔떨림 진폭 (월드 단위). 카메라가 멀면 키울 것.")]
    [Range(0f, 1.5f)] public float flinchAmount = 0.5f;

    // ─── 런타임 ─────────────────────────────────────────────────
    Camera cam;
    Coroutine closeupCo;
    Coroutine hitStopCo;
    Coroutine flinchCo;
    Vector3 flinchBase;
    bool flinching;
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
        if (hitStopCo != null) Time.timeScale = 1f;   // 히트스톱 중 파괴되어도 시간 복구
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

    // ─── 몬스터 피격 — 타격받은 몬스터만 쉐이크 ──────────────────
    public void MonsterHitShake(int damage, Monster target = null)
    {
        float intensity = GetIntensityForDamage(damage);
        if (intensity <= 0f) return;
        if (target == null) return;

        var pl = target.GetComponent<ParallaxLayer>();
        if (pl != null) pl.Shake(intensity, shakeDuration, ignoreDepth: true);
    }

    public float GetIntensityForDamage(int damage)
    {
        if (damage <= 0) return 0f;
        if (damage <= 5) return intensityTier1;
        if (damage <= 15) return intensityTier2;
        if (damage <= 25) return intensityTier3;
        return intensityTier4;
    }

    // ─── 히트스톱 — 타격 순간 짧게 시간 정지 (피격·타격 공용) ──────
    public void HitStop() => HitStop(hitStopDuration);

    public void HitStop(float seconds)
    {
        if (seconds <= 0f) return;
        if (hitStopCo != null) StopCoroutine(hitStopCo);
        hitStopCo = StartCoroutine(HitStopRoutine(seconds));
    }

    IEnumerator HitStopRoutine(float seconds)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1f;   // 전투는 항상 정상 속도로 복귀
        hitStopCo = null;
    }

    // ─── 플레이어 피격 움찔 (카메라 줌인 대신 스프라이트만 흔듦) ──────
    public void PlayerFlinch(Transform target)
    {
        if (target == null || flinchDuration <= 0f || flinchAmount <= 0f) return;

        // 플레이어엔 런타임에 ParallaxLayer 가 붙어 매 프레임 localPosition 을 덮어씀.
        // → transform 직접 이동은 무효. ParallaxLayer 의 넉백 오프셋(LateUpdate 합산)으로 처리해야 보임.
        var pl = target.GetComponent<ParallaxLayer>();
        if (pl != null)
        {
            pl.Knockback(Vector2.left, flinchAmount, flinchDuration); // 몬스터(오른쪽) 반대편으로 밀림
            return;
        }

        // 폴백: ParallaxLayer 가 없으면 transform 직접 흔듦.
        if (flinching && flinchCo != null) { StopCoroutine(flinchCo); target.position = flinchBase; }
        flinchBase = target.position;
        flinching = true;
        flinchCo = StartCoroutine(FlinchRoutine(target));
    }

    IEnumerator FlinchRoutine(Transform t)
    {
        // 공격은 몬스터(오른쪽)에서 오므로 왼쪽으로 확 밀렸다가 부드럽게 복귀 + 잔떨림.
        Vector3 knock = new Vector3(-flinchAmount, 0f, 0f);
        float e = 0f;
        while (e < flinchDuration)
        {
            e += Time.deltaTime;   // 히트스톱(timeScale 0) 동안엔 멈췄다가 이어짐
            float back = 1f - Mathf.Clamp01(e / flinchDuration); // 1→0 (복귀)
            Vector2 jit = Random.insideUnitCircle * flinchAmount * 0.45f * back;
            t.position = flinchBase + knock * back + new Vector3(jit.x, jit.y, 0f);
            yield return null;
        }
        t.position = flinchBase;
        flinching = false;
        flinchCo = null;
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
