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
    // 카드별 일러스트 — CardData.cardImage 가 있으면 표시, 없으면 비활성. 카드 전체를 덮는 배경.
    public Image cardArtImage;
    // 카드 종류별 식별 아이콘 — typeStyles 의 cardIconSprite 가 cardType 매칭하여 자동 적용.
    public Image cardIconImage;
    // 카드 종류별로 sprite 가 자동 교체되는 cost 박스 — typeStyles 에서 cardType 매칭하여 적용.
    public Image costBoxImage;

    [System.Serializable]
    public struct CardTypeStyle
    {
        public CardData.CardType type;
        public Sprite costBoxSprite;
        public Sprite cardIconSprite;
    }

    [Header("카드 종류별 스타일 (Attack/Skill/Forbidden/Power)")]
    public CardTypeStyle[] typeStyles;

    [Header("호버 설정")]
    public float hoverScale = 1.6f;
    public float hoverSpeed = 8f;
    public float hoverYOffset = 80f;
    [Tooltip("LayoutGroup 아래(리워드/상점)에서의 호버 — 위로 안 올리고 이 배율로 살짝만 확대")]
    public float compactHoverScale = 1.12f;

    [Header("드래그 설정")]
    public float arrowTriggerDistance = 80f;

    private CardData cardData;
    // 손패 카드 인스턴스 ID — 같은 SO 가 여러 장일 때 인스턴스 단위 효과(40-4 등)가 정확히 매칭되게 함.
    // 0 이면 미지정 (랜덤 카드 사용 등 손패 외 사용에서 사용).
    private int instanceId;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Vector3 targetScale;
    private Vector3 targetPosition;

    private bool isDragging = false;
    private bool isArrowMode = false;
    private bool hovering = false;
    private bool homeSet = false;       // PlayerHand.ArrangeHand 가 home 을 지정했는지
    private Vector2 dragStartScreenPos;
    private Canvas canvas;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        canvas = GetComponentInParent<Canvas>();
        // PlayerHand 가 SetHome 으로 직접 배치하지 않는 경우(리워드/상점 등 LayoutGroup 사용)
        // → 1프레임 뒤 레이아웃이 적용된 위치를 home 으로 캡처. (없으면 전부 (0,0)에 겹침)
        if (!homeSet)
        {
            originalPosition = transform.localPosition;
            targetPosition = originalPosition;
            StartCoroutine(InitPosition());
        }
    }

    System.Collections.IEnumerator InitPosition()
    {
        yield return null;
        if (!homeSet) // 그 사이 PlayerHand 가 SetHome 했으면 그대로 둠
        {
            originalPosition = transform.localPosition;
            targetPosition = originalPosition;
        }
    }

    // PlayerHand 가 손패 배치 시 호출 — 이 카드의 정위치(home)를 지정. Update 가 여기로 부드럽게 모임.
    public void SetHome(Vector3 pos)
    {
        originalPosition = pos;
        homeSet = true;
        if (!isDragging && !hovering)
            targetPosition = pos;
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
    // 버프(금색)/디버프(보라색)/동일(원문 유지). 새 카드 추가 시 별도 작업 불필요 — 설명 텍스트에
    // 데미지/방어도 기본 수치를 standalone 숫자로 적기만 하면 자동 적용된다.
    private const string ColorBuff = "#FFD700"; // 버프 (금색)
    private const string ColorNerf = "#C77DFF"; // 디버프 (보라색)

    void RefreshDynamicDisplay()
    {
        if (cardData == null) return;

        if (manaCostText != null)
        {
            int cost = GazeEffectManager.Instance != null
                ? GazeEffectManager.Instance.GetEffectiveCost(cardData, instanceId)
                : cardData.manaCost;
            manaCostText.text = cost.ToString();
            // 버프(마나 감소)=금색 / 디버프(마나 증가)=보라색 / 동일=흰색.
            manaCostText.color = cost < cardData.manaCost ? new Color(1f, 0.843f, 0f)
                                : cost > cardData.manaCost ? new Color(0.78f, 0.49f, 1f)
                                : Color.white;
        }

        if (descriptionText != null)
        {
            bool hide = GazeEffectManager.Instance != null
                && GazeEffectManager.Instance.HiddenTextCard == cardData;
            if (hide)
            {
                descriptionText.text = "<color=#C77DFF>???</color>";
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
            hovering = true;
            var lg = GetComponentInParent<UnityEngine.UI.LayoutGroup>();
            bool layoutManaged = lg != null && lg.enabled; // 리워드/상점 = LayoutGroup 활성

            if (layoutManaged)
            {
                // 위로 안 올리고 살짝만 확대 (제자리). sibling 변경도 안 함(레이아웃 재배치 방지).
                targetScale = originalScale * compactHoverScale;
                targetPosition = originalPosition;
            }
            else
            {
                // 전투 손패 = 위로 크게 솟아오름 + 맨 앞으로.
                targetScale = originalScale * hoverScale;
                targetPosition = originalPosition + new Vector3(0, hoverYOffset, 0);
                transform.SetAsLastSibling();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isDragging)
        {
            hovering = false;
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
                    Vector3 startWorld = ScreenToWorldOnZ0(dragStartScreenPos);
                    DragArrow.Instance.ShowArrow(startWorld);
                }
            }
        }

        if (isArrowMode && DragArrow.Instance != null)
        {
            Vector3 mouseWorld = ScreenToWorldOnZ0(eventData.position);
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
            Vector3 worldPos = ScreenToWorldOnZ0(eventData.position);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);

            if (hit != null && hit.CompareTag("Monster"))
            {
                Monster targetMonster = hit.GetComponent<Monster>();
                if (targetMonster == null) targetMonster = hit.GetComponentInParent<Monster>();
                if (targetMonster != null && targetMonster.IsAlive)
                {
                    success = BattleManager.Instance.PlayCardOnMonster(cardData, targetMonster, instanceId);
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
            success = BattleManager.Instance.PlayCardOnField(cardData, instanceId);
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

    public void Setup(CardData data, int id = 0)
    {
        cardData = data;
        instanceId = id;
        if (cardNameText != null) cardNameText.text = data.cardName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (manaCostText != null) manaCostText.text = data.manaCost.ToString();

        if (rarityBorder != null)
            rarityBorder.color = data.GetRarityColor();

        // 카드별 일러스트 — sprite 있으면 표시, 없으면 숨김. 신규 카드 추가는 SO 슬롯에 드래그만.
        if (cardArtImage != null)
        {
            if (data.cardImage != null)
            {
                cardArtImage.sprite = data.cardImage;
                cardArtImage.enabled = true;
            }
            else
            {
                cardArtImage.enabled = false;
            }
        }

        // 카드 종류별 자동 매핑 (cost 박스 + 아이콘).
        Sprite typeCostSprite = null;
        Sprite typeIconSprite = null;
        if (typeStyles != null)
        {
            for (int i = 0; i < typeStyles.Length; i++)
            {
                if (typeStyles[i].type == data.cardType)
                {
                    typeCostSprite = typeStyles[i].costBoxSprite;
                    typeIconSprite = typeStyles[i].cardIconSprite;
                    break;
                }
            }
        }

        if (costBoxImage != null && typeCostSprite != null)
        {
            costBoxImage.sprite = typeCostSprite;
            costBoxImage.enabled = true;
        }

        if (cardIconImage != null)
        {
            if (typeIconSprite != null)
            {
                cardIconImage.sprite = typeIconSprite;
                cardIconImage.enabled = true;
            }
            else
            {
                cardIconImage.enabled = false;
            }
        }
    }

    public CardData GetCardData() => cardData;
    public int GetInstanceId() => instanceId;

    // 화면 좌표를 월드 Z=0 평면 위 좌표로 변환 — orthographic/perspective 카메라 모두 대응.
    static Vector3 ScreenToWorldOnZ0(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);
        return new Vector3(screenPos.x, screenPos.y, 0f);
    }
}