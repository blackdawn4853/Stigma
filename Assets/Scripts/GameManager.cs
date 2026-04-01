using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class SaveData
{
    public int playerMaxHp;
    public int playerCurrentHp;
    public int playerGold;
    public bool startNodeUnlocked;
    public int bossesDefeated;
    public List<string> deckCardNames = new List<string>();
}

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

    [Header("진행 상태")]
    public bool startNodeUnlocked = false;
    public int bossesDefeated = 0;

    [Header("시작 덱 설정")]
    public CardData[] startingDeck;
    public int strikeCount = 5;
    public int defendCount = 5;

    [Header("덱 관리")]
    public List<CardData> playerDeck = new List<CardData>();

    [Header("전체 카드 목록 (세이브/로드용)")]
    public CardData[] allCards;

    private string SavePath => Application.persistentDataPath + "/save.json";

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

    public void OnBossDefeated()
    {
        bossesDefeated++;

        if (bossesDefeated >= 1)
            startNodeUnlocked = true;

        Debug.Log($"보스 처치! 총 {bossesDefeated}회 | 시작 노드 해금: {startNodeUnlocked}");
        Save();
    }

    public void GameOver()
    {
        playerCurrentHp = playerMaxHp;
        playerGold = 100;
        InitializeDeck();
        Debug.Log("게임 오버!");
        SceneManager.LoadScene("NodeMap");
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
        SceneManager.LoadScene("NodeMap");
    }

    public void Save()
    {
        SaveData data = new SaveData();
        data.playerMaxHp = playerMaxHp;
        data.playerCurrentHp = playerCurrentHp;
        data.playerGold = playerGold;
        data.startNodeUnlocked = startNodeUnlocked;
        data.bossesDefeated = bossesDefeated;

        foreach (CardData card in playerDeck)
            data.deckCardNames.Add(card.cardName);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"세이브 완료: {SavePath}");
    }

    public bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    public void Load()
    {
        if (!HasSaveFile())
        {
            Debug.Log("세이브 파일 없음 — 새 게임 시작");
            return;
        }

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        playerMaxHp = data.playerMaxHp;
        playerCurrentHp = data.playerCurrentHp;
        playerGold = data.playerGold;
        startNodeUnlocked = data.startNodeUnlocked;
        bossesDefeated = data.bossesDefeated;

        playerDeck.Clear();
        foreach (string cardName in data.deckCardNames)
        {
            CardData found = FindCardByName(cardName);
            if (found != null)
                playerDeck.Add(found);
            else
                Debug.LogWarning($"카드 못 찾음: {cardName}");
        }

        Debug.Log($"로드 완료! HP:{playerCurrentHp}/{playerMaxHp} 골드:{playerGold} 덱:{playerDeck.Count}장");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("세이브 파일 삭제됨");
        }
    }

    CardData FindCardByName(string cardName)
    {
        if (allCards == null) return null;
        foreach (CardData card in allCards)
            if (card != null && card.cardName == cardName)
                return card;
        return null;
    }
}