using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Shop_Manager : MonoBehaviour
{
    [Header("상점 팝업창 오브젝트")]
    public GameObject shopPopup;

    [Header("카드 생성 설정")]
    public GameObject cardPrefab;
    public Transform cardPlace;
    public List<CardData> allCards = new List<CardData>();

    private Vector3 originalScale;
    private bool isHovering = false;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    void Start()
    {
        if (shopPopup != null) shopPopup.SetActive(false);
    }

    // ─── 호버 효과 ───────────────────────────────
    public void OnPointerEnter()
    {
        if (!isHovering)
        {
            isHovering = true;
            StopAllCoroutines();
            StartCoroutine(ScaleTo(originalScale * 1.1f, 0.08f));
        }
    }

    public void OnPointerExit()
    {
        isHovering = false;
        StopAllCoroutines();
        StartCoroutine(ScaleTo(originalScale, 0.08f));
    }

    // ─── 클릭 효과 + 상점 열기 ───────────────────
    public void OpenShop()
    {
        StartCoroutine(ClickEffect());

        if (shopPopup != null)
        {
            shopPopup.SetActive(true);
            GenerateCards();
        }
    }

    IEnumerator ClickEffect()
    {
        StopCoroutine("ScaleTo");
        transform.localScale = originalScale * 0.9f;
        yield return new WaitForSeconds(0.1f);
        StartCoroutine(ScaleTo(originalScale * 1.1f, 0.08f));
    }

    // ─── 부드러운 크기 변환 ───────────────────────
    IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        transform.localScale = target;
    }

    // ─── 카드 생성 ────────────────────────────────
    void GenerateCards()
    {
        foreach (Transform child in cardPlace) Destroy(child.gameObject);

        List<CardData> shuffleList = new List<CardData>(allCards);
        int count = Mathf.Min(5, shuffleList.Count);

        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, shuffleList.Count);
            CardData selectedData = shuffleList[randomIndex];
            GameObject newCard = Instantiate(cardPrefab, cardPlace);
            newCard.GetComponent<CardUI>().Setup(selectedData);
            shuffleList.RemoveAt(randomIndex);
        }
    }

    // ─── 상점 닫기 ────────────────────────────────
    public void CloseShop()
    {
        if (shopPopup != null) shopPopup.SetActive(false);
    }
}