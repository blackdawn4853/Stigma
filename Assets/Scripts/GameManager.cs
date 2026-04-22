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

[System.Serializable]
public class NodeState
{
    public int layer;
    public int index;
    public bool isVisited;
    public bool isAccessible;
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

    // 맵 상태 저장
    [HideInInspector] public List<NodeState> savedNodeStates = new List<NodeState>();
    [HideInInspector] public int currentNodeLayer = -1;
    [HideInInspector] public int currentNodeIndex = -1;

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
            if (card != null) playerDeck.Add(card);
        Debug.Log($"시작 덱 초기화: {playerDeck.Count}장");
    }

    public void OnBossDefeated()
    {
        bossesDefeated++;
        if (bossesDefeated >= 1) startNodeUnlocked = true;
        Save();
    }

    public void GameOver()
    {
        playerCurrentHp = playerMaxHp;
        playerGold = 100;
        InitializeDeck();
        savedNodeStates.Clear();
        currentNodeLayer = -1;
        currentNodeIndex = -1;
        SceneManager.LoadScene("NodeMap");
    }

    public void AddCardToDeck(CardData card)
    {
        playerDeck.Add(card);
        Debug.Log($"덱에 추가: {card.cardName} | 현재 덱: {playerDeck.Count}장");
    }

    public void LoadNodeMap()
    {
        SceneManager.LoadScene("NodeMap");
    }

    public void LoadCutscene()
    {
        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene("Cutscene");
        else
            SceneManager.LoadScene("Cutscene");
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

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("게임 종료");
    }

    // 맵 상태 저장
    public void SaveMapState(List<List<NodeData>> layers, NodeData currentNode)
    {
        savedNodeStates.Clear();

        foreach (var layer in layers)
        {
            foreach (var node in layer)
            {
                savedNodeStates.Add(new NodeState
                {
                    layer = node.layer,
                    index = node.index,
                    isVisited = node.isVisited,
                    isAccessible = node.isAccessible
                });
            }
        }

        if (currentNode != null)
        {
            currentNodeLayer = currentNode.layer;
            currentNodeIndex = currentNode.index;
        }
    }

    // 맵 상태 복원
    public void RestoreMapState(List<List<NodeData>> layers)
    {
        if (savedNodeStates.Count == 0) return;

        foreach (var state in savedNodeStates)
        {
            if (state.layer < layers.Count && state.index < layers[state.layer].Count)
            {
                NodeData node = layers[state.layer][state.index];
                node.isVisited = state.isVisited;
                node.isAccessible = state.isAccessible;
            }
        }
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
        Debug.Log($"세이브 완료");
    }

    public bool HasSaveFile() => File.Exists(SavePath);

    public void Load()
    {
        if (!HasSaveFile()) return;

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
            if (found != null) playerDeck.Add(found);
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }

    CardData FindCardByName(string cardName)
    {
        if (allCards == null) return null;
        foreach (CardData card in allCards)
            if (card != null && card.cardName == cardName) return card;
        return null;
    }
}