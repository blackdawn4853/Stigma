using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    public enum RoomType { Combat, Treasure, Shop, Heal, Boss }

    [Header("방 설정")]
    public int roomCount = 0;
    public RoomType currentRoomType;
    public bool hasKey = false;
    public bool keySpawnedThisFloor = false;

    [Header("플레이어")]
    public PlayerController player;
    public Transform playerStartPosition;

    [Header("화살표 UI")]
    public GameObject arrowUp;
    public GameObject arrowLeft;
    public GameObject arrowRight;

    [Header("방 타입별 확률 (%)")]
    public int combatChance = 50;
    public int treasureChance = 15;
    public int shopChance = 15;
    public int healChance = 20;

    private int bossRoomThreshold = 8;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        HideArrows();

        if (GameManager.Instance != null && GameManager.Instance.returningFromBattle)
        {
            GameManager.Instance.returningFromBattle = false;
            ShowArrows();
        }
        else
        {
            if (arrowUp != null) arrowUp.SetActive(true);
        }
    }

    public void EnterRoom(RoomType roomType)
    {
        currentRoomType = roomType;
        roomCount++;

        Debug.Log($"[Room {roomCount}] 입장: {roomType}");

        if (player != null && playerStartPosition != null)
            player.transform.position = playerStartPosition.position;

        HideArrows();

        switch (roomType)
        {
            case RoomType.Combat:
                StartCombatRoom();
                break;
            case RoomType.Treasure:
                StartTreasureRoom();
                break;
            case RoomType.Shop:
                GameManager.Instance.LoadShop();
                break;
            case RoomType.Heal:
                StartHealRoom();
                break;
            case RoomType.Boss:
                StartBossRoom();
                break;
        }
    }

    void StartCombatRoom()
    {
        Debug.Log("전투 방 시작!");
        if (BattleManager.Instance != null)
            BattleManager.Instance.InitializeBattlePublic();
    }

    void StartTreasureRoom()
    {
        Debug.Log("보물 방!");
        ShowArrows();
    }

    void StartHealRoom()
    {
        Debug.Log("회복 방!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerCurrentHp = Mathf.Min(
                GameManager.Instance.playerCurrentHp + 15,
                GameManager.Instance.playerMaxHp);
            Debug.Log($"HP 회복! {GameManager.Instance.playerCurrentHp}/{GameManager.Instance.playerMaxHp}");
        }
        ShowArrows();
    }

    void StartBossRoom()
    {
        Debug.Log("보스 방!");
        if (!hasKey)
        {
            Debug.Log("열쇠가 없어서 입장 불가!");
            ShowArrows();
            return;
        }
        if (BattleManager.Instance != null)
            BattleManager.Instance.InitializeBattlePublic();
    }

    public void OnCombatVictory()
    {
        Debug.Log("전투 승리! 화살표 표시");
        ShowArrows();
    }

    public void OnArrowClicked(string direction)
    {
        HideArrows();

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager가 없어!");
            return;
        }

        if (GameManager.Instance.currentRoomCount == 0)
        {
            GameManager.Instance.currentRoomCount++;
            GameManager.Instance.LoadBattle();
            return;
        }

        GameManager.Instance.currentRoomCount++;
        RoomType nextRoom = GetNextRoomType(direction);

        Vector3 targetPos = GetDoorPosition(direction);
        player.MoveTo(targetPos, () => EnterRoom(nextRoom));
    }

    RoomType GetNextRoomType(string direction)
    {
        if (roomCount >= bossRoomThreshold)
            return RoomType.Boss;

        int roll = Random.Range(0, 100);

        if (roll < combatChance)
            return RoomType.Combat;
        else if (roll < combatChance + treasureChance)
            return RoomType.Treasure;
        else if (roll < combatChance + treasureChance + shopChance)
            return RoomType.Shop;
        else
            return RoomType.Heal;
    }

    Vector3 GetDoorPosition(string direction)
    {
        Vector3 playerPos = player.transform.position;
        switch (direction)
        {
            case "Up":    return playerPos + new Vector3(0, 4, 0);
            case "Left":  return playerPos + new Vector3(-4, 0, 0);
            case "Right": return playerPos + new Vector3(4, 0, 0);
            default:      return playerPos;
        }
    }

    void ShowArrows()
    {
        if (arrowUp != null)    arrowUp.SetActive(true);
        if (arrowLeft != null)  arrowLeft.SetActive(true);
        if (arrowRight != null) arrowRight.SetActive(true);
    }

    void HideArrows()
    {
        if (arrowUp != null)    arrowUp.SetActive(false);
        if (arrowLeft != null)  arrowLeft.SetActive(false);
        if (arrowRight != null) arrowRight.SetActive(false);
    }
}