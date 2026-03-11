using UnityEngine;
using System.Collections.Generic;

public class RewardScene : MonoBehaviour
{
    [Header("보상 카드 설정")]
    public CardData[] allCards; // 전체 카드 풀 (Inspector에서 설정)
    public int rewardCount = 3; // 보상으로 제시할 카드 수

    [Header("카드 UI")]
    public GameObject cardPrefab;
    public Transform cardContainer;

    private List<CardData> rewardCards = new List<CardData>();

    void Start()
    {
        GenerateRewardCards();
        DisplayRewardCards();
    }

    void GenerateRewardCards()
    {
        rewardCards.Clear();
        List<CardData> pool = new List<CardData>(allCards);

        // 랜덤으로 rewardCount만큼 카드 선택
        for (int i = 0; i < rewardCount && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            rewardCards.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    void DisplayRewardCards()
    {
        foreach (CardData card in rewardCards)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            CardUI cardUI = cardObj.GetComponent<CardUI>();
            cardUI.Setup(card);

            // 버튼 클릭 이벤트 재설정 (BattleManager 없으므로)
            UnityEngine.UI.Button btn = cardObj.GetComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();

            CardData capturedCard = card;
            btn.onClick.AddListener(() => SelectCard(capturedCard));
        }
    }

    public void SelectCard(CardData card)
    {
        Debug.Log($"카드 선택: {card.cardName}");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddCardToDeck(card);
            GameManager.Instance.ReturnToMap(); // 이걸로 교체!
        }
    }
}