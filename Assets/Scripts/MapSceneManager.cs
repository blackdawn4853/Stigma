using UnityEngine;
using UnityEngine.UI;
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
        List<List<NodeData>> layers = MapGenerator.Instance.GenerateMap();

        float layerHeight = MapGenerator.Instance.layerHeight;
        int maxLayer = layers.Count - 1;

        foreach (var layer in layers)
        {
            foreach (var nodeData in layer)
            {
                SpawnNodeUI(nodeData, layerHeight, maxLayer);
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.returningFromBattle)
        {
            GameManager.Instance.returningFromBattle = false;
            RestoreMapState();
        }

        StartCoroutine(SetScrollToBottom());
    }

    IEnumerator SetScrollToBottom()
    {
        yield return null;
        yield return null;

        float layerHeight = MapGenerator.Instance.layerHeight;
        int maxLayer = MapGenerator.Instance.GetLayers().Count - 1;
        float totalHeight = topPadding + (maxLayer * layerHeight) + bottomPadding;
        mapContainer.sizeDelta = new Vector2(1920f, totalHeight);

        yield return null;

        ScrollRect scrollRect = mapContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    void SpawnNodeUI(NodeData nodeData, float layerHeight, int maxLayer)
    {
        if (nodeUIPrefab == null || mapContainer == null) return;

        GameObject nodeObj = Instantiate(nodeUIPrefab, mapContainer);
        RectTransform rt = nodeObj.GetComponent<RectTransform>();

        float yPos = -(topPadding + (maxLayer - nodeData.layer) * layerHeight);
        rt.anchoredPosition = new Vector2(nodeData.position.x, yPos);

        MapNodeUI nodeUI = nodeObj.GetComponent<MapNodeUI>();
        nodeUI.Setup(nodeData);
        allNodeUIs.Add(nodeUI);
    }

    void DrawLine(Vector2 from, Vector2 to)
    {
        if (linePrefab == null) return;

        GameObject lineObj = Instantiate(linePrefab, mapContainer);
        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchoredPosition = (from + to) / 2f;
        float distance = Vector2.Distance(from, to);
        rt.sizeDelta = new Vector2(distance, 4f);
        Vector2 dir = to - from;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnNodeSelected(NodeData nodeData)
    {
        if (currentNode != null)
            currentNode.isVisited = true;

        currentNode = nodeData;
        UpdateAccessibleNodes();

        switch (nodeData.nodeType)
        {
            case NodeData.NodeType.Combat:
                GameManager.Instance.LoadBattle();
                break;
            case NodeData.NodeType.Shop:
                GameManager.Instance.LoadShop();
                break;
            case NodeData.NodeType.RandomEvent:
                Debug.Log("랜덤 이벤트!");
                break;
            case NodeData.NodeType.Boss:
                GameManager.Instance.LoadBattle();
                break;
        }

        RefreshAllNodes();
    }

    void UpdateAccessibleNodes()
    {
        foreach (var nodeUI in allNodeUIs)
            nodeUI.GetNodeData().isAccessible = false;

        if (currentNode != null)
        {
            foreach (var nextNode in currentNode.nextNodes)
                nextNode.isAccessible = true;
        }
    }

    void RefreshAllNodes()
    {
        foreach (var nodeUI in allNodeUIs)
            nodeUI.UpdateVisual();
    }

    void RestoreMapState()
    {
        UpdateAccessibleNodes();
        RefreshAllNodes();
    }

    public NodeData GetCurrentNode() => currentNode;
}