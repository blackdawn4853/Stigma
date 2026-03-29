using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeUI : MonoBehaviour
{
    [Header("UI 요소")]
    public Image nodeImage;
    public TextMeshProUGUI nodeTypeText;

    [Header("노드 색상")]
    public Color startColor = new Color(0.2f, 0.8f, 0.2f);
    public Color combatColor = new Color(0.8f, 0.2f, 0.2f);
    public Color shopColor = new Color(0.2f, 0.5f, 0.8f);
    public Color randomEventColor = new Color(0.8f, 0.7f, 0.1f);
    public Color bossColor = new Color(0.6f, 0.1f, 0.8f);
    public Color visitedColor = new Color(0.4f, 0.4f, 0.4f);
    public Color inaccessibleColor = new Color(0.2f, 0.2f, 0.2f);

    private NodeData nodeData;

    public void Setup(NodeData data)
    {
        nodeData = data;
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (nodeData == null) return;

        // 텍스트 설정
        if (nodeTypeText != null)
        {
            switch (nodeData.nodeType)
            {
                case NodeData.NodeType.Start:       nodeTypeText.text = "시작"; break;
                case NodeData.NodeType.Combat:      nodeTypeText.text = "전투"; break;
                case NodeData.NodeType.Shop:        nodeTypeText.text = "상점"; break;
                case NodeData.NodeType.RandomEvent: nodeTypeText.text = "이벤트"; break;
                case NodeData.NodeType.Boss:        nodeTypeText.text = "보스"; break;
            }
        }

        // 색상 설정
        if (nodeImage != null)
        {
            if (nodeData.isVisited)
                nodeImage.color = visitedColor;
            else if (!nodeData.isAccessible)
                nodeImage.color = inaccessibleColor;
            else
            {
                switch (nodeData.nodeType)
                {
                    case NodeData.NodeType.Start:       nodeImage.color = startColor; break;
                    case NodeData.NodeType.Combat:      nodeImage.color = combatColor; break;
                    case NodeData.NodeType.Shop:        nodeImage.color = shopColor; break;
                    case NodeData.NodeType.RandomEvent: nodeImage.color = randomEventColor; break;
                    case NodeData.NodeType.Boss:        nodeImage.color = bossColor; break;
                }
            }
        }
    }

    public void OnNodeClicked()
    {
        if (nodeData == null) return;
        if (!nodeData.isAccessible) return;
        if (nodeData.isVisited) return;

        MapSceneManager.Instance.OnNodeSelected(nodeData);
    }

    public NodeData GetNodeData() => nodeData;
}