using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance { get; private set; }

    [Header("맵 설정")]
    public int minLayers = 15;
    public int maxLayers = 20;
    public int minStartNodes = 3;
    public int maxStartNodes = 5;
    public int minNextNodes = 1;
    public int maxNextNodes = 3;
    public int maxNodesPerLayer = 5;

    [Header("노드 타입 확률 (%)")]
    public int combatChance = 60;
    public int shopChance = 20;
    public int randomEventChance = 20;

    [Header("화면 설정")]
    public float layerHeight = 150f;
    public float nodeSpacing = 200f;

    private List<List<NodeData>> layers = new List<List<NodeData>>();
    private NodeData startNode;
    private NodeData bossNode;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public List<List<NodeData>> GenerateMap()
    {
        layers.Clear();

        int totalLayers = Random.Range(minLayers, maxLayers + 1);

        // 1. 시작 노드
        List<NodeData> startLayer = new List<NodeData>();
        startNode = new NodeData();
        startNode.nodeType = NodeData.NodeType.Start;
        startNode.layer = 0;
        startNode.index = 0;
        startNode.isAccessible = true;
        startLayer.Add(startNode);
        layers.Add(startLayer);

        // 2. 첫 번째 층 - 3~5개 노드
        int firstLayerCount = Random.Range(minStartNodes, maxStartNodes + 1);
        List<NodeData> firstLayer = CreateLayer(1, firstLayerCount);
        layers.Add(firstLayer);

        foreach (var node in firstLayer)
        {
            startNode.nextNodes.Add(node);
            node.prevNodes.Add(startNode);
        }

        // 3. 중간 층들
        for (int i = 2; i < totalLayers; i++)
        {
            int nodeCount = Random.Range(2, maxNodesPerLayer + 1);
            List<NodeData> layer = CreateLayer(i, nodeCount);
            layers.Add(layer);
            ConnectLayers(layers[i - 1], layer);
        }

        // 4. 보스 노드
        List<NodeData> bossLayer = new List<NodeData>();
        bossNode = new NodeData();
        bossNode.nodeType = NodeData.NodeType.Boss;
        bossNode.layer = totalLayers;
        bossNode.index = 0;
        bossLayer.Add(bossNode);
        layers.Add(bossLayer);

        foreach (var node in layers[totalLayers - 1])
        {
            node.nextNodes.Add(bossNode);
            bossNode.prevNodes.Add(node);
        }

        CalculatePositions();

        // 디버그
        Debug.Log($"맵 생성 완료! 총 층 수: {layers.Count}");
        foreach (var layer in layers)
            Debug.Log($"층 {layer[0].layer}: {layer.Count}개 노드");

        return layers;
    }

    List<NodeData> CreateLayer(int layerIndex, int count)
    {
        List<NodeData> layer = new List<NodeData>();
        for (int i = 0; i < count; i++)
        {
            NodeData node = new NodeData();
            node.nodeType = GetRandomNodeType(layerIndex);
            node.layer = layerIndex;
            node.index = i;
            layer.Add(node);
        }
        return layer;
    }

    void ConnectLayers(List<NodeData> currentLayer, List<NodeData> nextLayer)
    {
        foreach (var nextNode in nextLayer)
        {
            NodeData randomPrev = currentLayer[Random.Range(0, currentLayer.Count)];
            if (!randomPrev.nextNodes.Contains(nextNode))
            {
                randomPrev.nextNodes.Add(nextNode);
                nextNode.prevNodes.Add(randomPrev);
            }
        }

        foreach (var currentNode in currentLayer)
        {
            int connections = Random.Range(minNextNodes, maxNextNodes + 1);
            for (int i = 0; i < connections; i++)
            {
                NodeData randomNext = nextLayer[Random.Range(0, nextLayer.Count)];
                if (!currentNode.nextNodes.Contains(randomNext))
                {
                    currentNode.nextNodes.Add(randomNext);
                    randomNext.prevNodes.Add(currentNode);
                }
            }
        }
    }

    NodeData.NodeType GetRandomNodeType(int layer)
    {
        if (layer == layers.Count - 1)
            return NodeData.NodeType.Combat;

        int roll = Random.Range(0, 100);
        if (roll < combatChance) return NodeData.NodeType.Combat;
        if (roll < combatChance + shopChance) return NodeData.NodeType.Shop;
        return NodeData.NodeType.RandomEvent;
    }

    void CalculatePositions()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            List<NodeData> layer = layers[i];
            float totalWidth = (layer.Count - 1) * nodeSpacing;
            float startX = -totalWidth / 2f;

            for (int j = 0; j < layer.Count; j++)
            {
                layer[j].position = new Vector2(
                    startX + j * nodeSpacing,
                    i * layerHeight
                );
            }
        }
    }

    public NodeData GetStartNode() => startNode;
    public NodeData GetBossNode() => bossNode;
    public List<List<NodeData>> GetLayers() => layers;
}