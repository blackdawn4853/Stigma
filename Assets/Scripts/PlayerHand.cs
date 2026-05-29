using UnityEngine;
using System.Collections.Generic;

public class PlayerHand : MonoBehaviour
{
    public static PlayerHand Instance { get; private set; }

    [Header("카드 UI 프리팹")]
    public GameObject cardPrefab;

    [Header("손패 배치 위치")]
    public Transform handTransform;

    [Header("손패 배치 (가운데 정렬, 카드 사용 시 자동 모임)")]
    [Tooltip("카드 사이 기본 간격 (로컬 단위)")]
    public float cardSpacing = 220f;
    [Tooltip("손패 최대 폭 — 카드가 많아 넘치면 간격을 압축")]
    public float maxHandWidth = 1500f;

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

        // BattleManager 손패 기준으로 다시 생성 — handCardIds 와 병렬 인덱스로 인스턴스 ID 동행
        var bm = BattleManager.Instance;
        for (int i = 0; i < bm.hand.Count; i++)
        {
            int id = i < bm.handCardIds.Count ? bm.handCardIds[i] : 0;
            SpawnCard(bm.hand[i], id);
        }

        ArrangeHand(); // 스폰 직후 가운데 정렬 (초기 드로우 뭉침 방지)
    }

    // 손패를 가운데 기준으로 균등 배치. 카드 수가 바뀔 때마다 호출 → 자동으로 모이고 퍼짐.
    public void ArrangeHand()
    {
        cardUIList.RemoveAll(c => c == null);
        int n = cardUIList.Count;
        if (n == 0) return;

        float spacing = cardSpacing;
        if (n > 1 && spacing * (n - 1) > maxHandWidth)
            spacing = maxHandWidth / (n - 1);

        float startX = -spacing * (n - 1) / 2f;
        for (int i = 0; i < n; i++)
        {
            cardUIList[i].SetHome(new Vector3(startX + spacing * i, 0f, 0f));
            cardUIList[i].transform.SetSiblingIndex(i);
        }
    }

    // 카드 UI 생성
    void SpawnCard(CardData cardData, int instanceId)
    {
        GameObject cardObj = Instantiate(cardPrefab, handTransform);
        CardUI cardUI = cardObj.GetComponent<CardUI>();
        cardUI.Setup(cardData, instanceId);
        cardUIList.Add(cardUI);
    }

    // 카드 사용 후 UI에서 제거
    public void RemoveCardFromHand(CardUI cardUI)
    {
        cardUIList.Remove(cardUI);
        Destroy(cardUI.gameObject);

        ArrangeHand(); // 남은 카드들을 가운데로 다시 모음 (빈칸 메움)

        // BattleUI 업데이트 (마나 등)
        BattleUI.Instance.UpdateUI();
    }

    // 턴 종료 후 손패 새로고침
    public void OnTurnEnd()
    {
        RefreshHand();
    }
}