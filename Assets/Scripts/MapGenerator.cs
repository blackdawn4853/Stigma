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
    public int maxNextNodes = 2;
    public int maxNodesPerLayer = 5;

    [Header("노드 타입 확률 (%)")]
    public int combatChance = 55;
    public int shopChance = 20;
    public int randomEventChance = 20;
    public int brandChance = 5;

    [Header("화면 설정")]
    public float layerHeight = 150f;
    public float nodeSpacing = 200f;

    [Header("노드 위치 흩뜨리기")]
    public float randomOffsetX = 60f;
    public float randomOffsetY = 30f;
    public float minNodeDistance = 120f;

    private List<List<NodeData>> layers = new List<List<NodeData>>();
    private NodeData startNode;
    private NodeData bossNode;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public List<List<NodeData>> GenerateMap()
    {
        layers.Clear();

        int totalLayers = Random.Range(minLayers, maxLayers + 1);

        List<NodeData> startLayer = new List<NodeData>();
        startNode = new NodeData();
        startNode.nodeType = NodeData.NodeType.Start;
        startNode.layer = 0;
        startNode.index = 0;
        startNode.isAccessible = true;
        startLayer.Add(startNode);
        layers.Add(startLayer);

        int firstLayerCount = Random.Range(minStartNodes, maxStartNodes + 1);
        List<NodeData> firstLayer = CreateLayer(1, firstLayerCount);
        layers.Add(firstLayer);

        for (int i = 2; i < totalLayers; i++)
        {
            int nodeCount = Random.Range(2, maxNodesPerLayer + 1);
            List<NodeData> layer = CreateLayer(i, nodeCount);
            layers.Add(layer);
        }

        List<NodeData> bossLayer = new List<NodeData>();
        bossNode = new NodeData();
        bossNode.nodeType = NodeData.NodeType.Boss;
        bossNode.layer = totalLayers;
        bossNode.index = 0;
        bossLayer.Add(bossNode);
        layers.Add(bossLayer);

        CalculatePositions();

        List<NodeData> sortedFirst = SortByX(layers[1]);
        foreach (var node in sortedFirst)
        {
            startNode.nextNodes.Add(node);
            node.prevNodes.Add(startNode);
        }

        for (int i = 1; i < layers.Count - 1; i++)
            ConnectLayers(layers[i], layers[i + 1]);

        Debug.Log($"맵 생성 완료! 총 층 수: {layers.Count}");

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

    List<NodeData> SortByX(List<NodeData> layer)
    {
        List<NodeData> sorted = new List<NodeData>(layer);
        sorted.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        return sorted;
    }

    void ConnectLayers(List<NodeData> currentLayer, List<NodeData> nextLayer)
    {
        List<NodeData> sortedCurrent = SortByX(currentLayer);
        List<NodeData> sortedNext = SortByX(nextLayer);

        for (int i = 0; i < sortedNext.Count; i++)
        {
            int mappedIndex = Mathf.RoundToInt((float)i / (sortedNext.Count - 0.99f) * (sortedCurrent.Count - 1));
            mappedIndex = Mathf.Clamp(mappedIndex, 0, sortedCurrent.Count - 1);

            NodeData from = sortedCurrent[mappedIndex];
            NodeData to = sortedNext[i];

            if (!from.nextNodes.Contains(to))
            {
                from.nextNodes.Add(to);
                to.prevNodes.Add(from);
            }
        }

        for (int i = 0; i < sortedCurrent.Count; i++)
        {
            if (sortedCurrent[i].nextNodes.Count == 0)
            {
                int mappedIndex = Mathf.RoundToInt((float)i / (sortedCurrent.Count - 0.99f) * (sortedNext.Count - 1));
                mappedIndex = Mathf.Clamp(mappedIndex, 0, sortedNext.Count - 1);
                NodeData to = sortedNext[mappedIndex];
                sortedCurrent[i].nextNodes.Add(to);
                to.prevNodes.Add(sortedCurrent[i]);
            }
        }

        for (int i = 0; i < sortedCurrent.Count; i++)
        {
            int extraConnections = Random.Range(0, maxNextNodes);
            for (int e = 0; e < extraConnections; e++)
            {
                int minIdx = Mathf.Max(0, Mathf.FloorToInt((float)i / sortedCurrent.Count * sortedNext.Count) - 1);
                int maxIdx = Mathf.Min(sortedNext.Count - 1, Mathf.CeilToInt((float)(i + 1) / sortedCurrent.Count * sortedNext.Count));
                int randomIdx = Random.Range(minIdx, maxIdx + 1);
                NodeData to = sortedNext[randomIdx];

                if (!sortedCurrent[i].nextNodes.Contains(to))
                {
                    sortedCurrent[i].nextNodes.Add(to);
                    to.prevNodes.Add(sortedCurrent[i]);
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
        if (roll < combatChance + shopChance + randomEventChance) return NodeData.NodeType.RandomEvent;
        return NodeData.NodeType.Brand;
    }

    void CalculatePositions()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            List<NodeData> layer = layers[i];
            float totalWidth = (layer.Count - 1) * nodeSpacing;
            float startX = -totalWidth / 2f;

            bool isStartOrBoss = (i == 0 || i == layers.Count - 1);

            for (int j = 0; j < layer.Count; j++)
            {
                float baseX = startX + j * nodeSpacing;
                float baseY = i * layerHeight;

                if (isStartOrBoss)
                {
                    layer[j].position = new Vector2(baseX, baseY);
                    continue;
                }

                Vector2 candidate = Vector2.zero;
                bool found = false;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float offsetX = Random.Range(-randomOffsetX, randomOffsetX);
                    float offsetY = Random.Range(-randomOffsetY, randomOffsetY);
                    candidate = new Vector2(baseX + offsetX, baseY + offsetY);

                    bool tooClose = false;
                    for (int k = 0; k < j; k++)
                    {
                        if (Vector2.Distance(candidate, layer[k].position) < minNodeDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose) { found = true; break; }
                }

                layer[j].position = found ? candidate : new Vector2(baseX, baseY);
            }
        }
    }

    public NodeData GetStartNode() => startNode;
    public NodeData GetBossNode() => bossNode;
    public List<List<NodeData>> GetLayers() => layers;
    public bool HasLayers() => layers != null && layers.Count > 0;

    // Load 직후 호출 — 직렬화된 노드 + 연결을 NodeData 그래프로 복원.
    public void RestoreFromSerialized(List<SerializableNode> serialNodes, int layerCount)
    {
        layers.Clear();
        startNode = null;
        bossNode = null;

        if (serialNodes == null || serialNodes.Count == 0 || layerCount <= 0)
            return;

        var byKey = new Dictionary<(int, int), NodeData>();
        var grouped = new Dictionary<int, List<NodeData>>();

        foreach (var sn in serialNodes)
        {
            NodeData nd = new NodeData();
            nd.nodeType = (NodeData.NodeType)sn.nodeType;
            nd.layer = sn.layer;
            nd.index = sn.index;
            nd.position = sn.position;
            nd.isVisited = sn.isVisited;
            nd.isAccessible = sn.isAccessible;
            byKey[(sn.layer, sn.index)] = nd;
            if (!grouped.ContainsKey(sn.layer)) grouped[sn.layer] = new List<NodeData>();
            grouped[sn.layer].Add(nd);
        }

        for (int i = 0; i < layerCount; i++)
        {
            if (grouped.TryGetValue(i, out var ld))
            {
                ld.Sort((a, b) => a.index.CompareTo(b.index));
                layers.Add(ld);
            }
            else layers.Add(new List<NodeData>());
        }

        // 연결 복원
        foreach (var sn in serialNodes)
        {
            if (!byKey.TryGetValue((sn.layer, sn.index), out var nd)) continue;
            int n = Mathf.Min(sn.nextLayers.Count, sn.nextIndices.Count);
            for (int i = 0; i < n; i++)
            {
                if (byKey.TryGetValue((sn.nextLayers[i], sn.nextIndices[i]), out var target))
                {
                    nd.nextNodes.Add(target);
                    target.prevNodes.Add(nd);
                }
            }
        }

        if (layers.Count > 0 && layers[0].Count > 0) startNode = layers[0][0];
        if (layers.Count > 0 && layers[layers.Count - 1].Count > 0)
            bossNode = layers[layers.Count - 1][0];

        Debug.Log($"[MapGenerator] 세이브 복원 — {layers.Count} 층, 노드 {serialNodes.Count}개");
    }
}