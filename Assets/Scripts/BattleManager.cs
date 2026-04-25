using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("몬스터 설정")]
    public MonsterData monsterData;
    public int monsterCurrentHp;
    public MonsterData.MonsterAction monsterNextAction;
    public int monsterDefense = 0;
    public int monsterStrength = 0;
    public int monsterStrengthTurns = 0;
    public int monsterDebuffTurns = 0;

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
    public GameObject monsterObject;
    public float introSpeed = 3f;
    public HitEffect monsterHitEffect;
    public HitEffect playerHitEffect;

    private bool introComplete = false;
    private List<string> gazeChangeLog = new List<string>();
    private int nextTurnManaReduction = 0;
    private int regenHealAmount = 5;
    private int regenTurnsRemaining = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

        monsterCurrentHp = monsterData.maxHp;
        monsterDefense = 0;
        monsterStrength = 0;
        monsterStrengthTurns = 0;
        monsterDebuffTurns = 0;
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

        monsterNextAction = monsterData.GetNextAction();

        if (BattleUI.Instance != null) BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();

        if (playerObject != null && monsterObject != null)
            StartCoroutine(IntroCoroutine());
        else
        {
            introComplete = true;
            if (MonsterIntent.Instance != null)
                MonsterIntent.Instance.UpdateIntent(monsterNextAction);
        }

        Debug.Log("전투 시작!");
    }

    IEnumerator IntroCoroutine()
    {
        introComplete = false;

        if (BattleUI.Instance != null) BattleUI.Instance.gameObject.SetActive(false);
        if (PlayerHand.Instance != null) PlayerHand.Instance.gameObject.SetActive(false);

        Vector3 playerFinalPos = playerObject.transform.position;
        Vector3 monsterFinalPos = monsterObject.transform.position;

        playerObject.transform.position = playerFinalPos + new Vector3(-15f, 0, 0);
        monsterObject.transform.position = monsterFinalPos + new Vector3(15f, 0, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * introSpeed;
            float smooth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            playerObject.transform.position = Vector3.Lerp(playerFinalPos + new Vector3(-15f, 0, 0), playerFinalPos, smooth);
            monsterObject.transform.position = Vector3.Lerp(monsterFinalPos + new Vector3(15f, 0, 0), monsterFinalPos, smooth);
            yield return null;
        }

        playerObject.transform.position = playerFinalPos;
        monsterObject.transform.position = monsterFinalPos;

        yield return new WaitForSeconds(0.3f);

        if (BattleUI.Instance != null) BattleUI.Instance.gameObject.SetActive(true);
        if (PlayerHand.Instance != null) PlayerHand.Instance.gameObject.SetActive(true);

        if (MonsterIntent.Instance != null)
            MonsterIntent.Instance.UpdateIntent(monsterNextAction);

        introComplete = true;
        Debug.Log("인트로 완료!");
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

    public bool PlayCardOnMonster(CardData card)
    {
        if (!introComplete) return false;
        if (!hand.Contains(card)) return false;
        int cost = GetCardCost(card);
        if (currentMana < cost) { Debug.Log("마나가 부족해!"); return false; }

        currentMana -= cost;
        hand.Remove(card);

        if (GazeEffectManager.Instance != null)
            GazeEffectManager.Instance.OnCardPlayed(card, true);

        if (IsDrawCard(card))
        {
            ApplyCardEffect(card, true);
            discardPile.Add(card);
        }
        else
        {
            discardPile.Add(card);
            ApplyCardEffect(card, true);
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
            ApplyCardEffect(card, false);
            discardPile.Add(card);
        }
        else
        {
            discardPile.Add(card);
            ApplyCardEffect(card, false);
        }

        return true;
    }

    void ApplyCardEffect(CardData card, bool targetIsMonster)
    {
        if (card.gazeChange != 0)
            ChangeGaze(card.gazeChange, card.cardName);

        if (card.cardType == CardData.CardType.Forbidden && gazeLevel >= 75)
            usedForbiddenInCursedZone = true;

        int damage, actualDamage;

        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
                damage = CalculateDamage(card.value, card);
                actualDamage = ApplyDamageToMonster(damage, card);
                Debug.Log($"{card.cardName} — {actualDamage} 데미지!");
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
                damage = CalculateDamage(card.value, card);
                actualDamage = ApplyDamageToMonster(damage, card);
                playerDefense += GetCardShield(card, card.value2);
                Debug.Log($"{card.cardName} — {actualDamage} 데미지 + 방어도");
                break;

            case CardData.CardEffectType.MultiHit:
                int totalMulti = 0;
                for (int i = 0; i < card.value2; i++)
                {
                    damage = CalculateDamage(card.value, card);
                    totalMulti += ApplyDamageToMonster(damage, card);
                }
                Debug.Log($"{card.cardName} — {card.value2}회 공격, 총 {totalMulti} 데미지!");
                break;

            case CardData.CardEffectType.PenetratingDamage:
                damage = CalculateDamage(card.value, card);
                monsterCurrentHp -= damage;
                if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                if (GazeEffectManager.Instance != null)
                    GazeEffectManager.Instance.OnDamageDealt(card, damage);
                Debug.Log($"{card.cardName} — 관통! {damage} 데미지!");
                break;

            case CardData.CardEffectType.RandomDamage:
                int randDmg = Random.Range(card.value, card.value2 + 1);
                damage = CalculateDamage(randDmg, card);
                actualDamage = ApplyDamageToMonster(damage, card);
                Debug.Log($"{card.cardName} — 랜덤 {actualDamage} 데미지!");
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
                damage = CalculateDamage(card.value, card);
                actualDamage = ApplyDamageToMonster(damage, card);
                Debug.Log($"{card.cardName} — 전체 {actualDamage} 데미지!");
                break;

            case CardData.CardEffectType.AllMultiHit:
                int totalAll = 0;
                for (int i = 0; i < card.value2; i++)
                {
                    damage = CalculateDamage(card.value, card);
                    totalAll += ApplyDamageToMonster(damage, card);
                }
                Debug.Log($"{card.cardName} — 전체 {card.value2}회, 총 {totalAll} 데미지!");
                break;

            case CardData.CardEffectType.DamageSelfDamage:
                damage = CalculateDamage(card.value, card);
                actualDamage = ApplyDamageToMonster(damage, card);
                playerCurrentHp -= card.value2;
                if (playerHitEffect != null) playerHitEffect.PlayHit();
                regenTurnsRemaining = card.value3;
                Debug.Log($"{card.cardName} — {actualDamage} 데미지, 자해 {card.value2}, {card.value3}턴 재생!");
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

            ApplyCardEffect(randomCard, true);
            discardPile.Add(randomCard);

            yield return new WaitForSeconds(0.3f);
        }
    }

    int CalculateDamage(int baseDamage, CardData card = null)
    {
        int damage = baseDamage + playerStrength;
        if (card != null && GazeEffectManager.Instance != null)
        {
            damage += GazeEffectManager.Instance.GetFlatDamageBonus(card);
            damage = Mathf.RoundToInt(damage * GazeEffectManager.Instance.GetDamageMultiplier(card));
        }
        if (playerDebuffTurns > 0)
            damage = Mathf.RoundToInt(damage * 0.75f);
        if (monsterDebuffTurns > 0)
            damage = Mathf.RoundToInt(damage * 1.25f);
        return damage;
    }

    int ApplyDamageToMonster(int damage, CardData card = null)
    {
        bool ignoreDefense = card != null && GazeEffectManager.Instance != null
            && GazeEffectManager.Instance.IgnoresMonsterDefense(card);
        int actualDamage;
        if (ignoreDefense)
        {
            actualDamage = damage;
        }
        else
        {
            actualDamage = Mathf.Max(0, damage - monsterDefense);
            monsterDefense = Mathf.Max(0, monsterDefense - damage);
        }
        monsterCurrentHp -= actualDamage;
        if (actualDamage > 0 && monsterHitEffect != null)
            monsterHitEffect.PlayHit();
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
                monsterStrength += 3;
                gazeLevel = gazeResetValue;
            }
            if (playerHitEffect != null) playerHitEffect.PlayHit();
            CheckPlayerDeath();
        }

        // ✅ 턴 종료 시선 로그 표시
        if (BattleUI.Instance != null)
            BattleUI.Instance.ShowGazeLog(gazeChangeLog);
        gazeChangeLog.Clear();

        MonsterTurn();

        playerDefense = 0;
        monsterDefense = 0;

        if (playerStrengthTurns > 0) playerStrengthTurns--;
        if (playerStrengthTurns == 0) playerStrength = 0;
        if (monsterStrengthTurns > 0) monsterStrengthTurns--;
        if (monsterStrengthTurns == 0) monsterStrength = 0;
        if (monsterDebuffTurns > 0) monsterDebuffTurns--;
        if (playerDebuffTurns > 0) playerDebuffTurns--;

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
    }

    void MonsterTurn()
    {
        if (monsterNextAction == null) return;

        switch (monsterNextAction.actionType)
        {
            case MonsterData.ActionType.Attack:
                int damage = monsterNextAction.value + monsterStrength;

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

            case MonsterData.ActionType.Defend:
                monsterDefense += monsterNextAction.value;
                break;

            case MonsterData.ActionType.Buff:
                monsterStrength += 5;
                monsterStrengthTurns = monsterNextAction.duration;
                break;

            case MonsterData.ActionType.Debuff:
                playerDebuffTurns = monsterNextAction.duration;
                break;
        }

        monsterNextAction = monsterData.GetNextAction();

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateMonsterIntent();
    }

    public void ChangeGaze(int amount, string reason = "")
    {
        int before = gazeLevel;
        gazeLevel = Mathf.Clamp(gazeLevel + amount, 0, 100);
        int actual = gazeLevel - before;

        // ✅ 수정: "카드이름 +8" 형식으로 저장
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
        if (monsterCurrentHp <= 0)
        {
            monsterCurrentHp = 0;
            if (GazeEffectManager.Instance != null)
                GazeEffectManager.Instance.OnMonsterKilled();
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