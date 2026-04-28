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

        RefreshDynamicDisplay();
    }

    // 시선/근력/약화 모디파이어가 반영된 데미지·방어도 값을 카드 설명에서 색상으로 강조.
    // 향상(green)/감소(red)/동일(원문 유지). 새 카드 추가 시 별도 작업 불필요 — 설명 텍스트에
    // 데미지/방어도 기본 수치를 standalone 숫자로 적기만 하면 자동 적용된다.
    private const string ColorBuff = "#66ff99"; // 더 좋아짐
    private const string ColorNerf = "#ff6666"; // 더 나빠짐

    void RefreshDynamicDisplay()
    {
        if (cardData == null) return;

        if (manaCostText != null)
        {
            int cost = GazeEffectManager.Instance != null
                ? GazeEffectManager.Instance.GetEffectiveCost(cardData)
                : cardData.manaCost;
            manaCostText.text = cost.ToString();
            manaCostText.color = cost < cardData.manaCost ? new Color(0.4f, 1f, 0.6f)
                                : cost > cardData.manaCost ? new Color(1f, 0.4f, 0.4f)
                                : Color.white;
        }

        if (descriptionText != null)
        {
            bool hide = GazeEffectManager.Instance != null
                && GazeEffectManager.Instance.HiddenTextCard == cardData;
            if (hide)
            {
                descriptionText.text = "<color=#888>???</color>";
            }
            else
            {
                descriptionText.text = BuildEffectiveDescription(cardData);
            }
        }
    }

    string BuildEffectiveDescription(CardData card)
    {
        string desc = card.description;
        if (string.IsNullOrEmpty(desc) || BattleManager.Instance == null) return desc ?? "";

        // 데미지 값 강조
        int baseDmg = GazeEffectManager.GetCardBaseDamageValue(card);
        if (baseDmg > 0)
        {
            int effDmg = BattleManager.Instance.PreviewCardDamage(card);
            if (effDmg != baseDmg)
                desc = ReplaceStandaloneNumber(desc, baseDmg,
                    $"<color={(effDmg > baseDmg ? ColorBuff : ColorNerf)}>{effDmg}</color>");
        }

        // 방어도 값 강조
        int baseShd = GazeEffectManager.GetCardBaseShieldValue(card);
        if (baseShd > 0)
        {
            int effShd = BattleManager.Instance.PreviewCardShield(card);
            if (effShd != baseShd)
                desc = ReplaceStandaloneNumber(desc, baseShd,
                    $"<color={(effShd > baseShd ? ColorBuff : ColorNerf)}>{effShd}</color>");
        }

        return desc;
    }

    // standalone 숫자(앞뒤가 숫자가 아닌 위치)의 첫 매치만 치환.
    // 한국어 사이의 숫자(예: "10의 피해") 정상 매칭, 색상 태그 안의 숫자(예: "<color>15</color>") 미매칭.
    static string ReplaceStandaloneNumber(string text, int number, string replacement)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string s = number.ToString();
        int idx = 0;
        while ((idx = text.IndexOf(s, idx, System.StringComparison.Ordinal)) >= 0)
        {
            bool prevOk = idx == 0 || !char.IsDigit(text[idx - 1]);
            int after = idx + s.Length;
            bool nextOk = after >= text.Length || !char.IsDigit(text[after]);
            if (prevOk && nextOk)
                return text.Substring(0, idx) + replacement + text.Substring(after);
            idx += s.Length;
        }
        return text;
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
                Monster targetMonster = hit.GetComponent<Monster>();
                if (targetMonster == null) targetMonster = hit.GetComponentInParent<Monster>();
                if (targetMonster != null && targetMonster.IsAlive)
                {
                    success = BattleManager.Instance.PlayCardOnMonster(cardData, targetMonster);
                }
                else
                {
                    Debug.Log("이미 쓰러진 몬스터야!");
                }
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