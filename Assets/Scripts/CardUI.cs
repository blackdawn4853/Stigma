using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("카드 UI 요소")]
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI manaCostText;
    public Image cardBackground;
    public Image rarityBorder;

    [Header("호버 설정")]
    public float hoverScale = 1.6f;
    public float hoverSpeed = 8f;
    public float hoverYOffset = 80f;

    [Header("드래그 설정")]
    public float arrowTriggerDistance = 80f;

    private CardData cardData;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Vector3 targetScale;
    private Vector3 targetPosition;

    private bool isDragging = false;
    private bool isArrowMode = false;
    private Vector2 dragStartScreenPos;
    private Canvas canvas;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        canvas = GetComponentInParent<Canvas>();
        StartCoroutine(InitPosition());
    }

    System.Collections.IEnumerator InitPosition()
    {
        yield return null;
        originalPosition = transform.localPosition;
        targetPosition = originalPosition;
    }

    void Update()
    {
        if (!isDragging)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * hoverSpeed);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * hoverSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isDragging)
        {
            targetScale = originalScale * hoverScale;
            targetPosition = originalPosition + new Vector3(0, hoverYOffset, 0);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isDragging)
        {
            targetScale = originalScale;
            targetPosition = originalPosition;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardData == null || !cardData.requiresTarget) return;

        isDragging = true;
        isArrowMode = false;
        dragStartScreenPos = eventData.position;
        targetScale = originalScale;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        float distance = Vector2.Distance(eventData.position, dragStartScreenPos);

        if (!isArrowMode)
        {
            if (distance < arrowTriggerDistance)
            {
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    eventData.position,
                    canvas.worldCamera,
                    out worldPos);
                transform.position = worldPos;
            }
            else
            {
                isArrowMode = true;
                transform.localPosition = originalPosition;
                targetPosition = originalPosition;

                if (DragArrow.Instance != null)
                {
                    Vector3 startWorld = Camera.main.ScreenToWorldPoint(dragStartScreenPos);
                    startWorld.z = 0;
                    DragArrow.Instance.ShowArrow(startWorld);
                }
            }
        }

        if (isArrowMode && DragArrow.Instance != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(eventData.position);
            mouseWorld.z = 0;
            DragArrow.Instance.UpdateArrow(mouseWorld);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        if (DragArrow.Instance != null)
            DragArrow.Instance.HideArrow();

        if (cardData == null)
        {
            transform.localPosition = originalPosition;
            targetPosition = originalPosition;
            return;
        }

        bool success = false;

        if (cardData.requiresTarget)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
            worldPos.z = 0;
            Collider2D hit = Physics2D.OverlapPoint(worldPos);

            if (hit != null && hit.CompareTag("Monster"))
            {
                success = BattleManager.Instance.PlayCardOnMonster(cardData);
            }
            else
            {
                Debug.Log("몬스터에게 드래그해줘!");
            }
        }
        else
        {
            success = BattleManager.Instance.PlayCardOnField(cardData);
        }

        if (success)
            PlayerHand.Instance.RemoveCardFromHand(this);
        else
        {
            transform.localPosition = originalPosition;
            targetPosition = originalPosition;
            isArrowMode = false;
        }
    }

    public void Setup(CardData data)
    {
        cardData = data;
        if (cardNameText != null) cardNameText.text = data.cardName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (manaCostText != null) manaCostText.text = data.manaCost.ToString();

        if (rarityBorder != null)
            rarityBorder.color = data.GetRarityColor();
    }

    public CardData GetCardData() => cardData;
}