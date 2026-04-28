using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// HP 바 좌측에 붙는 방어도 배지. 0 이면 비활성, >0 이면 활성.
// 몬스터(MonsterRuntimeUI)와 플레이어(BattleUI) 양쪽에서 동일한 시각적 형태로 사용.
// 비활성 → 활성 전환 시 아래에서 슬라이드 + 페이드 인 (StS 스타일).
public class DefenseBadgeUI : MonoBehaviour
{
    private Image bgImage;
    private TextMeshProUGUI valueText;

    private RectTransform rt;
    private CanvasGroup cg;
    private Vector2 finalAnchoredPos;
    private Coroutine slideCoroutine;

    private const float SlideDuration = 0.25f;
    private const float SlideOffsetY = 22f;

    public static DefenseBadgeUI Create(Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, Color bgColor)
    {
        GameObject root = new GameObject("DefenseBadge", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        var rt = (RectTransform)root.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var ui = root.AddComponent<DefenseBadgeUI>();
        ui.rt = rt;
        ui.finalAnchoredPos = anchoredPos;
        ui.cg = root.AddComponent<CanvasGroup>();

        ui.bgImage = root.GetComponent<Image>();
        ui.bgImage.raycastTarget = false;
        ui.bgImage.preserveAspect = true;

        // 등록된 방어 아이콘이 있으면 그 위에 흰색 그대로 그림. 없으면 컬러 사각형 폴백.
        if (BattleIcons.Defense != null)
        {
            ui.bgImage.sprite = BattleIcons.Defense;
            ui.bgImage.color = Color.white;
        }
        else
        {
            ui.bgImage.sprite = null;
            ui.bgImage.color = bgColor;
        }

        // 정중앙 숫자 (아이콘 안쪽, 검정색)
        GameObject txt = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(rt, false);
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        ui.valueText = txt.GetComponent<TextMeshProUGUI>();
        ui.valueText.alignment = TextAlignmentOptions.Center;
        ui.valueText.fontSize = 30;
        ui.valueText.color = Color.black;
        ui.valueText.fontStyle = FontStyles.Bold;
        ui.valueText.fontWeight = FontWeight.Black;
        ui.valueText.raycastTarget = false;

        root.SetActive(false);
        return ui;
    }

    public void SetValue(int defense)
    {
        bool wasActive = gameObject.activeSelf;
        bool show = defense > 0;
        if (wasActive != show) gameObject.SetActive(show);
        if (show && valueText != null) valueText.text = defense.ToString();

        // 비활성 → 활성 전환 시에만 슬라이드 인 (값만 갱신될 때는 애니메이션 X)
        if (show && !wasActive) PlaySlideIn();
    }

    void PlaySlideIn()
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        if (gameObject.activeInHierarchy)
            slideCoroutine = StartCoroutine(SlideInCoroutine());
    }

    IEnumerator SlideInCoroutine()
    {
        if (rt == null) yield break;
        Vector2 startPos = finalAnchoredPos + new Vector2(0f, -SlideOffsetY);

        rt.anchoredPosition = startPos;
        if (cg != null) cg.alpha = 0f;

        float t = 0f;
        while (t < SlideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / SlideDuration);
            rt.anchoredPosition = Vector2.Lerp(startPos, finalAnchoredPos, k);
            if (cg != null) cg.alpha = k;
            yield return null;
        }
        rt.anchoredPosition = finalAnchoredPos;
        if (cg != null) cg.alpha = 1f;
        slideCoroutine = null;
    }
}
