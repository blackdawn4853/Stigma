using System.Collections;
using UnityEngine;

// 배경 레이어 — 카메라 마우스 추적 오프셋과 피격 쉐이크 둘 다 적용.
// 한 LateUpdate 에서 originLocal + parallaxOffset + shakeOffset 으로 합쳐서 transform 갱신.
[DisallowMultipleComponent]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax 설정")]
    [Tooltip("0 = 거의 고정 (멀리 있는 배경), 1 = 카메라와 같이 이동 (가장 가까운 레이어). 카메라 오프셋 × 이 값 만큼 레이어가 함께 움직임.")]
    [Range(0f, 1f)] public float parallaxFactor = 0.5f;

    [Header("쉐이크 보정 (CombatCameraEffect 호출)")]
    [Tooltip("0 = 가장 먼 배경(약함), 1 = 가장 앞 레이어(강함)")]
    [Range(0f, 1f)] public float depth = 0.5f;
    [Tooltip("이 레이어만 쉐이크를 더 강하게/약하게 보정 (1=기본)")]
    [Range(0f, 3f)] public float intensityMultiplier = 1f;

    [Header("활성")]
    [Tooltip("끄면 transform 을 건드리지 않음 — 인트로 슬라이드 등 외부 통제 중일 때 일시 비활성.")]
    public bool active = true;

    Vector3 originLocal;
    bool originCaptured;
    Coroutine shakeCo;
    Vector3 shakeOffset;
    Vector3 parallaxOffset;

    void Awake() { CaptureOrigin(); }

    void CaptureOrigin()
    {
        originLocal = transform.localPosition;
        originCaptured = true;
    }

    // 외부에서 호출 — 현재 위치를 새 origin 으로 잡고 즉시 활성.
    public void EngageNow()
    {
        CaptureOrigin();
        active = true;
    }

    void LateUpdate()
    {
        if (!active) return;
        if (!originCaptured) CaptureOrigin();
        transform.localPosition = originLocal + parallaxOffset + shakeOffset;
    }

    // MouseCameraController 가 매 프레임 호출 — 카메라 오프셋을 받아 자기 factor 만큼 적용.
    public void ApplyParallaxOffset(Vector3 cameraOffset)
    {
        parallaxOffset = cameraOffset * parallaxFactor;
    }

    public void Shake(float baseIntensity, float duration, bool ignoreDepth = false)
    {
        if (!originCaptured) CaptureOrigin();
        if (shakeCo != null)
        {
            StopCoroutine(shakeCo);
            shakeOffset = Vector3.zero;
        }
        shakeCo = StartCoroutine(ShakeRoutine(baseIntensity, duration, ignoreDepth));
    }

    IEnumerator ShakeRoutine(float baseIntensity, float duration, bool ignoreDepth)
    {
        float amp = ignoreDepth ? baseIntensity * intensityMultiplier
                                : baseIntensity * depth * intensityMultiplier;
        if (amp <= 0f || duration <= 0f) { shakeOffset = Vector3.zero; shakeCo = null; yield break; }

        float t = 0f;
        while (t < duration)
        {
            float decay = 1f - (t / duration);
            Vector2 r = Random.insideUnitCircle * amp * decay;
            shakeOffset = new Vector3(r.x, r.y, 0f);
            t += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
        shakeCo = null;
    }
}
