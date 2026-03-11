using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("카드 UI 요소")]
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI manaCostText;
    public Image cardBackground;
    public Image rarityBorder;

    [Header("호버 설정")]
    public float hoverScale = 1.4f;
    public float hoverSpeed = 8f;
    public float hoverYOffset = 30f;

    private CardData cardData;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Vector3 targetScale;
    private Vector3 targetPosition;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    // 한 프레임 기다렸다가 위치 저장
        StartCoroutine(InitPosition());
    }

    System.Collections.IEnumerator InitPosition()
    {
        yield return null; // 한 프레임 대기
        originalPosition = transform.localPosition;
        targetPosition = originalPosition;
    }

    void Update()
    {
        // 부드럽게 크기/위치 변환
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * hoverSpeed);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * hoverSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
        targetPosition = originalPosition + new Vector3(0, hoverYOffset, 0);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
        targetPosition = originalPosition;
    }

    public void Setup(CardData data)
    {
        cardData = data;
        cardNameText.text = data.cardName;
        descriptionText.text = data.description;
        manaCostText.text = data.manaCost.ToString();

        if (rarityBorder != null)
            rarityBorder.color = data.GetRarityColor();
    }

    public void OnCardClicked()
    {
        if (cardData == null) return;

        bool success = BattleManager.Instance.PlayCard(cardData);

        if (success)
        {
            PlayerHand.Instance.RemoveCardFromHand(this);
        }
        else
        {
            Debug.Log("마나 부족!");
        }
    }

    public CardData GetCardData() => cardData;
}