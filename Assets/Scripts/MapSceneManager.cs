using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class MapSceneManager : MonoBehaviour
{
    public static MapSceneManager Instance { get; private set; }

    [Header("맵 UI")]
    public RectTransform mapContainer;
    public GameObject nodeUIPrefab;
    public GameObject linePrefab;

    public float topPadding = 150f;
    public float bottomPadding = 150f;

    private NodeData currentNode;
    private List<MapNodeUI> allNodeUIs = new List<MapNodeUI>();
    private Dictionary<NodeData, Vector2> nodeUIPositions = new Dictionary<NodeData, Vector2>();
    private List<List<NodeData>> currentLayers;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        GenerateAndDisplayMap();
    }

    void GenerateAndDisplayMap()
    {
        nodeUIPositions.Clear();
        allNodeUIs.Clear();

        // ✅ 복귀 시 기존 레이어 재사용, 아니면 새로 생성
        if (GameManager.Instance != null && GameManager.Instance.returningFromBattle)
            currentLayers = MapGenerator.Instance.GetLayers();
        else
            currentLayers = MapGenerator.Instance.GenerateMap();

        float layerHeight = MapGenerator.Instance.layerHeight;
        int maxLayer = currentLayers.Count - 1;
        float totalHeight = bottomPadding + (maxLayer * layerHeight) + topPadding;

        mapContainer.anchorMin = new Vector2(0.5f, 0f);
        mapContainer.anchorMax = new Vector2(0.5f, 0f);
        mapContainer.pivot = new Vector2(0.5f, 0f);
        mapContainer.anchoredPosition = Vector2.zero;
        mapContainer.sizeDelta = new Vector2(1920f, totalHeight);

        foreach (var layer in currentLayers)
            foreach (var nodeData in layer)
            {
                float xPos = nodeData.position.x;
                float yPos = bottomPadding + nodeData.position.y;
                nodeUIPositions[nodeData] = new Vector2(xPos, yPos);
            }

        DrawAllLines(currentLayers);

        foreach (var layer in currentLayers)
            foreach (var nodeData in layer)
                SpawnNodeUI(nodeData, layerHeight);

        if (GameManager.Instance != null && GameManager.Instance.returningFromBattle)
        {
            GameManager.Instance.returningFromBattle = false;
            GameManager.Instance.RestoreMapState(currentLayers);
            RestoreCurrentNode();
        }
        else
        {
            InitializeStartState(currentLayers);
        }

        RefreshAllNodes();
        StartCoroutine(ScrollToBottom());
    }

    void InitializeStartState(List<List<NodeData>> layers)
    {
        if (layers.Count < 2) return;

        NodeData startNode = layers[0][0];
        startNode.isAccessible = false;
        startNode.isVisited = true;
        currentNode = startNode;

        foreach (var node in layers[1])
            node.isAccessible = true;
    }

    void RestoreCurrentNode()
    {
        if (GameManager.Instance == null) return;
        int layer = GameManager.Instance.currentNodeLayer;
        int index = GameManager.Instance.currentNodeIndex;

        if (layer >= 0 && layer < currentLayers.Count &&
            index >= 0 && index < currentLayers[layer].Count)
            currentNode = currentLayers[layer][index];
    }

    IEnumerator ScrollToBottom()
    {
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = mapContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    void SpawnNodeUI(NodeData nodeData, float layerHeight)
    {
        if (nodeUIPrefab == null || mapContainer == null) return;

        GameObject nodeObj = Instantiate(nodeUIPrefab, mapContainer);
        RectTransform rt = nodeObj.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = nodeUIPositions[nodeData];

        MapNodeUI nodeUI = nodeObj.GetComponent<MapNodeUI>();
        if (nodeUI != null)
        {
            nodeUI.Setup(nodeData);
            allNodeUIs.Add(nodeUI);
        }
    }

    void DrawAllLines(List<List<NodeData>> layers)
    {
        if (linePrefab == null) return;

        foreach (var layer in layers)
            foreach (var nodeData in layer)
                foreach (var nextNode in nodeData.nextNodes)
                {
                    if (!nodeUIPositions.ContainsKey(nodeData) || !nodeUIPositions.ContainsKey(nextNode))
                        continue;
                    DrawLine(nodeUIPositions[nodeData], nodeUIPositions[nextNode]);
                }
    }

    void DrawLine(Vector2 from, Vector2 to)
    {
        if (linePrefab == null) return;

        GameObject lineObj = Instantiate(linePrefab, mapContainer);
        RectTransform rt = lineObj.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (from + to) / 2f;

        float distance = Vector2.Distance(from, to);
        rt.sizeDelta = new Vector2(distance, 4f);

        Vector2 dir = to - from;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnNodeSelected(NodeData nodeData)
    {
        nodeData.isVisited = true;
        if (currentNode != null)
            currentNode.isVisited = true;

        currentNode = nodeData;
        UpdateAccessibleNodes();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveMapState(currentLayers, currentNode);
            GameManager.Instance.Save();
        }

        RefreshAllNodes();

        switch (nodeData.nodeType)
        {
            case NodeData.NodeType.Start:
                Debug.Log("시작 노드 — 추후 구현");
                break;
            case NodeData.NodeType.Combat:
                if (GameManager.Instance != null)
                    GameManager.Instance.LoadBattle();
                else
                    SceneManager.LoadScene("BattleScene");
                break;
            case NodeData.NodeType.Shop:
                if (GameManager.Instance != null)
                    GameManager.Instance.LoadShop();
                else
                    SceneManager.LoadScene("ShopScene");
                break;
            case NodeData.NodeType.RandomEvent:
                Debug.Log("랜덤 이벤트!");
                break;
            case NodeData.NodeType.Brand:
                if (GameManager.Instance != null)
                    GameManager.Instance.LoadBrand();
                else
                    SceneManager.LoadScene("BrandNodeScene");
                break;
            case NodeData.NodeType.Boss:
                if (GameManager.Instance != null)
                    GameManager.Instance.LoadBattle();
                else
                    SceneManager.LoadScene("BattleScene");
                break;
        }
    }

    void UpdateAccessibleNodes()
    {
        foreach (var nodeUI in allNodeUIs)
            nodeUI.GetNodeData().isAccessible = false;

        if (currentNode != null)
            foreach (var nextNode in currentNode.nextNodes)
                nextNode.isAccessible = true;
    }

    void RefreshAllNodes()
    {
        foreach (var nodeUI in allNodeUIs)
            nodeUI.UpdateVisual();
    }

    public NodeData GetCurrentNode() => currentNode;
}