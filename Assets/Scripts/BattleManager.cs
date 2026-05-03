using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("몬스터 (씬에 사전 배치된 Monster 컴포넌트들 - 인카운터 미설정 시 폴백)")]
    public List<Monster> monsters = new List<Monster>();
    public bool autoFindMonsters = true;

    [Header("인카운터 동적 스폰 (NextEncounter 가 있으면 씬 배치 무시)")]
    public GameObject monsterPrefab;
    public Transform encounterAnchor; // 비워두면 defaultAnchorPosition 사용
    public Vector3 defaultAnchorPosition = new Vector3(5f, -3f, 0f);
    [Tooltip("다중 몬스터 자동 가로 간격 (anchor 중심으로 좌우 분산)")]
    public float monsterSpacing = 2f;
    [Tooltip("몬스터 1마리 추가될 때마다 플레이어를 이만큼 왼쪽으로 밀어 공간 확보 (단독 시 0)")]
    public float playerLeftShiftPerExtraMonster = 0.7f;

    [Header("바닥 정렬 (카메라 뷰 기준 발 위치 자동 보정)")]
    [Tooltip("켜면 카메라 줌이 바뀌거나 몬스터 sprite 크기가 달라도 모두 같은 화면 바닥 라인에 발 정렬")]
    public bool autoAlignToFloor = true;
    [Tooltip("화면 하단에서 위로 얼마나 떨어진 지점을 발 라인으로 잡을지 (0.2 = 화면 하단 20% 지점)")]
    [Range(0f, 0.5f)] public float floorPaddingFraction = 0.2f;

    [Header("카메라 프레이밍 (몬스터 수에 따라 직교 사이즈 조정)")]
    public Camera battleCamera;
    public bool autoFindBattleCamera = true;
    // index 0 = 1마리, 1 = 2마리, 2 = 3마리, 3 = 4마리. 부족하면 마지막 값 사용.
    public float[] cameraSizeByMonsterCount = { 5f, 6f, 7f, 8f };

    [Header("플레이어 설정")]
    public int playerCurrentHp;
    public int playerMaxHp = 100;
    public int currentMana;
    public int maxMana = 3;
    public int playerDefense = 0;
    public int playerStrength = 0;
    public int playerStrengthTurns = 0;
    public int playerDebuffTurns = 0;
    public int turnCount = 1;

    [Header("시선 게이지")]
    public int gazeLevel = 0;
    public int gazeResetValue = 30;
    private bool usedForbiddenInCursedZone = false;

    [Header("덱 설정")]
    public List<CardData> deck = new List<CardData>();
    public List<CardData> hand = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();

    // 손패 카드별 인스턴스 ID (hand 와 병렬). 같은 CardData SO 가 여러 장 있어도 구분 가능.
    // 인스턴스 단위 비용 모디파이어 (40-4 틈새 시야 등) 가 정확한 카드만 타겟팅하기 위해 사용.
    public List<int> handCardIds = new List<int>();
    private int nextCardInstanceId = 1;

    [Header("테스트용 시작 카드")]
    public CardData[] startingCards;

    [Header("인트로 연출")]
    public GameObject playerObject;
    public float introSpeed = 3f;
    public HitEffect playerHitEffect;

    private bool introComplete = false;
    private Vector3 playerBasePosition;
    private bool playerBasePositionCached = false;
    private List<string> gazeChangeLog = new List<string>();
    private int nextTurnManaReduction = 0;
    private int regenHealAmount = 5;
    private int regenTurnsRemaining = 0;

    public int RegenTurnsRemaining => regenTurnsRemaining;

    // ─── 다중 몬스터 호환 헬퍼 ─────────────────────────────────────
    public Monster PrimaryMonster => GetFirstAlive() ?? (monsters.Count > 0 ? monsters[0] : null);
    public bool AnyMonsterAlive
    {
        get
        {
            for (int i = 0; i < monsters.Count; i++)
                if (monsters[i] != null && monsters[i].IsAlive) return true;
            return false;
        }
    }

    public Monster GetFirstAlive()
    {
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && monsters[i].IsAlive) return monsters[i];
        return null;
    }

    public List<Monster> GetAliveMonsters()
    {
        var list = new List<Monster>();
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && monsters[i].IsAlive) list.Add(monsters[i]);
        return list;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 인카운터가 설정돼있으면 씬 배치 몬스터를 모두 제거하고 인카운터로 스폰
        if (EncounterDatabase.NextEncounter != null)
        {
            DespawnSceneMonsters();
            SpawnEncounter(EncounterDatabase.NextEncounter);
            EncounterDatabase.NextEncounter = null; // 1회용
        }
        else if (autoFindMonsters && monsters.Count == 0)
        {
#if UNITY_2023_1_OR_NEWER
            var found = FindObjectsByType<Monster>(FindObjectsSortMode.InstanceID);
#else
            var found = FindObjectsOfType<Monster>();
#endif
            for (int i = 0; i < found.Length; i++) monsters.Add(found[i]);
        }
    }

    void DespawnSceneMonsters()
    {
#if UNITY_2023_1_OR_NEWER
        var found = FindObjectsByType<Monster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var found = FindObjectsOfType<Monster>(true);
#endif
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null) Destroy(found[i].gameObject);
        }
        monsters.Clear();
    }

    void SpawnEncounter(EncounterData encounter)
    {
        if (encounter == null || encounter.entries == null) return;
        if (monsterPrefab == null)
        {
            Debug.LogError("[BattleManager] monsterPrefab 미할당 — 인카운터 스폰 불가");
            return;
        }
        Vector3 anchor = encounterAnchor != null ? encounterAnchor.position : defaultAnchorPosition;

        // 유효 엔트리 카운트 (자동 가로 분산용)
        int validCount = 0;
        for (int i = 0; i < encounter.entries.Length; i++)
            if (encounter.entries[i] != null && encounter.entries[i].data != null) validCount++;

        int idx = 0;
        for (int i = 0; i < encounter.entries.Length; i++)
        {
            var entry = encounter.entries[i];
            if (entry == null || entry.data == null) continue;

            // 자동 가로 정렬: anchor 중심으로 좌우 균등 분산. (count-1)/2 를 빼서 가운데 정렬.
            // 단독이면 autoX=0, 2마리면 ±spacing/2, 3마리면 -spacing/0/+spacing 등.
            float autoX = (idx - (validCount - 1) * 0.5f) * monsterSpacing;
            // entry.positionOffset 는 인카운터별 미세 조정용 (자동 정렬 위에 더해짐)
            Vector3 pos = anchor
                        + new Vector3(autoX, 0f, 0f)
                        + new Vector3(entry.positionOffset.x, entry.positionOffset.y, 0f);
            idx++;

            GameObject go = Instantiate(monsterPrefab, pos, Quaternion.identity);
            go.name = entry.data.monsterName;
            var mono = go.GetComponent<Monster>();
            if (mono == null) mono = go.AddComponent<Monster>();
            mono.data = entry.data;
            AttachBossAI(go, entry.data);
            monsters.Add(mono);
        }
        Debug.Log($"[BattleManager] 인카운터 스폰: {encounter.encounterName} ({monsters.Count}마리, anchor={anchor}, spacing={monsterSpacing})");
    }

    // MonsterData.bossAIType 에 따라 해당 컴포넌트를 부착. 신규 보스 추가 시 case 만 늘리면 됨.
    void AttachBossAI(GameObject go, MonsterData data)
    {
        if (data == null) return;
        switch (data.bossAIType)
        {
            case MonsterData.BossAIType.None:
                return;
            case MonsterData.BossAIType.Shoggoth:
                if (go.GetComponent<ShoggothAI>() == null) go.AddComponent<ShoggothAI>();
                break;
        }
    }

    void Start()
    {
        InitializeBattlePublic();
    }

    public void InitializeBattlePublic()
    {
        if (GameManager.Instance != null)
        {
            playerCurrentHp = GameManager.Instance.playerCurrentHp;
            playerMaxHp = GameManager.Instance.playerMaxHp;
        }
        else
        {
            playerCurrentHp = playerMaxHp;
        }

        playerDefense = 0;
        playerStrength = 0;
        playerStrengthTurns = 0;
        playerDebuffTurns = 0;
        // 시선은 런 단위로 유지 — 전투 사이에도 GameManager.runGazeLevel 에서 이어받음.
        gazeLevel = GameManager.Instance != null ? GameManager.Instance.runGazeLevel : 0;
        usedForbiddenInCursedZone = false;
        maxMana = 3;
        currentMana = maxMana;
        nextTurnManaReduction = 0;
        regenTurnsRemaining = 0;
        gazeChangeLog.Clear();

        // 몬스터 초기화 — sprite override / scale 적용 (위치는 아직 spawn 그대로)
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            if (!monsters[i].gameObject.activeSelf) monsters[i].gameObject.SetActive(true);
            monsters[i].InitializeForBattle();
        }

        ApplyCameraFraming();
        ApplyPlayerShift();
        AlignAllToFloor();

        // 모든 위치 보정 끝난 후 최종 위치 캐시 (인트로 코루틴이 이 위치로 슬라이드 인)
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null) monsters[i].CacheFinalPosition();

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.InitializeBattle();

        deck.Clear();
        hand.Clear();
        handCardIds.Clear();
        discardPile.Clear();
        nextCardInstanceId = 1;

        if (GameManager.Instance != null && GameManager.Instance.playerDeck.Count > 0)
        {
            foreach (CardData card in GameManager.Instance.playerDeck)
                deck.Add(card);
        }
        else
        {
            foreach (CardData card in startingCards)
                if (card != null) deck.Add(card);
        }

        ShuffleDeck();
        DrawCards(5);

        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();

        if (playerObject != null && monsters.Count > 0)
            StartCoroutine(IntroCoroutine());
        else
        {
            introComplete = true;
            RefreshAllIntents();
        }

        Debug.Log($"전투 시작! 몬스터 {monsters.Count}마리");
    }

    IEnumerator IntroCoroutine()
    {
        introComplete = false;

        if (BattleUI.Instance != null) BattleUI.Instance.gameObject.SetActive(false);
        if (PlayerHand.Instance != null) PlayerHand.Instance.gameObject.SetActive(false);

        Vector3 playerFinalPos = playerObject.transform.position;
        Vector3 playerStartPos = playerFinalPos + new Vector3(-15f, 0, 0);
        playerObject.transform.position = playerStartPos;

        // 각 몬스터 시작 위치 저장
        var startPositions = new Vector3[monsters.Count];
        var finalPositions = new Vector3[monsters.Count];
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            finalPositions[i] = monsters[i].FinalPosition;
            float offset = Mathf.Max(0.1f, monsters[i].introEnterOffsetX);
            startPositions[i] = finalPositions[i] + new Vector3(offset, 0, 0);
            monsters[i].transform.position = startPositions[i];
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * introSpeed;
            float smooth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            playerObject.transform.position = Vector3.Lerp(playerStartPos, playerFinalPos, smooth);
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] == null) continue;
                monsters[i].transform.position = Vector3.Lerp(startPositions[i], finalPositions[i], smooth);
            }
            yield return null;
        }

        playerObject.transform.position = playerFinalPos;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            monsters[i].transform.position = finalPositions[i];
        }

        yield return new WaitForSeconds(0.3f);

        if (BattleUI.Instance != null) BattleUI.Instance.gameObject.SetActive(true);
        if (PlayerHand.Instance != null) PlayerHand.Instance.gameObject.SetActive(true);

        RefreshAllIntents();

        introComplete = true;
        Debug.Log("인트로 완료!");
    }

    // 몬스터 수에 따라 플레이어 위치를 왼쪽으로 밀어 공간 확보.
    // 첫 호출 시 인스펙터/씬에 배치된 위치를 base 로 캐시 → 이후엔 base 기준으로 매번 재계산.
    void ApplyPlayerShift()
    {
        if (playerObject == null) return;
        if (!playerBasePositionCached)
        {
            playerBasePosition = playerObject.transform.position;
            playerBasePositionCached = true;
        }

        int count = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null) count++;
        float shift = Mathf.Max(0, count - 1) * playerLeftShiftPerExtraMonster;

        playerObject.transform.position = new Vector3(
            playerBasePosition.x - shift,
            playerBasePosition.y,
            playerBasePosition.z);
    }

    // 카메라 뷰 기준 "발 라인" Y 좌표. 카메라 ortho 사이즈가 바뀌면 자동으로 화면 하단 비율에 맞춰 이동.
    float ComputeFloorY()
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) return 0f;
        // 화면 바닥 = cam.y - ortho. 패딩만큼 위로 올림.
        return cam.transform.position.y + cam.orthographicSize * (2f * floorPaddingFraction - 1f);
    }

    void AlignAllToFloor()
    {
        if (!autoAlignToFloor) return;
        float floorY = ComputeFloorY();

        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            AlignTransformToFloor(monsters[i].transform, floorY);
        }
        if (playerObject != null)
            AlignTransformToFloor(playerObject.transform, floorY);

        Debug.Log($"[BattleManager] floor 정렬 — floorY={floorY:F2} (cam ortho={ (battleCamera != null ? battleCamera : Camera.main)?.orthographicSize ?? 0f:F2})");
    }

    // 대상 Transform 의 자식 SpriteRenderer 의 월드 bounds 의 바닥(min.y)이 floorY 가 되도록 Y 만 평행이동.
    // 스프라이트 크기/scale/pivot 무관 — 항상 발(스프라이트 바닥)이 floor 라인에 맞춰짐.
    void AlignTransformToFloor(Transform t, float floorY)
    {
        var sr = t.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Bounds b = sr.bounds; // world space, 현재 transform 반영됨
        float currentBottom = b.min.y;
        float delta = floorY - currentBottom;
        Vector3 p = t.position;
        t.position = new Vector3(p.x, p.y + delta, p.z);
    }

    void ApplyCameraFraming()
    {
        if (battleCamera == null && autoFindBattleCamera) battleCamera = Camera.main;
        if (battleCamera == null || !battleCamera.orthographic) return;
        if (cameraSizeByMonsterCount == null || cameraSizeByMonsterCount.Length == 0) return;

        int count = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null) count++;
        if (count <= 0) return;

        int idx = Mathf.Clamp(count - 1, 0, cameraSizeByMonsterCount.Length - 1);
        float targetSize = cameraSizeByMonsterCount[idx];
        battleCamera.orthographicSize = targetSize;
        Debug.Log($"[BattleManager] 카메라 프레이밍: 몬스터 {count}마리 → orthographicSize {targetSize}");
    }

    void RefreshAllIntents()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null || !monsters[i].IsAlive) continue;
            monsters[i].EnsureRuntimeUI();
            if (monsters[i].runtimeUI != null) monsters[i].runtimeUI.UpdateIntent();
        }
    }

    void ShuffleDeck()
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            CardData temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    void ReshuffleDeck()
    {
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
        Debug.Log("덱 리셔플!");
    }

    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discardPile.Count == 0) break;
                ReshuffleDeck();
            }
            hand.Add(deck[0]);
            handCardIds.Add(nextCardInstanceId++);
            deck.RemoveAt(0);
        }

        if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();
        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
    }

    // 외부에서 손패에 카드를 추가할 때 (외신의 강림, 미션 보상 등) — 인스턴스 ID 자동 부여.
    // 반환값: 부여된 인스턴스 ID.
    public int AddToHandWithId(CardData card)
    {
        if (card == null) return 0;
        int id = nextCardInstanceId++;
        hand.Add(card);
        handCardIds.Add(id);
        return id;
    }

    // 인스턴스 ID 우선 매칭 (같은 SO 중복 시 정확한 카드 식별), 0 이면 첫 매칭 fallback.
    int FindHandIndex(CardData card, int instanceId)
    {
        if (instanceId > 0)
        {
            for (int i = 0; i < handCardIds.Count; i++)
                if (handCardIds[i] == instanceId) return i;
        }
        return hand.IndexOf(card);
    }

    void RemoveFromHandAt(int index)
    {
        if (index < 0 || index >= hand.Count) return;
        hand.RemoveAt(index);
        if (index < handCardIds.Count) handCardIds.RemoveAt(index);
    }

    bool IsDrawCard(CardData card)
    {
        return card.effectType == CardData.CardEffectType.Draw ||
               card.effectType == CardData.CardEffectType.ShieldAndDraw ||
               card.effectType == CardData.CardEffectType.DrawAndReduceMana;
    }

    int GetCardCost(CardData card, int instanceId = 0)
    {
        return GazeEffectManager.Instance != null
            ? GazeEffectManager.Instance.GetEffectiveCost(card, instanceId)
            : card.manaCost;
    }

    public bool PlayCardOnMonster(CardData card, Monster target, int instanceId = 0)
    {
        if (!introComplete) return false;
        int handIndex = FindHandIndex(card, instanceId);
        if (handIndex < 0) return false;
        if (target == null || !target.IsAlive)
        {
            Debug.Log("유효한 몬스터 타겟이 아니야!");
            return false;
        }
        int cost = GetCardCost(card, instanceId);
        if (currentMana < cost) { Debug.Log("마나가 부족해!"); return false; }

        currentMana -= cost;
        RemoveFromHandAt(handIndex);

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnCardPlayed(card, true);

        if (IsDrawCard(card))
        {
            ApplyCardEffect(card, target);
            discardPile.Add(card);
        }
        else
        {
            discardPile.Add(card);
            ApplyCardEffect(card, target);
        }

        return true;
    }

    public bool PlayCardOnField(CardData card, int instanceId = 0)
    {
        if (!introComplete) return false;
        int handIndex = FindHandIndex(card, instanceId);
        if (handIndex < 0) return false;
        int cost = GetCardCost(card, instanceId);
        if (currentMana < cost) { Debug.Log("마나가 부족해!"); return false; }

        currentMana -= cost;
        RemoveFromHandAt(handIndex);

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnCardPlayed(card, false);

        if (IsDrawCard(card))
        {
            ApplyCardEffect(card, null);
            discardPile.Add(card);
        }
        else
        {
            discardPile.Add(card);
            ApplyCardEffect(card, null);
        }

        return true;
    }

    void ApplyCardEffect(CardData card, Monster target)
    {
        if (card.gazeChange != 0)
            ChangeGaze(card.gazeChange, card.cardName);

        if (card.cardType == CardData.CardType.Forbidden && gazeLevel >= 75)
            usedForbiddenInCursedZone = true;

        int damage, actualDamage;

        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
                damage = CalculateDamage(card.value, card, target);
                actualDamage = ApplyDamageToMonster(damage, card, target);
                Debug.Log($"{card.cardName} → {NameOf(target)} {actualDamage} 데미지!");
                break;

            case CardData.CardEffectType.Shield:
                playerDefense += GetCardShield(card, card.value);
                Debug.Log($"{card.cardName} — 방어도 적용");
                break;

            case CardData.CardEffectType.Draw:
                DrawCards(card.value);
                break;

            case CardData.CardEffectType.GazeChange:
                break;

            case CardData.CardEffectType.DamageAndShield:
                damage = CalculateDamage(card.value, card, target);
                actualDamage = ApplyDamageToMonster(damage, card, target);
                playerDefense += GetCardShield(card, card.value2);
                Debug.Log($"{card.cardName} → {NameOf(target)} {actualDamage} 데미지 + 방어도");
                break;

            case CardData.CardEffectType.MultiHit:
                int totalMulti = 0;
                for (int i = 0; i < card.value2; i++)
                {
                    if (target == null || !target.IsAlive) break;
                    damage = CalculateDamage(card.value, card, target);
                    totalMulti += ApplyDamageToMonster(damage, card, target);
                }
                Debug.Log($"{card.cardName} → {NameOf(target)} {card.value2}회, 총 {totalMulti} 데미지!");
                break;

            case CardData.CardEffectType.PenetratingDamage:
                damage = CalculateDamage(card.value, card, target);
                if (target != null) target.DirectDamage(damage);
                if (target != null && card != null && GazeEffectManager.Instance != null)
                    GazeEffectManager.Instance.OnDamageDealt(card, damage);
                Debug.Log($"{card.cardName} → {NameOf(target)} 관통! {damage} 데미지!");
                break;

            case CardData.CardEffectType.RandomDamage:
                int randDmg = Random.Range(card.value, card.value2 + 1);
                damage = CalculateDamage(randDmg, card, target);
                actualDamage = ApplyDamageToMonster(damage, card, target);
                Debug.Log($"{card.cardName} → {NameOf(target)} 랜덤 {actualDamage} 데미지!");
                break;

            case CardData.CardEffectType.StrengthBuff:
                playerStrength += card.value;
                playerStrengthTurns = card.value2;
                Debug.Log($"{card.cardName} — {card.value2}턴 동안 힘 +{card.value}!");
                break;

            case CardData.CardEffectType.DrawAndReduceMana:
                DrawCards(card.value);
                nextTurnManaReduction += card.value2;
                Debug.Log($"{card.cardName} — {card.value}장 드로우, 다음 턴 마나 -{card.value2}!");
                break;

            case CardData.CardEffectType.ShieldAndDraw:
                playerDefense += GetCardShield(card, card.value);
                DrawCards(card.value2);
                Debug.Log($"{card.cardName} — 방어도 + {card.value2}장 드로우!");
                break;

            case CardData.CardEffectType.Heal:
                playerCurrentHp = Mathf.Min(playerCurrentHp + card.value, playerMaxHp);
                if (card.value2 > 0)
                {
                    playerMaxHp -= card.value2;
                    playerCurrentHp = Mathf.Min(playerCurrentHp, playerMaxHp);
                }
                Debug.Log($"{card.cardName} — 체력 {card.value} 회복, 최대체력 -{card.value2}!");
                break;

            case CardData.CardEffectType.AllDamage:
            {
                int totalAll = 0;
                var alive = GetAliveMonsters();
                foreach (var m in alive)
                {
                    damage = CalculateDamage(card.value, card, m);
                    totalAll += ApplyDamageToMonster(damage, card, m);
                }
                Debug.Log($"{card.cardName} — 전체 {alive.Count}타겟, 총 {totalAll} 데미지!");
                break;
            }

            case CardData.CardEffectType.AllMultiHit:
            {
                int totalMHit = 0;
                var alive = GetAliveMonsters();
                for (int i = 0; i < card.value2; i++)
                {
                    foreach (var m in alive)
                    {
                        if (!m.IsAlive) continue;
                        damage = CalculateDamage(card.value, card, m);
                        totalMHit += ApplyDamageToMonster(damage, card, m);
                    }
                }
                Debug.Log($"{card.cardName} — 전체 {card.value2}회, 총 {totalMHit} 데미지!");
                break;
            }

            case CardData.CardEffectType.DamageSelfDamage:
                damage = CalculateDamage(card.value, card, target);
                actualDamage = ApplyDamageToMonster(damage, card, target);
                playerCurrentHp -= card.value2;
                if (playerHitEffect != null) playerHitEffect.PlayHit();
                regenTurnsRemaining = card.value3;
                Debug.Log($"{card.cardName} → {NameOf(target)} {actualDamage} 데미지, 자해 {card.value2}, {card.value3}턴 재생!");
                CheckPlayerDeath();
                break;

            case CardData.CardEffectType.RandomCardUse:
                StartCoroutine(RandomCardUseCoroutine(card.value));
                break;
        }

        CheckMonsterDeath();

        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
    }

    string NameOf(Monster m) => m != null ? m.DisplayName : "(없음)";

    IEnumerator RandomCardUseCoroutine(int count)
    {
        List<CardData> allCards = new List<CardData>(deck);
        allCards.AddRange(discardPile);

        for (int i = 0; i < count && allCards.Count > 0; i++)
        {
            int idx = Random.Range(0, allCards.Count);
            CardData randomCard = allCards[idx];
            allCards.RemoveAt(idx);

            deck.Remove(randomCard);
            discardPile.Remove(randomCard);

            // 단일 타겟 카드면 무작위 살아있는 몬스터에게, 아니면 필드용으로 처리
            Monster t = null;
            if (randomCard.requiresTarget)
            {
                var alive = GetAliveMonsters();
                if (alive.Count > 0) t = alive[Random.Range(0, alive.Count)];
            }
            ApplyCardEffect(randomCard, t);
            discardPile.Add(randomCard);

            yield return new WaitForSeconds(0.3f);
        }
    }

    int CalculateDamage(int baseDamage, CardData card, Monster target)
    {
        int damage = baseDamage + playerStrength;
        if (card != null && GazeEffectManager.Instance != null)
        {
            damage += GazeEffectManager.Instance.GetFlatDamageBonus(card, target);
            damage = Mathf.RoundToInt(damage * GazeEffectManager.Instance.GetDamageMultiplier(card, target));
        }
        if (playerDebuffTurns > 0)
        {
            int before = damage;
            damage = Mathf.RoundToInt(damage * 0.75f);
            string cardName = card != null ? card.cardName : "?";
            Debug.Log($"[약화 적용] {cardName} 데미지 {before} → {damage} (25% 감소, 남은 턴 {playerDebuffTurns})");
        }
        if (target != null && target.debuffTurns > 0)
            damage = Mathf.RoundToInt(damage * 1.25f);
        return damage;
    }

    int ApplyDamageToMonster(int damage, CardData card, Monster target)
    {
        if (target == null || !target.IsAlive) return 0;
        bool ignoreDefense = card != null && GazeEffectManager.Instance != null
            && GazeEffectManager.Instance.IgnoresMonsterDefense(card, target);
        int actualDamage = target.TakeDamage(damage, ignoreDefense);
        if (card != null && GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnDamageDealt(card, actualDamage);
        return actualDamage;
    }

    int GetCardShield(CardData card, int baseShield)
    {
        int shield = baseShield;
        if (GazeEffectManager.Instance != null)
        {
            shield += GazeEffectManager.Instance.GetFlatShieldBonus(card);
            shield = Mathf.RoundToInt(shield * GazeEffectManager.Instance.GetShieldMultiplier());
        }
        return Mathf.Max(0, shield);
    }

    // 카드 UI 가 손패에서 표시할 "현재 시선/근력/약화 보정이 반영된 데미지/방어도" 미리보기.
    // target=null 이라 타겟 의존 효과(약점추적/파멸계약 등)는 제외 — 대상이 정해진 후 실제 적용 시 추가됨.
    public int PreviewCardDamage(CardData card)
    {
        if (card == null) return 0;
        int baseV = GazeEffectManager.GetCardBaseDamageValue(card);
        if (baseV <= 0) return 0;
        return CalculateDamage(baseV, card, null);
    }

    public int PreviewCardShield(CardData card)
    {
        if (card == null) return 0;
        int baseV = GazeEffectManager.GetCardBaseShieldValue(card);
        if (baseV <= 0) return 0;
        return GetCardShield(card, baseV);
    }

    private bool isEndingTurn = false;

    public void EndTurn()
    {
        if (!introComplete) return;
        if (isEndingTurn) return; // 보스 멀티히트 처리 중 더블클릭 방지
        StartCoroutine(EndTurnRoutine());
    }

    IEnumerator EndTurnRoutine()
    {
        isEndingTurn = true;
        Debug.Log($"--- {turnCount}턴 종료 ---");

        if (regenTurnsRemaining > 0)
        {
            playerCurrentHp = Mathf.Min(playerCurrentHp + regenHealAmount, playerMaxHp);
            regenTurnsRemaining--;
        }

        if (usedForbiddenInCursedZone)
        {
            playerCurrentHp -= 2;
            usedForbiddenInCursedZone = false;
            if (playerHitEffect != null) playerHitEffect.PlayHit();
            CheckPlayerDeath();
        }

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnTurnEnd();

        if (gazeLevel >= 100)
        {
            if (GazeEffectManager.Instance != null)
            {
                GazeEffectManager.Instance.TriggerGaze100();
            }
            else
            {
                playerCurrentHp -= 20;
                foreach (var m in GetAliveMonsters()) m.ApplyStrength(3, 99);
                gazeLevel = gazeResetValue;
            }
            if (playerHitEffect != null) playerHitEffect.PlayHit();
            CheckPlayerDeath();
        }

        if (BattleUI.Instance != null)
            BattleUI.Instance.ShowGazeLog(gazeChangeLog);
        gazeChangeLog.Clear();

        // 플레이어 버프/디버프 턴수 감소: MonsterTurn 이전에 처리해야
        // 이번 MonsterTurn 에서 새로 걸리는 디버프(약화 등)가 즉시 0턴으로 사라지지 않는다.
        if (playerStrengthTurns > 0) playerStrengthTurns--;
        if (playerStrengthTurns == 0) playerStrength = 0;
        if (playerDebuffTurns > 0) playerDebuffTurns--;

        yield return StartCoroutine(MonsterTurnRoutine());

        // 플레이어 방어도 리셋 (이번 MonsterTurn 동안 방어 흡수에 쓰임)
        playerDefense = 0;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null || !monsters[i].IsAlive) continue;
            monsters[i].EndOfTurnCleanup();
        }

        turnCount++;
        currentMana = Mathf.Max(0, maxMana - nextTurnManaReduction);
        nextTurnManaReduction = 0;

        discardPile.AddRange(hand);
        hand.Clear();
        handCardIds.Clear();
        DrawCards(5);

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnTurnStart();

        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null) PlayerHand.Instance.OnTurnEnd();
        RefreshAllIntents();
        isEndingTurn = false;
    }

    IEnumerator MonsterTurnRoutine()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || !m.IsAlive) continue;
            m.BeginTurn();

            if (m.bossAI != null)
            {
                yield return StartCoroutine(m.bossAI.ExecuteAction(m, this));
            }
            else
            {
                if (m.nextAction == null) continue;
                ExecuteMonsterAction(m);
            }
            if (playerCurrentHp <= 0) break;
        }

        // 다음 턴 행동 결정
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || !m.IsAlive) continue;
            if (m.bossAI != null) m.bossAI.PrepareNextAction(m);
            else m.nextAction = m.PickNextAction();
        }
    }

    void ExecuteMonsterAction(Monster m)
    {
        var action = m.nextAction;
        switch (action.actionType)
        {
            case MonsterData.ActionType.Attack:
            {
                int damage = action.value + m.strength;
                if (GazeEffectManager.Instance != null)
                    damage += GazeEffectManager.Instance.GetMonsterBonusAttack();

                int actualDamage = Mathf.Max(0, damage - playerDefense);
                playerDefense = Mathf.Max(0, playerDefense - damage);

                if (actualDamage > 0)
                {
                    playerCurrentHp -= actualDamage;
                    if (playerHitEffect != null) playerHitEffect.PlayHit();
                }
                CheckPlayerDeath();
                break;
            }
            case MonsterData.ActionType.Defend:
                m.AddDefense(action.value);
                Debug.Log($"[{m.DisplayName}] 방어도 +{action.value}, 현재 방어도: {m.defense}");
                break;
            case MonsterData.ActionType.Buff:
                m.ApplyStrength(5, action.duration);
                break;
            case MonsterData.ActionType.Debuff:
                playerDebuffTurns = Mathf.Max(playerDebuffTurns, action.duration);
                break;
            case MonsterData.ActionType.AttackAndDebuff:
            {
                int damage = action.value + m.strength;
                if (GazeEffectManager.Instance != null)
                    damage += GazeEffectManager.Instance.GetMonsterBonusAttack();
                int actualDamage = Mathf.Max(0, damage - playerDefense);
                playerDefense = Mathf.Max(0, playerDefense - damage);
                if (actualDamage > 0)
                {
                    playerCurrentHp -= actualDamage;
                    if (playerHitEffect != null) playerHitEffect.PlayHit();
                }
                playerDebuffTurns = Mathf.Max(playerDebuffTurns, action.duration);
                CheckPlayerDeath();
                break;
            }
        }
    }

    public void ChangeGaze(int amount, string reason = "")
    {
        int before = gazeLevel;
        gazeLevel = Mathf.Clamp(gazeLevel + amount, 0, 100);
        int actual = gazeLevel - before;

        if (actual != 0 && reason != "")
        {
            string sign = actual > 0 ? "+" : "";
            gazeChangeLog.Add($"{reason} {sign}{actual}");
        }

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnGazeChanged(actual);

        if (BattleUI.Instance != null)
        {
            BattleUI.Instance.FlashGazeBar(amount > 0);
            BattleUI.Instance.UpdateUI();
        }
    }

    void CheckMonsterDeath()
    {
        bool anyKilled = false;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            if (m.currentHp <= 0 && m.gameObject.activeSelf)
            {
                m.currentHp = 0;
                anyKilled = true;
                if (GazeEffectManager.Instance != null)
                    GazeEffectManager.Instance.OnMonsterKilled();
                m.Die();
            }
        }

        if (anyKilled && !AnyMonsterAlive)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.playerCurrentHp = playerCurrentHp;
                GameManager.Instance.runGazeLevel = gazeLevel; // 런 단위 시선 유지
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("RewardScene");
        }
    }

    void CheckPlayerDeath()
    {
        if (playerCurrentHp <= 0)
        {
            if (GazeEffectManager.Instance != null && GazeEffectManager.Instance.IsDeathProtected)
            {
                playerCurrentHp = 1;
                return;
            }
            playerCurrentHp = 0;
            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }

    // BossAI 등 외부에서 데미지 적용 후 사망 체크용
    public void CheckPlayerDeathPublic() => CheckPlayerDeath();
}
