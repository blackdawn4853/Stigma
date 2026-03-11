using UnityEngine;

public class MapNode : MonoBehaviour
{
    public enum NodeType { Battle, Shop, Heal }
    public NodeType nodeType;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            if (hit != null && hit.gameObject == gameObject)
            {
                OnNodeClicked();
            }
        }
    }

    void OnNodeClicked()
    {
        switch (nodeType)
        {
            case NodeType.Battle:
                GameManager.Instance.LoadBattle();
                break;
            case NodeType.Shop:
                GameManager.Instance.LoadShop();
                break;
            case NodeType.Heal:
                GameManager.Instance.LoadHeal();
                break;
        }
    }
}