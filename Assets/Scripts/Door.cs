using UnityEngine;

public class Door : MonoBehaviour
{
    public enum DoorDirection { Up, Down, Left, Right }

    [Header("문 설정")]
    public DoorDirection direction;
    public bool isOpen = false;
    public Room connectedRoom;

    [Header("문 오브젝트")]
    public GameObject openVisual;   // 열린 문 스프라이트
    public GameObject closedVisual; // 닫힌 문 스프라이트

    void Start()
    {
        UpdateVisual();
    }

    public void OpenDoor()
    {
        isOpen = true;
        UpdateVisual();
    }

    public void CloseDoor()
    {
        isOpen = false;
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (openVisual != null)   openVisual.SetActive(isOpen);
        if (closedVisual != null) closedVisual.SetActive(!isOpen);
    }

    // 플레이어가 문에 닿으면 방 이동
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag("Player")) return;

        if (connectedRoom != null)
            DungeonGenerator.Instance.MoveToRoom(connectedRoom);
    }
}