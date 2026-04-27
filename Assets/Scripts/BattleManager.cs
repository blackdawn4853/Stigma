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
    public Transform encounterAnchor; // 비워두면 (0, -1, 0) 기준
    public Vector3 defaultAnchorPosition = new Vector3(5f, -1f, 0f);

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

    [Header("테스트용 시작 카드")]
    public CardData[] startingCards;

    [Header("인트로 연출")]
    public GameObject playerObject;
    public float introSpeed = 3f;
    public HitEffect playerHitEffect;

    private bool introComplete = false;
    private List<string> gazeChangeLog = new List<string>();
    private int nextTurnManaReduction = 0;
    private int regenHealAmount = 5;
    private int regenTurnsRemaining = 0;

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
        for (int i = 0; i < encounter.entries.Length; i++)
        {
            var entry = encounter.entries[i];
            if (entry == null || entry.data == null) continue;
            Vector3 pos = anchor + new Vector3(entry.positionOffset.x, entry.positionOffset.y, 0f);
            GameObject go = Instantiate(monsterPrefab, pos, Quaternion.identity);
            go.name = entry.data.monsterName;
            var mono = go.GetComponent<Monster>();
            if (mono == null) mono = go.AddComponent<Monster>();
            mono.data = entry.data;
            monsters.Add(mono);
        }
        Debug.Log($"[BattleManager] 인카운터 스폰: {encounter.encounterName} ({monsters.Count}마리)");
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
        gazeLevel = 0;
        usedForbiddenInCursedZone = false;
        maxMana = 3;
        currentMana = maxMana;
        nextTurnManaReduction = 0;
        regenTurnsRemaining = 0;
        gazeChangeLog.Clear();

        // 몬스터 초기화 (사망 상태 GameObject 도 활성화 시 부활)
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null) continue;
            if (!monsters[i].gameObject.activeSelf) monsters[i].gameObject.SetActive(true);
            monsters[i].CacheFinalPosition();
            monsters[i].InitializeForBattle();
        }

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.InitializeBattle();

        deck.Clear();
        hand.Clear();
        discardPile.Clear();

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
            deck.RemoveAt(0);
        }

        if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();
        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
    }

    bool IsDrawCard(CardData card)
    {
        return card.effectType == CardData.CardEffectType.Draw ||
               card.effectType == CardData.CardEffectType.ShieldAndDraw ||
               card.effectType == CardData.CardEffectType.DrawAndReduceMana;
    }

    int GetCardCost(CardData card)
    {
        return GazeEffectManager.Instance != null
            ? GazeEffectManager.Instance.GetEffectiveCost(card)
            : card.manaCost;
    }

    public bool PlayCardOnMonster(CardData card, Monster target)
    {
        if (!introComplete) return false;
        if (!hand.Contains(card)) return false;
        if (target == null || !target.IsAlive)
        {
            Debug.Log("유효한 몬스터 타겟이 아니야!");
            return false;
        }
        int cost = GetCardCost(card);
        if (currentMana < cost) { Debug.Log("마나가 부족해!"); return false; }

        currentMana -= cost;
        hand.Remove(card);

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

    public bool PlayCardOnField(CardData card)
    {
        if (!introComplete) return false;
        if (!hand.Contains(card)) return false;
        int cost = GetCardCost(card);
        if (currentMana < cost) { Debug.Log("마나가 부족해!"); return false; }

        currentMana -= cost;
        hand.Remove(card);

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

            case CardData.CardEffectType.ImmunityShield:
                playerDefense += GetCardShield(card, card.value);
                playerDebuffTurns = 0;
                Debug.Log($"{card.cardName} — 방어도 + 디버프 면역!");
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
            damage = Mathf.RoundToInt(damage * 0.75f);
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

    public void EndTurn()
    {
        if (!introComplete) return;

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

        MonsterTurn();

        // 플레이어/몬스터 방어 리셋, 턴 감소
        playerDefense = 0;
        if (playerStrengthTurns > 0) playerStrengthTurns--;
        if (playerStrengthTurns == 0) playerStrength = 0;
        if (playerDebuffTurns > 0) playerDebuffTurns--;
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
        DrawCards(5);

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnTurnStart();

        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null) PlayerHand.Instance.OnTurnEnd();
        RefreshAllIntents();
    }

    void MonsterTurn()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || !m.IsAlive || m.nextAction == null) continue;
            ExecuteMonsterAction(m);
            if (playerCurrentHp <= 0) break;
        }

        // 다음 턴 행동 결정
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || !m.IsAlive) continue;
            m.nextAction = m.PickNextAction();
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
                GameManager.Instance.playerCurrentHp = playerCurrentHp;
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
}
