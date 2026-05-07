using System.Collections;
using UnityEngine;

// 배경 레이어 — 깊이값(depth)에 따라 흔들림 강도가 비례.
// 0 = 가장 먼 배경(약함), 1 = 가장 앞 레이어(강함).
// CombatCameraEffect.MonsterHitShake() 가 모든 ParallaxLayer 를 찾아서 일괄 호출.
[DisallowMultipleComponent]
public class ParallaxLayer : MonoBehaviour
{
    [Header("깊이")]
    [Tooltip("0 = 가장 먼 배경(약함), 1 = 가장 앞 레이어(강함)")]
    [Range(0f, 1f)]
    public float depth = 0.5f;

    [Header("쉐이크 보정")]
    [Tooltip("이 레이어만 쉐이크를 더 강하게/약하게 보정 (1=기본)")]
    [Range(0f, 3f)]
    public float intensityMultiplier = 1f;

    Vector3 originLocal;
    bool originCaptured;
    Coroutine shakeCo;

    void Awake() { CaptureOrigin(); }

    void CaptureOrigin()
    {
        originLocal = transform.localPosition;
        originCaptured = true;
    }

    public void Shake(float baseIntensity, float duration)
    {
        if (!originCaptured) CaptureOrigin();
        if (shakeCo != null)
        {
            StopCoroutine(shakeCo);
            transform.localPosition = originLocal;
        }
        shakeCo = StartCoroutine(ShakeRoutine(baseIntensity, duration));
    }

    IEnumerator ShakeRoutine(float baseIntensity, float duration)
    {
        float amp = baseIntensity * depth * intensityMultiplier;
        if (amp <= 0f || duration <= 0f) { transform.localPosition = originLocal; shakeCo = null; yield break; }

        float t = 0f;
        while (t < duration)
        {
            float decay = 1f - (t / duration);
            Vector2 r = Random.insideUnitCircle * amp * decay;
            transform.localPosition = originLocal + new Vector3(r.x, r.y, 0f);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originLocal;
        shakeCo = null;
    }
}
