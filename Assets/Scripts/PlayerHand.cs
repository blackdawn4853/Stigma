using UnityEngine;
using System.Collections.Generic;

public class PlayerHand : MonoBehaviour
{
    public static PlayerHand Instance { get; private set; }

    [Header("카드 UI 프리팹")]
    public GameObject cardPrefab;

    [Header("손패 배치 위치")]
    public Transform handTransform;

    private List<CardUI> cardUIList = new List<CardUI>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        
    }

    // 손패 전체 새로고침
    public void RefreshHand()
    {
        // 기존 카드 UI 전부 제거
        foreach (CardUI card in cardUIList)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        cardUIList.Clear();

        // BattleManager 손패 기준으로 다시 생성
        foreach (CardData cardData in BattleManager.Instance.hand)
        {
            SpawnCard(cardData);
        }
    }

    // 카드 UI 생성
    void SpawnCard(CardData cardData)
    {
        GameObject cardObj = Instantiate(cardPrefab, handTransform);
        CardUI cardUI = cardObj.GetComponent<CardUI>();
        cardUI.Setup(cardData);
        cardUIList.Add(cardUI);
    }

    // 카드 사용 후 UI에서 제거
    public void RemoveCardFromHand(CardUI cardUI)
    {
        cardUIList.Remove(cardUI);
        Destroy(cardUI.gameObject);
        
        // BattleUI 업데이트 (마나 등)
        BattleUI.Instance.UpdateUI();
    }

    // 턴 종료 후 손패 새로고침
    public void OnTurnEnd()
    {
        RefreshHand();
    }
}