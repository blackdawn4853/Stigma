using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

// 전투 임팩트 효과 — 카메라 쉐이크 + 데미지 팝업.
// BattleScene 진입 시 자동 부트스트랩.
// 호출: BattleManager.DamagePlayer / Monster.TakeDamage 에서 트리거.
public class CombatEffectsManager : MonoBehaviour
{
    public static CombatEffectsManager Instance { get; private set; }

    [Header("카메라 쉐이크")]
    [Tooltip("이 값 이상의 피해를 플레이어가 받으면 쉐이크 발동.")]
    public int shakeDamageThreshold = 20;
    [Tooltip("쉐이크 지속 시간 (초).")]
    public float shakeDuration = 0.25f;
    [Tooltip("데미지당 쉐이크 강도 (월드 단위). damage*이 값 = intensity.")]
    public float shakeIntensityPerDamage = 0.025f;
    [Tooltip("쉐이크 강도 상한.")]
    public float maxShakeIntensity = 0.6f;

    [Header("데미지 팝업")]
    [Tooltip("팝업 폰트 크기 (월드 단위). 슬더스 느낌으로 크게.")]
    public float popupFontSize = 11f;
    [Tooltip("팝업이 떠오르는 거리 (월드 단위).")]
    public float popupRiseHeight = 1.2f;
    [Tooltip("팝업 표시 시간 (초). 이 시간동안 페이드 아웃.")]
    public float popupDuration = 0.8f;
    [Tooltip("팝업 시작 위치 랜덤 흩뿌리기 반경 (다중 히트 겹침 방지).")]
    public float popupSpawnJitter = 0.3f;
    [Tooltip("팝업 시작 위치 오프셋 (캐릭터 기준). 위쪽 또는 앞쪽에 띄우려면 (0, 1.5, 0) 같이.")]
    public Vector3 popupSpawnOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("팝업 색상 (글자 안쪽).")]
    public Color popupColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("팝업 외곽선 색.")]
    public Color popupOutlineColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("팝업 외곽선 두께 (0~1). 두껍게 잡으면 슬더스 스타일 검정 테두리.")]
    [Range(0f, 1f)]
    public float popupOutlineWidth = 0.45f;
    [Tooltip("팝업 폰트 두께 추가 (faux bold). 0 = Bold 만, 양수 = 더 굵게.")]
    [Range(-1f, 1f)]
    public float popupFontWeightBoost = 0.5f;
    [Tooltip("팝업 sorting order — 캐릭터 sprite 보다 위로.")]
    public int popupSortingOrder = 200;
    [Tooltip("팝업 폰트 (비어있으면 TMP_Settings.defaultFontAsset 사용).")]
    public TMP_FontAsset popupFont;

    // ─── 런타임 ──────────────────────────────────────────────────────
    private Coroutine shakeCoroutine;
    private Camera shakeCamera;
    private Vector3 shakeOrigin;
    private bool shakeOriginSaved;

    // ─── 부트스트랩 ──────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "BattleScene" && Instance == null)
            Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleScene" && Instance == null)
            Spawn();
    }

    static void Spawn()
    {
        GameObject go = new GameObject("CombatEffectsManager");
        go.AddComponent<CombatEffectsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 쉐이크 중에 씬 전환되면 카메라 위치 복원
        if (shakeCamera != null && shakeOriginSaved)
            shakeCamera.transform.localPosition = shakeOrigin;
    }

    // ─── 카메라 쉐이크 ───────────────────────────────────────────────
    public void ShakeCamera(int damage)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        // 기존 쉐이크 중이면 중단하고 origin 으로 복원 후 새로 시작
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            if (shakeOriginSaved && shakeCamera != null)
                shakeCamera.transform.localPosition = shakeOrigin;
        }

        // 카메라 origin 저장 (최초 1회 또는 카메라 바뀐 경우)
        if (!shakeOriginSaved || shakeCamera != cam)
        {
            shakeCamera = cam;
            shakeOrigin = cam.transform.localPosition;
            shakeOriginSaved = true;
        }

        float intensity = Mathf.Min(damage * shakeIntensityPerDamage, maxShakeIntensity);
        shakeCoroutine = StartCoroutine(ShakeCoroutine(cam, intensity, shakeDuration));
    }

    Camera ResolveCamera()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.battleCamera != null)
            return BattleManager.Instance.battleCamera;
        return Camera.main;
    }

    IEnumerator ShakeCoroutine(Camera cam, float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && cam != null)
        {
            elapsed += Time.deltaTime;
            float falloff = 1f - (elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * intensity * falloff;
            cam.transform.localPosition = shakeOrigin + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
        if (cam != null) cam.transform.localPosition = shakeOrigin;
        shakeCoroutine = null;
    }

    // ─── 데미지 팝업 ─────────────────────────────────────────────────
    public void ShowDamagePopup(Vector3 worldPos, int amount)
    {
        ShowDamagePopup(worldPos, amount, popupColor);
    }

    public void ShowDamagePopup(Vector3 worldPos, int amount, Color color)
    {
        if (amount <= 0) return;

        GameObject go = new GameObject($"DmgPopup_{amount}");
        Vector2 jitter = Random.insideUnitCircle * popupSpawnJitter;
        go.transform.position = worldPos + popupSpawnOffset + new Vector3(jitter.x, jitter.y, 0f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = amount.ToString();
        tmp.fontSize = popupFontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        if (popupFont != null) tmp.font = popupFont;

        // faux bold 추가 — 글자 형태 자체를 두껍게 (Bold 스타일 위에 추가)
        // characterSpacing / fontWeight 는 폰트 asset 의 weight variant 가 있어야 작동.
        // material 의 _FaceDilate 로 굵기 조절 (모든 TMP 폰트에서 작동).
        if (popupFontWeightBoost != 0f && tmp.fontMaterial != null)
        {
            // material 인스턴스 분리 (다른 TMP 와 공유 안 되게)
            Material mat = new Material(tmp.fontMaterial);
            mat.SetFloat("_FaceDilate", popupFontWeightBoost);
            tmp.fontMaterial = mat;
        }

        // 외곽선 (가독성 + 슬더스 스타일 검정 테두리)
        if (popupOutlineWidth > 0f)
        {
            tmp.outlineColor = popupOutlineColor;
            tmp.outlineWidth = popupOutlineWidth;
        }

        // sortingOrder — TextMeshPro 의 MeshRenderer 에 설정
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = popupSortingOrder;
            mr.sortingLayerID = 0; // Default layer
        }

        StartCoroutine(AnimatePopup(go, tmp, color));
    }

    IEnumerator AnimatePopup(GameObject go, TextMeshPro tmp, Color baseColor)
    {
        Vector3 start = go.transform.position;
        Vector3 end = start + new Vector3(0f, popupRiseHeight, 0f);
        float elapsed = 0f;
        while (elapsed < popupDuration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupDuration;
            go.transform.position = Vector3.Lerp(start, end, t);
            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            if (tmp != null) tmp.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
