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
    public int runGazeLevel;
    public bool startNodeUnlocked;
    public int bossesDefeated;
    public List<string> deckCardNames = new List<string>();
    public List<DrawLine> drawingLines = new List<DrawLine>();

    // 맵 구조 (전체 노드 + 연결)
    public int currentNodeLayer = -1;
    public int currentNodeIndex = -1;
    public int mapLayerCount = 0;
    public List<SerializableNode> mapNodes = new List<SerializableNode>();

    // 시선 효과 — 런별로 굴린 5개 (20/40/60/80/100 임계값에 매칭)
    public List<string> activeGazeEffectNames = new List<string>();
}

// NodeData 의 직렬화 형태. 그래프 구조라 nextNodes 는 (layer, index) 키로 저장.
[System.Serializable]
public class SerializableNode
{
    public int nodeType;        // (int)NodeData.NodeType
    public int layer;
    public int index;
    public Vector2 position;
    public bool isVisited;
    public bool isAccessible;
    public List<int> nextLayers = new List<int>();
    public List<int> nextIndices = new List<int>();
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

    [Header("런 단위 시선 (전투 사이에 유지됨, 새 런 시작/사망 시 0)")]
    public int runGazeLevel = 0;

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

    [Header("노드맵 드로잉 (영구 저장)")]
    [HideInInspector] public List<DrawLine> drawingLines = new List<DrawLine>();

    // Load 직후 1프레임만 true — MapSceneManager 가 보고 맵 복원 분기 탐
    [HideInInspector] public bool justLoadedFromSave = false;
    [HideInInspector] public List<SerializableNode> pendingMapNodes;
    [HideInInspector] public int pendingMapLayerCount;

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

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.InitializeRun();

        // 새 런: 인카운터 최근 메모리 리셋
        if (EncounterDatabase.Instance != null)
            EncounterDatabase.Instance.ResetAllRecent();
        EncounterDatabase.NextEncounter = null;
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
        runGazeLevel = 0;
        InitializeDeck();
        savedNodeStates.Clear();
        currentNodeLayer = -1;
        currentNodeIndex = -1;
        drawingLines.Clear(); // 새 런 → 새 맵 → 드로잉 초기화
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

    public void LoadBrand()
    {
        SceneManager.LoadScene("BrandNodeScene");
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
        data.runGazeLevel = runGazeLevel;
        data.startNodeUnlocked = startNodeUnlocked;
        data.bossesDefeated = bossesDefeated;
        foreach (CardData card in playerDeck)
            data.deckCardNames.Add(card.cardName);

        // 드로잉
        data.drawingLines = drawingLines != null ? drawingLines : new List<DrawLine>();

        // 맵 구조 (MapGenerator 가 layers 들고 있을 때만)
        data.currentNodeLayer = currentNodeLayer;
        data.currentNodeIndex = currentNodeIndex;
        if (MapGenerator.Instance != null)
        {
            var layers = MapGenerator.Instance.GetLayers();
            if (layers != null && layers.Count > 0)
            {
                data.mapLayerCount = layers.Count;
                foreach (var layer in layers)
                    foreach (var node in layer)
                        data.mapNodes.Add(SerializeNode(node));
            }
        }

        // 시선 효과 (런별 굴린 5개)
        if (GazeEffectManager.Instance != null)
            data.activeGazeEffectNames = GazeEffectManager.Instance.GetActiveEffectNames();

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"세이브 완료");
    }

    SerializableNode SerializeNode(NodeData n)
    {
        var sn = new SerializableNode();
        sn.nodeType = (int)n.nodeType;
        sn.layer = n.layer;
        sn.index = n.index;
        sn.position = n.position;
        sn.isVisited = n.isVisited;
        sn.isAccessible = n.isAccessible;
        if (n.nextNodes != null)
        {
            foreach (var next in n.nextNodes)
            {
                sn.nextLayers.Add(next.layer);
                sn.nextIndices.Add(next.index);
            }
        }
        return sn;
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
        runGazeLevel = data.runGazeLevel;
        startNodeUnlocked = data.startNodeUnlocked;
        bossesDefeated = data.bossesDefeated;

        playerDeck.Clear();
        foreach (string cardName in data.deckCardNames)
        {
            CardData found = FindCardByName(cardName);
            if (found != null) playerDeck.Add(found);
        }

        drawingLines = data.drawingLines != null ? data.drawingLines : new List<DrawLine>();

        // 맵 상태 → MapSceneManager 가 NodeMap Start 에서 복원
        currentNodeLayer = data.currentNodeLayer;
        currentNodeIndex = data.currentNodeIndex;
        pendingMapNodes = data.mapNodes;
        pendingMapLayerCount = data.mapLayerCount;

        // 시선 효과 복원 — GazeEffectManager 가 있으면 즉시 적용
        if (GazeEffectManager.Instance != null && data.activeGazeEffectNames != null)
            GazeEffectManager.Instance.RestoreActiveEffects(data.activeGazeEffectNames);
    }

    // MainMenu 의 Load 버튼이 호출 — Load + 컷씬 스킵 + NodeMap 직행.
    public void LoadGameAndStart()
    {
        if (!HasSaveFile())
        {
            Debug.LogWarning("[GameManager] 세이브 파일 없음 — Load 무시");
            return;
        }
        Load();
        justLoadedFromSave = true;
        SceneManager.LoadScene("NodeMap");
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