using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 연결")]
    public GameObject hoverBG;           // HoverBG 오브젝트
    public TextMeshProUGUI buttonText;   // 버튼 텍스트

    [Header("색상")]
    public Color normalTextColor = Color.white;
    public Color hoverTextColor = Color.black;

    [Header("슬라이드 설정")]
    public float slideDistance = 300f;   // 시작 위치 (왼쪽 밖)
    public float slideDuration = 0.2f;   // 슬라이드 시간

    private RectTransform hoverBGRect;
    private Vector2 hiddenPos;   // 숨겨진 위치 (왼쪽 밖)
    private Vector2 shownPos;    // 보여지는 위치
    private Coroutine currentCoroutine;

    void Start()
    {
        hoverBGRect = hoverBG.GetComponent<RectTransform>();

        shownPos = hoverBGRect.anchoredPosition;
        hiddenPos = new Vector2(-slideDistance, shownPos.y);

        // 시작 시 숨기기
        hoverBGRect.anchoredPosition = hiddenPos;
        hoverBG.SetActive(false);

        if (buttonText != null)
            buttonText.color = normalTextColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        hoverBG.SetActive(true);
        currentCoroutine = StartCoroutine(SlideIn());
        if (buttonText != null)
            buttonText.color = hoverTextColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(SlideOut());
        if (buttonText != null)
            buttonText.color = normalTextColor;
    }

    IEnumerator SlideIn()
    {
        float elapsed = 0f;
        Vector2 startPos = hoverBGRect.anchoredPosition;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            hoverBGRect.anchoredPosition = Vector2.Lerp(startPos, shownPos, t);
            yield return null;
        }

        hoverBGRect.anchoredPosition = shownPos;
    }

    IEnumerator SlideOut()
    {
        float elapsed = 0f;
        Vector2 startPos = hoverBGRect.anchoredPosition;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            hoverBGRect.anchoredPosition = Vector2.Lerp(startPos, hiddenPos, t);
            yield return null;
        }

        hoverBGRect.anchoredPosition = hiddenPos;
        hoverBG.SetActive(false);
    }
}