using UnityEngine;

// 카메라 뷰 사이즈에 따라 스프라이트를 자동으로 스케일/위치 조정해 항상 카메라 뷰 전체를 덮게 한다.
// orthographic 과 perspective 둘 다 지원 (perspective 는 스프라이트 Z 거리 기반 frustum 계산).
//
// 사용: BattleScene 의 SpriteRenderer GameObject 에 부착, sortingOrder 를 충분히 낮게(-100 등) 설정.
//
// DefaultExecutionOrder=100 — 같은 GameObject 의 ParallaxLayer.LateUpdate 가 transform.localPosition 을
// 덮어쓴 뒤 우리가 마지막에 transform.position 으로 카메라 추적 위치를 설정하도록 강제. 이 순서가 어긋나면
// Background 가 카메라를 안 따라가게 되고, 자식 Floor 가 화면상에서 미끄러져 보임.
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public class BattleBackground : MonoBehaviour
{
    [Tooltip("None 이면 Camera.main 자동 사용")]
    public Camera targetCamera;
    [Tooltip("뷰 가장자리 여백 배수 (1 = 정확히 덮음, 1.2 = 20% 여유)")]
    public float padding = 1.2f;
    [Tooltip("1=카메라 완전 추적(화면 정지). 0.85~0.95=배경이 살짝 시차로 움직임(자연스러운 파랄럭스).")]
    [Range(0f, 1f)] public float followFactor = 0.9f;

    SpriteRenderer sr;
    Vector3 initialPosition;
    bool initialCaptured;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        initialPosition = transform.position;
        initialCaptured = true;
    }

    void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        float viewH, viewW;
        if (cam.orthographic)
        {
            viewH = cam.orthographicSize * 2f;
            viewW = viewH * cam.aspect;
        }
        else
        {
            float dist = Mathf.Abs(transform.position.z - cam.transform.position.z);
            if (dist <= 0.01f) return;
            viewH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            viewW = viewH * cam.aspect;
        }

        Bounds b = sr.sprite.bounds; // 스케일 1 기준 월드 크기 (PPU 반영)
        if (b.size.x <= 0f || b.size.y <= 0f) return;

        float scaleX = (viewW * padding) / b.size.x;
        float scaleY = (viewH * padding) / b.size.y;
        float s = Mathf.Max(scaleX, scaleY); // uniform — 이미지 비율 유지

        transform.localScale = new Vector3(s, s, 1f);

        // 스프라이트 pivot 이 어디든(예: 좌하단) 카메라 중앙에 맞도록 보정.
        Vector3 camPos = cam.transform.position;
        Vector3 spriteCenter = b.center * s;
        Vector3 target = new Vector3(
            camPos.x - spriteCenter.x,
            camPos.y - spriteCenter.y,
            transform.position.z);

        if (!initialCaptured) { initialPosition = target; initialCaptured = true; }

        // followFactor=1 → 완전히 카메라 추적(화면 정지). <1 → 살짝 뒤쳐져서 자연스러운 파랄럭스.
        transform.position = Vector3.Lerp(initialPosition, target, followFactor);
    }
}
