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

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.RefreshHand();

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

        if (BattleUI.Instance != null)
            BattleUI.Instance.gameObject.SetActive(false);
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.gameObject.SetActive(false);

        Vector3 playerFinalPos = playerObject.transform.position;
        Vector3 monsterFinalPos = monsterObject.transform.position;

        playerObject.transform.position = playerFinalPos + new Vector3(-15f, 0, 0);
        monsterObject.transform.position = monsterFinalPos + new Vector3(15f, 0, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * introSpeed;
            float smooth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            playerObject.transform.position = Vector3.Lerp(
                playerFinalPos + new Vector3(-15f, 0, 0), playerFinalPos, smooth);
            monsterObject.transform.position = Vector3.Lerp(
                monsterFinalPos + new Vector3(15f, 0, 0), monsterFinalPos, smooth);
            yield return null;
        }

        playerObject.transform.position = playerFinalPos;
        monsterObject.transform.position = monsterFinalPos;

        yield return new WaitForSeconds(0.3f);

        if (BattleUI.Instance != null)
            BattleUI.Instance.gameObject.SetActive(true);
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.gameObject.SetActive(true);

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

        if (PlayerHand.Instance != null)
            PlayerHand.Instance.RefreshHand();

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
    }

    // 드로우 카드인지 확인
    bool IsDrawCard(CardData card)
    {
        return card.effectType == CardData.CardEffectType.Draw;
    }

    public bool PlayCardOnMonster(CardData card)
    {
        if (!introComplete) return false;
        if (!hand.Contains(card)) return false;
        if (currentMana < card.manaCost)
        {
            Debug.Log("마나가 부족해!");
            return false;
        }

        currentMana -= card.manaCost;
        hand.Remove(card);

        // 드로우 카드는 효과 먼저, 그다음 버림으로
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
        if (currentMana < card.manaCost)
        {
            Debug.Log("마나가 부족해!");
            return false;
        }

        currentMana -= card.manaCost;
        hand.Remove(card);

        // 드로우 카드는 효과 먼저, 그다음 버림으로
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
            ChangeGaze(card.gazeChange);

        if (card.cardType == CardData.CardType.Forbidden && gazeLevel >= 75)
            usedForbiddenInCursedZone = true;

        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
                int damage = card.value + playerStrength;
                if (playerDebuffTurns > 0)
                    damage = Mathf.RoundToInt(damage * 0.75f);
                if (monsterDebuffTurns > 0)
                    damage = Mathf.RoundToInt(damage * 1.25f);
                int actualDamage = Mathf.Max(0, damage - monsterDefense);
                monsterDefense = Mathf.Max(0, monsterDefense - damage);
                monsterCurrentHp -= actualDamage;
                if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                Debug.Log($"{card.cardName} — 몬스터에게 {actualDamage} 데미지!");
                break;

            case CardData.CardEffectType.Shield:
                playerDefense += card.value;
                Debug.Log($"{card.cardName} — 방어도 {card.value} 획득!");
                break;

            case CardData.CardEffectType.Draw:
                DrawCards(card.value);
                Debug.Log($"{card.cardName} — 카드 {card.value}장 드로우!");
                break;

            case CardData.CardEffectType.GazeChange:
                Debug.Log($"{card.cardName} — 시선 {card.gazeChange}!");
                break;
        }

        CheckMonsterDeath();

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
    }

    public void EndTurn()
    {
        if (!introComplete) return;

        Debug.Log($"--- {turnCount}턴 종료 ---");

        if (usedForbiddenInCursedZone)
        {
            playerCurrentHp -= 2;
            usedForbiddenInCursedZone = false;
            Debug.Log("침식! 고정 피해 2");
            if (playerHitEffect != null) playerHitEffect.PlayHit();
            CheckPlayerDeath();
        }

        if (gazeLevel >= 100)
        {
            playerCurrentHp -= 20;
            monsterStrength += 3;
            gazeLevel = 40;
            Debug.Log("폭주! 고정 피해 20, 몬스터 영구 힘 +3, 시선 40으로 조정!");
            if (playerHitEffect != null) playerHitEffect.PlayHit();
            CheckPlayerDeath();
        }

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
        currentMana = maxMana;

        discardPile.AddRange(hand);
        hand.Clear();
        DrawCards(5);

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.OnTurnEnd();

        Debug.Log($"--- {turnCount}턴 시작 | 마나: {currentMana}/{maxMana} ---");
    }

    void MonsterTurn()
    {
        if (monsterNextAction == null) return;

        Debug.Log($"몬스터 행동: {monsterNextAction.actionType}");

        switch (monsterNextAction.actionType)
        {
            case MonsterData.ActionType.Attack:
                int damage = monsterNextAction.value + monsterStrength;

                if (gazeLevel >= 25 && gazeLevel < 50) damage += 1;
                else if (gazeLevel >= 50) damage += 2;

                int actualDamage = Mathf.Max(0, damage - playerDefense);
                playerDefense = Mathf.Max(0, playerDefense - damage);

                if (actualDamage > 0)
                {
                    playerCurrentHp -= actualDamage;
                    if (playerHitEffect != null) playerHitEffect.PlayHit();
                    Debug.Log($"몬스터 공격! {actualDamage} 데미지");
                }
                else
                {
                    Debug.Log("방어도로 완전 차단!");
                }
                CheckPlayerDeath();
                break;

            case MonsterData.ActionType.Defend:
                monsterDefense += monsterNextAction.value;
                Debug.Log($"몬스터 방어! 방어도 {monsterNextAction.value} 획득");
                break;

            case MonsterData.ActionType.Buff:
                monsterStrength += 5;
                monsterStrengthTurns = monsterNextAction.duration;
                Debug.Log($"몬스터 버프! {monsterNextAction.duration}턴 동안 공격력 +5");
                break;

            case MonsterData.ActionType.Debuff:
                playerDebuffTurns = monsterNextAction.duration;
                Debug.Log($"몬스터 디버프! {monsterNextAction.duration}턴 동안 플레이어 공격력 25% 감소");
                break;
        }

        monsterNextAction = monsterData.GetNextAction();

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateMonsterIntent();
    }

    public void ChangeGaze(int amount)
    {
        gazeLevel = Mathf.Clamp(gazeLevel + amount, 0, 100);
        Debug.Log($"시선 게이지: {gazeLevel}");

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
    }

    void CheckMonsterDeath()
    {
        if (monsterCurrentHp <= 0)
        {
            monsterCurrentHp = 0;
            Debug.Log("몬스터 처치!");

            if (GameManager.Instance != null)
                GameManager.Instance.playerCurrentHp = playerCurrentHp;

            UnityEngine.SceneManagement.SceneManager.LoadScene("RewardScene");
        }
    }

    void CheckPlayerDeath()
    {
        if (playerCurrentHp <= 0)
        {
            playerCurrentHp = 0;
            Debug.Log("플레이어 사망...");

            if (GameManager.Instance != null)
                GameManager.Instance.GameOver();
        }
    }
}