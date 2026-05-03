using UnityEngine;

// 카메라 직교 사이즈와 화면 비율에 따라 스프라이트를 자동으로 스케일/위치 조정해
// 항상 카메라 뷰 전체를 덮게 한다. 몬스터 수에 따른 줌아웃에도 배경이 잘리지 않음.
//
// 사용: BattleScene 의 SpriteRenderer GameObject 에 부착, sortingOrder 를 충분히 낮게(-100 등) 설정.
[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class BattleBackground : MonoBehaviour
{
    [Tooltip("None 이면 Camera.main 자동 사용")]
    public Camera targetCamera;
    [Tooltip("뷰 가장자리 여백 배수 (1 = 정확히 덮음, 1.05 = 5% 여유)")]
    public float padding = 1.05f;

    SpriteRenderer sr;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        Bounds b = sr.sprite.bounds; // 스케일 1 기준 월드 크기 (PPU 반영)
        if (b.size.x <= 0f || b.size.y <= 0f) return;

        float scaleX = (viewW * padding) / b.size.x;
        float scaleY = (viewH * padding) / b.size.y;
        float s = Mathf.Max(scaleX, scaleY); // uniform — 이미지 비율 유지

        transform.localScale = new Vector3(s, s, 1f);

        // 스프라이트 pivot 이 어디든(예: 좌하단) 카메라 중앙에 맞도록 보정.
        Vector3 camPos = cam.transform.position;
        Vector3 spriteCenter = b.center * s;
        transform.position = new Vector3(
            camPos.x - spriteCenter.x,
            camPos.y - spriteCenter.y,
            transform.position.z);
    }
}
