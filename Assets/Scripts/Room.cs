using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    public enum RoomType { Start, Combat, Shop, Heal, Treasure, Boss }

    [Header("방 설정")]
    public RoomType roomType;
    public bool isCleared = false;

    [Header("문 연결")]
    public Door doorUp;
    public Door doorDown;
    public Door doorLeft;
    public Door doorRight;

    [Header("방 콘텐츠")]
    public GameObject monsterObject;    // 몬스터 오브젝트
    public GameObject shopObject;       // 상점 오브젝트

    // 이 방과 연결된 방들
    public Room connectedUp;
    public Room connectedDown;
    public Room connectedLeft;
    public Room connectedRight;

    void Start()
    {
        SetupRoom();
    }

    void SetupRoom()
    {
        // 방 타입에 따라 콘텐츠 활성화
        if (monsterObject != null)
            monsterObject.SetActive(roomType == RoomType.Combat);

        if (shopObject != null)
            shopObject.SetActive(roomType == RoomType.Shop);
    }

    // 문 활성화/비활성화
    public void SetDoors(bool up, bool down, bool left, bool right)
    {
        if (doorUp != null)    doorUp.gameObject.SetActive(up);
        if (doorDown != null)  doorDown.gameObject.SetActive(down);
        if (doorLeft != null)  doorLeft.gameObject.SetActive(left);
        if (doorRight != null) doorRight.gameObject.SetActive(right);
    }

    // 방 클리어
    public void ClearRoom()
    {
        isCleared = true;
        if (monsterObject != null)
            monsterObject.SetActive(false);

        // 문 열기
        OpenAllDoors();
        Debug.Log($"{roomType} 방 클리어!");
    }

    void OpenAllDoors()
    {
        if (doorUp != null)    doorUp.OpenDoor();
        if (doorDown != null)  doorDown.OpenDoor();
        if (doorLeft != null)  doorLeft.OpenDoor();
        if (doorRight != null) doorRight.OpenDoor();
    }
}