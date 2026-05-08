using UnityEngine;

// 마우스 위치에 따라 카메라가 미세하게 이동 — 다키스트 던전 스타일.
// + 모든 ParallaxLayer 에 카메라 오프셋 전파해서 배경 레이어가 깊이에 따라 다른 속도로 따라옴.
// CombatCameraEffect 의 클로즈업 중엔 자동 일시 정지 (카메라 통제권 양보).
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MouseCameraController : MonoBehaviour
{
    public static MouseCameraController Instance { get; private set; }

    [Header("마우스 카메라")]
    [Tooltip("마우스 좌우 끝에서 카메라가 최대 X 만큼 이동 (월드 단위)")]
    public float maxOffsetX = 0.8f;
    [Tooltip("마우스 위아래 끝에서 카메라가 최대 Y 만큼 이동 (월드 단위)")]
    public float maxOffsetY = 0.4f;
    [Tooltip("타겟 위치로 보간하는 속도 (1=느림, 10=빠름). 마우스 정지 시 복귀 속도도 이 값 사용.")]
    [Range(0.5f, 20f)] public float followSpeed = 4f;

    [Header("Parallax 전파")]
    [Tooltip("끄면 카메라만 움직이고 배경 레이어는 안 따라감")]
    public bool driveParallax = true;

    Camera cam;
    Vector3 basePosition;
    Vector3 currentOffset;
    ParallaxLayer[] cachedLayers;

    // BattleManager.Awake 가 호출 — Camera.main 에 자동 부착.
    public static MouseCameraController EnsureForBattle()
    {
        if (Instance != null) return Instance;
        var cam = Camera.main;
        if (cam == null) return null;
        var mc = cam.GetComponent<MouseCameraController>();
        if (mc == null) mc = cam.gameObject.AddComponent<MouseCameraController>();
        return mc;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        cam = GetComponent<Camera>();
        basePosition = transform.position;
        RefreshLayerCache();
        if (driveParallax)
            Debug.Log($"[Parallax] {(cachedLayers != null ? cachedLayers.Length : 0)}개 레이어 등록 완료");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RefreshLayerCache()
    {
#if UNITY_2023_1_OR_NEWER
        cachedLayers = Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None);
#else
        cachedLayers = Object.FindObjectsOfType<ParallaxLayer>();
#endif
    }

    // 카메라 기준 위치 재설정 (씬 이동 / 배틀 재시작 등에서 호출 가능)
    public void ResetBase()
    {
        basePosition = transform.position - currentOffset;
    }

    void LateUpdate()
    {
        // 클로즈업 중이면 카메라 통제권 양보 — 마우스 추적 + Parallax 일시 정지.
        if (CombatCameraEffect.Instance != null && CombatCameraEffect.Instance.IsCloseupActive) return;

        // 마우스 정규화 (-1 ~ 1, 화면 중앙 = 0)
        Vector2 m = Input.mousePosition;
        float nx = Mathf.Clamp((m.x / Mathf.Max(1, Screen.width)) * 2f - 1f, -1f, 1f);
        float ny = Mathf.Clamp((m.y / Mathf.Max(1, Screen.height)) * 2f - 1f, -1f, 1f);

        Vector3 target = new Vector3(nx * maxOffsetX, ny * maxOffsetY, 0f);
        currentOffset = Vector3.Lerp(currentOffset, target, Time.deltaTime * followSpeed);

        // basePosition 기준 오프셋 적용 (Z 는 base 그대로)
        transform.position = new Vector3(
            basePosition.x + currentOffset.x,
            basePosition.y + currentOffset.y,
            basePosition.z);

        // Parallax 레이어에 카메라 오프셋 전파
        if (driveParallax && cachedLayers != null)
        {
            for (int i = 0; i < cachedLayers.Length; i++)
            {
                if (cachedLayers[i] != null)
                    cachedLayers[i].ApplyParallaxOffset(currentOffset);
            }
        }
    }
}
