using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// 캐릭터 몸 한가운데 큰 반투명 방패 — 방어도 얻을 때만 순간 플래시 (슬더스 스타일).
// 영구 표시가 아니라 슬라이드인 + 페이드인 → 잠깐 유지 → 페이드아웃 후 사라짐.
// 숫자는 표시 안 함 (작은 배지가 별도로 보여줌).
// 자체 월드 스페이스 캔버스 — 캐릭터 위치 따라가며 LateUpdate 로 갱신.
// sortingOrder 로 캐릭터 sprite 앞으로 표시.
public class BodyDefenseUI : MonoBehaviour
{
    public Transform target;
    [Tooltip("캐릭터 발 기준 월드 오프셋 (몸 중앙 위치).")]
    public Vector3 worldOffset = new Vector3(0f, 0.9f, 0f);

    [Header("캔버스")]
    public Vector2 canvasSize = new Vector2(3f, 3f);
    public float canvasScale = 0.01f;
    [Tooltip("캐릭터 sprite 보다 위에 보이도록 충분히 큰 값. 보통 100 이상.")]
    public int sortingOrder = 100;

    [Header("방패 아이콘")]
    [Tooltip("방패 크기 (px in canvas-local).")]
    public Vector2 shieldSize = new Vector2(220f, 220f);
    [Tooltip("플래시 정점 투명도 (0~1). 흐릿하게.")]
    [Range(0f, 1f)] public float shieldPeakAlpha = 0.6f;
    [Tooltip("BattleIcons.Defense 가 없을 때 폴백 색.")]
    public Color shieldFallbackColor = new Color(0.4f, 0.75f, 1f, 1f);
    [Tooltip("좌우 반전 — 방패의 막는 면이 공격 오는 쪽을 향하게. 플레이어(왼쪽)는 true 로 오른쪽(몬스터)을 막음.")]
    public bool flipX = false;

    [Header("플래시 애니메이션")]
    [Tooltip("아래에서 위로 올라오는 거리.")]
    public float slideOffsetY = 60f;
    [Tooltip("슬라이드인 + 페이드인 시간.")]
    public float fadeInDuration = 0.18f;
    [Tooltip("정점 alpha 에서 머무르는 시간 (0이면 바로 페이드아웃).")]
    public float holdDuration = 0.08f;
    [Tooltip("페이드아웃 시간.")]
    public float fadeOutDuration = 0.32f;

    // 런타임
    private Image shieldImage;
    private RectTransform shieldRT;
    private CanvasGroup shieldCG;
    private Vector2 shieldFinalPos;
    private Coroutine animCo;
    private int lastDefense = 0;

    public static BodyDefenseUI CreateFor(Transform target, Vector3 worldOffset, bool flipX = false)
    {
        if (target == null) return null;
        GameObject go = new GameObject("BodyDefenseUI");
        // 부모 follow 안 함 — localScale 영향 받지 않게 별도 루트 + LateUpdate 추적
        go.transform.SetParent(null);
        go.transform.localScale = Vector3.one;

        var ui = go.AddComponent<BodyDefenseUI>();
        ui.target = target;
        ui.worldOffset = worldOffset;
        ui.flipX = flipX;
        ui.Build();
        return ui;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder; // 캐릭터 sprite 위로
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(canvasSize.x / canvasScale, canvasSize.y / canvasScale);
        rt.localScale = Vector3.one * canvasScale;

        // 방패 아이콘
        GameObject shieldGO = new GameObject("Shield",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        shieldGO.transform.SetParent(rt, false);
        shieldRT = (RectTransform)shieldGO.transform;
        shieldRT.anchorMin = new Vector2(0.5f, 0.5f);
        shieldRT.anchorMax = new Vector2(0.5f, 0.5f);
        shieldRT.pivot = new Vector2(0.5f, 0.5f);
        shieldRT.sizeDelta = shieldSize;
        shieldRT.anchoredPosition = Vector2.zero;
        shieldFinalPos = Vector2.zero;
        // 막는 면이 공격 오는 쪽을 보도록 좌우 반전 (자식 스케일만 — root 는 LateUpdate 가 카메라로 회전)
        shieldRT.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);

        shieldImage = shieldGO.GetComponent<Image>();
        shieldImage.raycastTarget = false;
        shieldImage.preserveAspect = true;
        if (BattleIcons.Defense != null)
        {
            shieldImage.sprite = BattleIcons.Defense;
            shieldImage.color = Color.white;
        }
        else
        {
            shieldImage.sprite = null;
            shieldImage.color = shieldFallbackColor;
        }

        shieldCG = shieldGO.GetComponent<CanvasGroup>();
        shieldCG.alpha = 0f;

        gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + worldOffset;
        if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
    }

    public void SetValue(int defense)
    {
        // 방어도가 늘어났을 때만 플래시 (감소/유지 시 무동작)
        if (defense > lastDefense)
            PlayFlash();
        lastDefense = defense;
    }

    // Build 이후에도 동적으로 방패 크기 조정 (PlayerHpBarUI 등 외부 인스펙터 슬롯에서 호출).
    public void SetShieldSize(Vector2 size)
    {
        shieldSize = size;
        if (shieldRT != null) shieldRT.sizeDelta = size;
    }

    void PlayFlash()
    {
        if (animCo != null) StopCoroutine(animCo);
        if (gameObject.activeInHierarchy)
            animCo = StartCoroutine(FlashCoroutine());
    }

    IEnumerator FlashCoroutine()
    {
        Vector2 startPos = shieldFinalPos + new Vector2(0f, -slideOffsetY);
        shieldRT.anchoredPosition = startPos;
        shieldCG.alpha = 0f;

        // Phase 1: 슬라이드인 + 페이드인
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / fadeInDuration);
            shieldRT.anchoredPosition = Vector2.Lerp(startPos, shieldFinalPos, k);
            shieldCG.alpha = k * shieldPeakAlpha;
            yield return null;
        }
        shieldRT.anchoredPosition = shieldFinalPos;
        shieldCG.alpha = shieldPeakAlpha;

        // Phase 2: 잠깐 유지
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        // Phase 3: 페이드아웃 (위치는 그대로)
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / fadeOutDuration);
            shieldCG.alpha = shieldPeakAlpha * k;
            yield return null;
        }
        shieldCG.alpha = 0f;
        animCo = null;
    }
}
