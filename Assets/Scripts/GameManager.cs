using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 스탯")]
    public int playerMaxHp = 100;
    public int playerCurrentHp = 100;
    public int playerGold = 100;

    [Header("던전 상태")]
    public bool returningFromBattle = false;
    public Vector2Int savedRoomGridPos = Vector2Int.zero;

    [Header("시작 덱 설정")]
    public CardData[] startingDeck; // 원하는 카드 몇 장이든 넣기 가능
    public int strikeCount = 5;     // 타격 몇 장
    public int defendCount = 5;     // 방어 몇 장

    [Header("덱 관리")]
    public List<CardData> playerDeck = new List<CardData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDeck();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeDeck()
    {
        playerDeck.Clear();

        foreach (CardData card in startingDeck)
        {
            if (card != null)
                playerDeck.Add(card);
        }

        Debug.Log($"시작 덱 초기화: {playerDeck.Count}장");
    }

    public void GameOver()
    {
        playerCurrentHp = playerMaxHp;
        playerGold = 100;
        InitializeDeck();
        Debug.Log("게임 오버!");
        SceneManager.LoadScene("MapScene");
    }

    public void AddCardToDeck(CardData card)
    {
        playerDeck.Add(card);
        Debug.Log($"덱에 추가: {card.cardName} | 현재 덱: {playerDeck.Count}장");
    }

    public void LoadBattle()
    {
        SceneManager.LoadScene("BattleScene");
    }

    public void LoadShop()
    {
        SceneManager.LoadScene("ShopScene");
    }

    public void ReturnToMap()
    {
        returningFromBattle = true;
        SceneManager.LoadScene("MapScene");
    }
}