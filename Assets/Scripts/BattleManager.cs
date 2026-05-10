using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("몬스터 (씬에 사전 배치된 Monster 컴포넌트들 - 인카운터 미설정 시 폴백)")]
    public List<Monster> monsters = new List<Monster>();
    [Tooltip("씬에 미리 배치된 Monster 컴포넌트를 자동 검색하여 monsters 리스트에 채움 (인카운터 미설정 시).")]
    public bool autoFindMonsters = true;

    [Header("몬스터 배치 (인스펙터에서 직접 조정)")]
    [Tooltip("인카운터 동적 스폰 시 사용할 몬스터 프리팹 (MonsterBase 등)")]
    public GameObject monsterPrefab;
    [Tooltip("몬스터 행의 중심 좌표. 이 위치를 기준으로 좌우 분산되어 스폰. Y 가 곧 몬스터의 발 라인.")]
    public Vector3 monsterAnchorPosition = new Vector3(5f, -1f, 0f);
    [Tooltip("다중 몬스터 시 좌우 간격 (월드 단위). 2마리: ±spacing/2, 3마리: -spacing/0/+spacing")]
    public float monsterSpacing = 1.2f;

    [Header("카메라")]
    public Camera battleCamera;
    public bool autoFindBattleCamera = true;
    [Tooltip("켜면 몬스터 수에 따라 카메라 ortho 사이즈 자동 변경. 끄면 씬에 잡은 카메라 그대로 — 플레이어/몬스터가 보이는 크기 동일.")]
    public bool autoApplyCameraFraming = false;
    [Tooltip("autoApplyCameraFraming 켰을 때만 사용. index 0 = 1마리, 1 = 2마리, 2 = 3마리, 3 = 4마리. 부족하면 마지막 값 사용.")]
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

    [Header("플레이어 (씬에서 위치 직접 조정 — 인스펙터 Transform 값이 곧 시작/최종 위치)")]
    [Tooltip("플레이어 GameObject. 위치는 이 GameObject 의 Transform 인스펙터에서 직접 잡으세요 — 코드는 건드리지 않습니다.")]
    public GameObject playerObject;
    [Tooltip("인트로 슬라이드 인 연출 속도 (1=1초)")]
    public float introSpeed = 3f;
    [Tooltip("끄면 슬라이드 인 연출 스킵 — 시작부터 최종 위치에 등장")]
    public bool playIntroSlide = true;
    public HitEffect playerHitEffect;

    [Header("캐릭터 Parallax")]
    [Tooltip("플레이어/몬스터에 적용할 parallaxFactor. 0 이면 캐릭터 고정 (배경만 패럴럭스). Floor 레이어와 같은 값을 유지해야 발 안 뜸.")]
    [Range(0f, 1f)] public float characterParallaxFactor = 0f;

    private bool introComplete = false;
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

        // 하단 HUD 바 + 플레이어 HP 바 자동 부트스트랩
        BottomHudBar.EnsureForBattle();
        PlayerHpBarUI.EnsureForBattle();
        MouseCameraController.EnsureForBattle();

        // 플레이어에 ParallaxLayer 부착 (인트로 동안 비활성, 끝난 후 활성화됨)
        if (playerObject != null) AttachCharacterParallax(playerObject);

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

        // 유효 엔트리 카운트 (자동 가로 분산용)
        int validCount = 0;
        for (int i = 0; i < encounter.entries.Length; i++)
            if (encounter.entries[i] != null && encounter.entries[i].data != null) validCount++;

        int idx = 0;
        for (int i = 0; i < encounter.entries.Length; i++)
        {
            var entry = encounter.entries[i];
            if (entry == null || entry.data == null) continue;

            // 인스펙터 monsterAnchorPosition 중심으로 좌우 균등 분산.
            // 단독이면 autoX=0, 2마리면 ±spacing/2, 3마리면 -spacing/0/+spacing 등.
            float autoX = (idx - (validCount - 1) * 0.5f) * monsterSpacing;
            // entry.positionOffset 는 인카운터별 미세 조정용 (자동 정렬 위에 더해짐)
            Vector3 pos = monsterAnchorPosition
                        + new Vector3(autoX, 0f, 0f)
                        + new Vector3(entry.positionOffset.x, entry.positionOffset.y, 0f);
            idx++;

            GameObject go = Instantiate(monsterPrefab, pos, Quaternion.identity);
            go.name = entry.data.monsterName;
            var mono = go.GetComponent<Monster>();
            if (mono == null) mono = go.AddComponent<Monster>();
            mono.data = entry.data;
            AttachBossAI(go, entry.data);
            AttachCharacterParallax(go);
            monsters.Add(mono);
            Debug.Log($"[몬스터 {idx}] 위치: ({pos.x:F2}, {pos.y:F2}) — {entry.data.monsterName}");
        }
        // 새로 부착된 ParallaxLayer 들을 카메라가 추적하도록 갱신
        if (MouseCameraController.Instance != null)
            MouseCameraController.Instance.RefreshLayerCache();
        Debug.Log($"[BattleManager] 인카운터 스폰: {encounter.encounterName} ({monsters.Count}마리, anchor={monsterAnchorPosition}, spacing={monsterSpacing})");
    }

    // 캐릭터(플레이어/몬스터)에 ParallaxLayer 부착 — factor=1 로 floor 와 같이 카메라 따라가게.
    // depth=0 으로 옛날 전체 레이어 쉐이크에선 빠지고, intensityMultiplier=1 로 신규 타겟형 쉐이크엔 정상 반응.
    // active=false 로 시작 — 인트로 슬라이드 끝난 뒤 EngageNow() 로 활성.
    void AttachCharacterParallax(GameObject go)
    {
        if (go == null) return;
        var pl = go.GetComponent<ParallaxLayer>();
        if (pl == null) pl = go.AddComponent<ParallaxLayer>();
        pl.parallaxFactor = characterParallaxFactor; // floor 와 같은 값이어야 발 안 뜸
        pl.depth = 0f;
        pl.intensityMultiplier = 1f;
        pl.active = false;
    }

    void EngageCharacterParallax()
    {
        if (playerObject != null)
        {
            var pl = playerObject.GetComponent<ParallaxLayer>();
            if (pl != null) pl.EngageNow();
        }
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            var pl = monsters[i].GetComponent<ParallaxLayer>();
            if (pl != null) pl.EngageNow();
        }
        if (MouseCameraController.Instance != null)
            MouseCameraController.Instance.RefreshLayerCache();
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

    // 플레이어 데미지 적용 — HP 차감 + 히트 플래시 + 데미지 팝업 + 다키스트 던전 스타일 클로즈업.
    // 모든 플레이어 피해는 이 헬퍼를 통해 들어와야 효과가 일관됨.
    public void DamagePlayer(int amount)
    {
        if (amount <= 0) return;
        playerCurrentHp -= amount;
        if (playerHitEffect != null) playerHitEffect.PlayHit();

        if (CombatEffectsManager.Instance != null)
        {
            Vector3 pos = playerObject != null ? playerObject.transform.position : Vector3.zero;
            CombatEffectsManager.Instance.ShowDamagePopup(pos, amount);
        }

        if (CombatCameraEffect.Instance != null && playerObject != null)
        {
            var sr = playerObject.GetComponent<SpriteRenderer>();
            CombatCameraEffect.Instance.PlayerHitCloseup(playerObject.transform, sr);
        }
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

        // 모든 위치 보정 끝난 후 최종 위치 캐시 (인트로 코루틴이 이 위치로 슬라이드 인)
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null) monsters[i].CacheFinalPosition();

        if (playerObject != null)
            Debug.Log($"[플레이어] 시작 위치: ({playerObject.transform.position.x:F2}, {playerObject.transform.position.y:F2})");

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
            EngageCharacterParallax();
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
        Vector3 playerStartPos = playIntroSlide ? playerFinalPos + new Vector3(-15f, 0, 0) : playerFinalPos;
        playerObject.transform.position = playerStartPos;

        // 각 몬스터 시작 위치 저장
        var startPositions = new Vector3[monsters.Count];
        var finalPositions = new Vector3[monsters.Count];
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            finalPositions[i] = monsters[i].FinalPosition;
            float offset = Mathf.Max(0.1f, monsters[i].introEnterOffsetX);
            startPositions[i] = playIntroSlide ? finalPositions[i] + new Vector3(offset, 0, 0) : finalPositions[i];
            monsters[i].transform.position = startPositions[i];
        }

        if (playIntroSlide)
        {
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

        // 인트로 끝 — 플레이어/몬스터 ParallaxLayer 활성화 (현재 위치를 origin 으로 캡처).
        EngageCharacterParallax();

        introComplete = true;
        Debug.Log($"[플레이어] 최종 위치: ({playerFinalPos.x:F2}, {playerFinalPos.y:F2})");
    }

    void ApplyCameraFraming()
    {
        if (!autoApplyCameraFraming) return;
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

    // ─── 에디터 미리보기 ────────────────────────────────────────────
    // BattleManager 인스펙터 우상단 점 3개 메뉴 또는 우클릭 → "Preview: 인카운터 스폰" 으로 호출.
    // EncounterDatabase.NextEncounter 가 설정돼있으면 그것을, 없으면 monsters 리스트에 미리 잡혀있는 데이터로 스폰.
    // 에디터 모드에서 Instantiate — 씬 하이어라키에 일반 GameObject 로 남으므로 Play 안 눌러도 위치/스케일 확인 가능.
    [ContextMenu("Preview: 인카운터 스폰")]
    void EditorSpawnPreview()
    {
        ClearPreview();
        if (monsterPrefab == null)
        {
            Debug.LogWarning("[BattleManager] Preview 스폰 — monsterPrefab 미할당");
            return;
        }

        var encounter = EncounterDatabase.NextEncounter;
        int count = 0;
        if (encounter != null && encounter.entries != null)
        {
            int validCount = 0;
            for (int i = 0; i < encounter.entries.Length; i++)
                if (encounter.entries[i] != null && encounter.entries[i].data != null) validCount++;

            int idx = 0;
            for (int i = 0; i < encounter.entries.Length; i++)
            {
                var entry = encounter.entries[i];
                if (entry == null || entry.data == null) continue;
                float autoX = (idx - (validCount - 1) * 0.5f) * monsterSpacing;
                Vector3 pos = monsterAnchorPosition
                            + new Vector3(autoX, 0f, 0f)
                            + new Vector3(entry.positionOffset.x, entry.positionOffset.y, 0f);
                idx++;
                SpawnPreviewOne(entry.data, pos);
                count++;
            }
        }
        else
        {
            // 폴백: monsters 리스트의 data 만으로 미리보기
            int valid = 0;
            for (int i = 0; i < monsters.Count; i++)
                if (monsters[i] != null && monsters[i].data != null) valid++;
            int idx = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m == null || m.data == null) continue;
                float autoX = (idx - (valid - 1) * 0.5f) * monsterSpacing;
                Vector3 pos = monsterAnchorPosition + new Vector3(autoX, 0f, 0f);
                idx++;
                SpawnPreviewOne(m.data, pos);
                count++;
            }
        }

        Debug.Log($"[BattleManager] Preview 스폰 완료 — {count}마리. MonsterData.visualScale 조정 후 다시 호출하면 갱신됨.");
    }

    void SpawnPreviewOne(MonsterData data, Vector3 pos)
    {
#if UNITY_EDITOR
        var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(monsterPrefab);
        if (go == null) go = Instantiate(monsterPrefab);
#else
        var go = Instantiate(monsterPrefab);
#endif
        go.name = $"_Preview_{data.monsterName}";
        go.transform.position = pos;
        var mono = go.GetComponent<Monster>();
        if (mono == null) mono = go.AddComponent<Monster>();
        mono.data = data;
        mono.ApplyVisualOverride();
    }

    [ContextMenu("Preview: 클리어")]
    void ClearPreview()
    {
#if UNITY_2023_1_OR_NEWER
        var found = FindObjectsByType<Monster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var found = FindObjectsOfType<Monster>(true);
#endif
        int removed = 0;
        for (int i = 0; i < found.Length; i++)
        {
            var go = found[i].gameObject;
            if (go.name.StartsWith("_Preview_"))
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
                removed++;
            }
        }
        if (removed > 0) Debug.Log($"[BattleManager] Preview 클리어 — {removed}마리 제거");
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
                DamagePlayer(card.value2);
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
            DamagePlayer(2);
            usedForbiddenInCursedZone = false;
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
                DamagePlayer(20);
                foreach (var m in GetAliveMonsters()) m.ApplyStrength(3, 99);
                gazeLevel = gazeResetValue;
            }
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
                    DamagePlayer(actualDamage);
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
                    DamagePlayer(actualDamage);
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
