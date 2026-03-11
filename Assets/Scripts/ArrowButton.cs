using UnityEngine;

public class ArrowButton : MonoBehaviour
{
    public string direction;

    void OnMouseDown()
    {
        Debug.Log($"화살표 클릭: {direction}");

        if (DungeonGenerator.Instance == null)
        {
            Debug.LogError("DungeonGenerator 없음!");
            return;
        }

        Room currentRoom = DungeonGenerator.Instance.GetCurrentRoom();
        if (currentRoom == null)
        {
            Debug.LogError("현재 방 없음!");
            return;
        }

        Room targetRoom = null;

        switch (direction)
        {
            case "Up":    targetRoom = currentRoom.connectedUp;    break;
            case "Down":  targetRoom = currentRoom.connectedDown;  break;
            case "Left":  targetRoom = currentRoom.connectedLeft;  break;
            case "Right": targetRoom = currentRoom.connectedRight; break;
        }

        if (targetRoom != null)
            DungeonGenerator.Instance.MoveToRoom(targetRoom);
        else
            Debug.Log($"{direction} 방향에 연결된 방 없음!");
    }
}