using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

// 단일 버프/디버프 아이콘. StatusIconBar 가 동적으로 생성한다.
// 마우스 호버 시 StatusTooltip 을 띄운다.
//
// 구조:
//   - 외부 (Root): HorizontalLayoutGroup 의 슬롯. 위치/크기 고정.
//   - 내부 (Content): bg/fg/count 보유. 슬라이드 인 애니메이션 대상.
// 레이아웃 그룹이 외부의 anchoredPosition 을 관리하므로, 내부를 별도 분리해 애니메이트.
public class StatusIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public StatusType type;
    public int value;
    public int turns;

    private Image bgImage;
    private Image fgImage;
    private TextMeshProUGUI countText;

    private RectTransform contentRt;
    private CanvasGroup contentCg;
    private Coroutine slideCoroutine;

    private const float SlideDuration = 0.25f;
    private const float SlideOffsetY = 20f;

    public static StatusIconUI Create(Transform parent, Vector2 size)
    {
        // 외부 슬롯 (HorizontalLayoutGroup 가 위치 결정)
        GameObject outer = new GameObject("StatusIcon", typeof(RectTransform));
        outer.transform.SetParent(parent, false);
        var outerRt = (RectTransform)outer.transform;
        outerRt.sizeDelta = size;

        var ui = outer.AddComponent<StatusIconUI>();

        // 내부 컨테이너 (애니메이션 대상)
        GameObject inner = new GameObject("Content", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(outerRt, false);
        ui.contentRt = (RectTransform)inner.transform;
        ui.contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        ui.contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        ui.contentRt.pivot = new Vector2(0.5f, 0.5f);
        ui.contentRt.sizeDelta = size;
        ui.contentRt.anchoredPosition = Vector2.zero;

        ui.contentCg = inner.AddComponent<CanvasGroup>();

        ui.bgImage = inner.GetComponent<Image>();
        ui.bgImage.raycastTarget = true; // 호버 감지용

        // 전경 아이콘 (스프라이트)
        GameObject fg = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        fg.transform.SetParent(ui.contentRt, false);
        var fgRt = (RectTransform)fg.transform;
        fgRt.anchorMin = Vector2.zero;
        fgRt.anchorMax = Vector2.one;
        fgRt.offsetMin = new Vector2(2f, 2f);
        fgRt.offsetMax = new Vector2(-2f, -2f);
        ui.fgImage = fg.GetComponent<Image>();
        ui.fgImage.raycastTarget = false;
        ui.fgImage.preserveAspect = true;

        // 우하단 카운트 텍스트
        GameObject txt = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(ui.contentRt, false);
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(0f, 0f);
        txtRt.offsetMax = new Vector2(-2f, -2f);
        ui.countText = txt.GetComponent<TextMeshProUGUI>();
        ui.countText.alignment = TextAlignmentOptions.BottomRight;
        ui.countText.fontSize = 12;
        ui.countText.color = Color.white;
        ui.countText.fontStyle = FontStyles.Bold;
        ui.countText.raycastTarget = false;
        ui.countText.outlineWidth = 0.25f;
        ui.countText.outlineColor = Color.black;

        return ui;
    }

    public void Setup(StatusType t, int val, int turn)
    {
        type = t;
        value = val;
        turns = turn;

        var info = StatusInfo.Get(t);
        var sprite = StatusInfo.GetSprite(t);

        if (sprite != null)
        {
            fgImage.sprite = sprite;
            fgImage.color = Color.white;
            bgImage.sprite = null;
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
        }
        else
        {
            fgImage.sprite = null;
            fgImage.color = new Color(0f, 0f, 0f, 0f);
            bgImage.sprite = null;
            bgImage.color = info.tintColor;
        }

        if (turn > 0) countText.text = turn.ToString();
        else if (val != 0) countText.text = (val > 0 ? "+" : "") + val.ToString();
        else countText.text = "";
    }

    // 새로 등장할 때 호출 — 아래에서 슬라이드 + 페이드 인.
    public void PlaySlideIn()
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        if (gameObject.activeInHierarchy)
            slideCoroutine = StartCoroutine(SlideInCoroutine());
    }

    IEnumerator SlideInCoroutine()
    {
        if (contentRt == null) yield break;
        Vector2 finalPos = Vector2.zero;
        Vector2 startPos = new Vector2(0f, -SlideOffsetY);

        contentRt.anchoredPosition = startPos;
        if (contentCg != null) contentCg.alpha = 0f;

        float t = 0f;
        while (t < SlideDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / SlideDuration);
            contentRt.anchoredPosition = Vector2.Lerp(startPos, finalPos, k);
            if (contentCg != null) contentCg.alpha = k;
            yield return null;
        }
        contentRt.anchoredPosition = finalPos;
        if (contentCg != null) contentCg.alpha = 1f;
        slideCoroutine = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StatusTooltip.Show(type, value, turns);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StatusTooltip.Hide();
    }

    void OnDisable()
    {
        StatusTooltip.Hide();
    }
}
