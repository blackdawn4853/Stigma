using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    [Header("방 프리팹")]
    public GameObject startRoomPrefab;
    public GameObject combatRoomPrefab;
    public GameObject shopRoomPrefab;

    [Header("화살표")]
    public GameObject arrowUp;
    public GameObject arrowDown;
    public GameObject arrowLeft;
    public GameObject arrowRight;

    [Header("플레이어")]
    public PlayerController player;

    [Header("던전 설정")]
    public int dungeonSize = 5;
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    private Dictionary<Vector2Int, Room> roomMap = new Dictionary<Vector2Int, Room>();
    private Room currentRoom;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        SpawnRoom(Vector2Int.zero, Room.RoomType.Start);
        SpawnRoom(new Vector2Int(0, 1), Room.RoomType.Combat);

        List<Vector2Int> occupied = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(0, 1) };
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 2; i < dungeonSize; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                List<Vector2Int> validRooms = new List<Vector2Int>(occupied);
                validRooms.Remove(Vector2Int.zero);

                Vector2Int randomExisting = validRooms[Random.Range(0, validRooms.Count)];
                Vector2Int randomDir = directions[Random.Range(0, directions.Length)];
                Vector2Int newPos = randomExisting + randomDir;

                if (newPos == new Vector2Int(0, -1)) continue;
                if (newPos == new Vector2Int(-1, 0)) continue;
                if (newPos == new Vector2Int(1, 0)) continue;

                if (!roomMap.ContainsKey(newPos))
                {
                    Room.RoomType type = GetRandomRoomType(i);
                    SpawnRoom(newPos, type);
                    occupied.Add(newPos);
                    break;
                }
            }
        }

        ConnectRooms();

        foreach (var r in roomMap.Values)
            r.gameObject.SetActive(false);

        if (GameManager.Instance != null && GameManager.Instance.returningFromBattle)
        {
            GameManager.Instance.returningFromBattle = false;
            Vector2Int savedPos = GameManager.Instance.savedRoomGridPos;

            if (roomMap.ContainsKey(savedPos))
            {
                currentRoom = roomMap[savedPos];
                currentRoom.isCleared = true;
                currentRoom.gameObject.SetActive(true);
                CenterCameraOnRoom(currentRoom);
                if (player != null) player.transform.position = currentRoom.transform.position;
                UpdateDoorConnections();
                UpdateArrows();
                Debug.Log("전투 후 복귀! 화살표 표시");
            }
        }
        else
        {
            currentRoom = roomMap[Vector2Int.zero];
            currentRoom.gameObject.SetActive(true);
            CenterCameraOnRoom(currentRoom);
            if (player != null) player.transform.position = currentRoom.transform.position;
            UpdateArrows();
        }
    }

    void CenterCameraOnRoom(Room room)
    {
        Camera.main.transform.position = new Vector3(
            room.transform.position.x,
            room.transform.position.y,
            -10f);
    }

    Room.RoomType GetRandomRoomType(int index)
    {
        if (index == dungeonSize - 1) return Room.RoomType.Shop;

        int roll = Random.Range(0, 100);
        if (roll < 60) return Room.RoomType.Combat;
        if (roll < 80) return Room.RoomType.Shop;
        return Room.RoomType.Combat;
    }

    void SpawnRoom(Vector2Int gridPos, Room.RoomType type)
    {
        GameObject prefab = startRoomPrefab;
        if (type == Room.RoomType.Combat && combatRoomPrefab != null)
            prefab = combatRoomPrefab;
        else if (type == Room.RoomType.Shop && shopRoomPrefab != null)
            prefab = shopRoomPrefab;

        if (prefab == null)
        {
            Debug.LogError("방 프리팹이 없어!");
            return;
        }

        Vector3 worldPos = new Vector3(gridPos.x * roomWidth, gridPos.y * roomHeight, 0);
        GameObject roomObj = Instantiate(prefab, worldPos, Quaternion.identity);
        Room room = roomObj.GetComponent<Room>();
        room.roomType = type;
        roomMap[gridPos] = room;

        Debug.Log($"방 생성: {type} at {gridPos} (world: {worldPos})");
    }

    void ConnectRooms()
    {
        foreach (var kvp in roomMap)
        {
            Vector2Int pos = kvp.Key;
            Room room = kvp.Value;

            bool up    = roomMap.ContainsKey(pos + Vector2Int.up);
            bool down  = roomMap.ContainsKey(pos + Vector2Int.down);
            bool left  = roomMap.ContainsKey(pos + Vector2Int.left);
            bool right = roomMap.ContainsKey(pos + Vector2Int.right);

            if (up)    room.connectedUp    = roomMap[pos + Vector2Int.up];
            if (down)  room.connectedDown  = roomMap[pos + Vector2Int.down];
            if (left)  room.connectedLeft  = roomMap[pos + Vector2Int.left];
            if (right) room.connectedRight = roomMap[pos + Vector2Int.right];

            bool shouldOpen = room.isCleared || room.roomType == Room.RoomType.Start;
            room.SetDoors(
                up    && (shouldOpen || up),
                down  && (shouldOpen || down),
                left  && (shouldOpen || left),
                right && (shouldOpen || right)
            );
        }
    }

    public void MoveToRoom(Room targetRoom)
    {
        if (CameraTransition.Instance != null && CameraTransition.Instance.IsTransitioning()) return;

        HideAllArrows();

        string direction = GetDirection(targetRoom);

        targetRoom.gameObject.SetActive(true);

        if (CameraTransition.Instance != null && direction != "")
        {
            CameraTransition.Instance.StartTransition(direction, player, () =>
            {
                if (currentRoom != null)
                    currentRoom.gameObject.SetActive(false);

                currentRoom = targetRoom;
                CenterCameraOnRoom(currentRoom);
                player.transform.position = currentRoom.transform.position;

                Debug.Log($"{currentRoom.roomType} 방 입장!");

                if (currentRoom.roomType == Room.RoomType.Combat && !currentRoom.isCleared)
                    StartCoroutine(StartCombatDelay());
                else if (currentRoom.roomType == Room.RoomType.Shop)
                    Debug.Log("상점 입장!");
                else
                    UpdateArrows();

                UpdateDoorConnections();
            });
        }
        else
        {
            EnterRoom(targetRoom);
        }
    }

    string GetDirection(Room targetRoom)
    {
        if (currentRoom == null) return "";
        if (targetRoom == currentRoom.connectedUp)    return "Up";
        if (targetRoom == currentRoom.connectedDown)  return "Down";
        if (targetRoom == currentRoom.connectedLeft)  return "Left";
        if (targetRoom == currentRoom.connectedRight) return "Right";
        return "";
    }

    void HideAllArrows()
    {
        if (arrowUp != null)    arrowUp.SetActive(false);
        if (arrowDown != null)  arrowDown.SetActive(false);
        if (arrowLeft != null)  arrowLeft.SetActive(false);
        if (arrowRight != null) arrowRight.SetActive(false);
    }

    void EnterRoom(Room targetRoom)
    {
        if (currentRoom != null)
            currentRoom.gameObject.SetActive(false);

        currentRoom = targetRoom;
        currentRoom.gameObject.SetActive(true);

        CenterCameraOnRoom(currentRoom);
        if (player != null) player.transform.position = currentRoom.transform.position;

        Debug.Log($"{currentRoom.roomType} 방 입장!");

        if (currentRoom.roomType == Room.RoomType.Combat && !currentRoom.isCleared)
            StartCoroutine(StartCombatDelay());
        else if (currentRoom.roomType == Room.RoomType.Shop)
            Debug.Log("상점 입장!");
        else
            UpdateArrows();

        UpdateDoorConnections();
    }

    void UpdateDoorConnections()
    {
        if (currentRoom.doorUp != null)
            currentRoom.doorUp.connectedRoom = currentRoom.connectedUp;
        if (currentRoom.doorDown != null)
            currentRoom.doorDown.connectedRoom = currentRoom.connectedDown;
        if (currentRoom.doorLeft != null)
            currentRoom.doorLeft.connectedRoom = currentRoom.connectedLeft;
        if (currentRoom.doorRight != null)
            currentRoom.doorRight.connectedRoom = currentRoom.connectedRight;
    }

    IEnumerator StartCombatDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (currentRoom.monsterObject != null)
        {
            Vector3 monsterPos = currentRoom.monsterObject.transform.position;
            Vector3 originalCamPos = Camera.main.transform.position;
            float originalSize = Camera.main.orthographicSize;

            // 클로즈업
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                float smooth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
                Camera.main.transform.position = Vector3.Lerp(
                    originalCamPos,
                    new Vector3(monsterPos.x, monsterPos.y, -10f),
                    smooth);
                Camera.main.orthographicSize = Mathf.Lerp(originalSize, 3f, smooth);
                yield return null;
            }

            // 위협 애니메이션 트리거
            Animator monsterAnim = currentRoom.monsterObject.GetComponent<Animator>();
            if (monsterAnim != null)
                monsterAnim.SetTrigger("Threat");

            // 애니메이션 재생 시간 대기 (조정 가능)
            yield return new WaitForSeconds(1.5f);
        }

        GameManager.Instance.savedRoomGridPos = GetGridPos(currentRoom);
        GameManager.Instance.LoadBattle();
    }

    Vector2Int GetGridPos(Room room)
    {
        foreach (var kvp in roomMap)
            if (kvp.Value == room) return kvp.Key;
        return Vector2Int.zero;
    }

    public void OnCombatVictory()
    {
        if (currentRoom != null)
            currentRoom.ClearRoom();
    }

    void UpdateArrows()
    {
        Debug.Log($"UpdateArrows - Up:{currentRoom.connectedUp != null} Down:{currentRoom.connectedDown != null} Left:{currentRoom.connectedLeft != null} Right:{currentRoom.connectedRight != null}");

        if (arrowUp != null)    arrowUp.SetActive(currentRoom.connectedUp != null);
        if (arrowDown != null)  arrowDown.SetActive(currentRoom.connectedDown != null);
        if (arrowLeft != null)  arrowLeft.SetActive(currentRoom.connectedLeft != null);
        if (arrowRight != null) arrowRight.SetActive(currentRoom.connectedRight != null);
    }

    public Room GetCurrentRoom()
    {
        return currentRoom;
    }
}