using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("플레이어 스탯")]
    public int playerMaxHp = 50;
    public int playerCurrentHp = 50;
    public int playerGold = 100;

    [Header("현재 게임 상태")]
    public string pendingNodeType = "";

    [Header("던전 상태")]
    public int currentRoomCount = 0;
    public bool returningFromBattle = false;
    public bool hasKey = false;
    public Vector2Int savedRoomGridPos = Vector2Int.zero;

    [Header("시작 덱 설정")]
    public CardData[] startingDeck;

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
            playerDeck.Add(card);

        Debug.Log($"시작 덱 초기화 완료: {playerDeck.Count}장");
    }

    public void GameOver()
    {
        InitializeDeck();
        playerCurrentHp = playerMaxHp;
        playerGold = 100;

        Debug.Log("게임 오버! 덱 초기화");
        SceneManager.LoadScene("MapScene");
    }

    public void AddCardToDeck(CardData card)
    {
        playerDeck.Add(card);
        Debug.Log($"덱에 추가: {card.cardName} | 현재 덱: {playerDeck.Count}장");
    }

    public void LoadBattle()
    {
        pendingNodeType = "Battle";
        SceneManager.LoadScene("BattleScene");
    }

    public void LoadShop()
    {
        pendingNodeType = "Shop";
        SceneManager.LoadScene("ShopScene");
    }

    public void LoadHeal()
    {
        playerCurrentHp = Mathf.Min(playerCurrentHp + 15, playerMaxHp);
        Debug.Log($"HP 회복! 현재 HP: {playerCurrentHp}/{playerMaxHp}");
        SceneManager.LoadScene("MapScene");
    }

    public void ReturnToMap()
    {
        returningFromBattle = true;
        SceneManager.LoadScene("MapScene");
    }
}