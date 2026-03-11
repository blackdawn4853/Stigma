using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("몬스터 다음 행동")]
    public CardData monsterNextCard;

    [Header("몬스터 설정")]
    public MonsterData monsterData;
    public int monsterCurrentHp;

    [Header("플레이어 설정")]
    public int playerCurrentHp;
    public int playerMaxHp;
    public int currentMana;
    public int maxMana;
    public int turnCount = 1;

    [Header("전투 상태")]
    public bool isShielded = false;
    public bool isDodging = false;
    public bool isEnemyWeakened = false;
    public int thornsValue = 0;
    public int poisonStacks = 0;

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
            playerCurrentHp = 50;
            playerMaxHp = 50;
        }

        monsterCurrentHp = monsterData.maxHp;
        maxMana = 3;
        currentMana = maxMana;

        deck.Clear();
        if (GameManager.Instance != null && GameManager.Instance.playerDeck.Count > 0)
        {
            foreach (CardData card in GameManager.Instance.playerDeck)
                deck.Add(card);
        }
        else
        {
            foreach (CardData card in startingCards)
                deck.Add(card);
        }

        ShuffleDeck();
        DrawCards(5);

        monsterNextCard = monsterData.GetNextCard(monsterCurrentHp);

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.RefreshHand();

        if (playerObject != null && monsterObject != null)
            StartCoroutine(IntroCoroutine());
        else
            introComplete = true;

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

    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discardPile.Count == 0) break;
                deck.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeck();
            }

            hand.Add(deck[0]);
            deck.RemoveAt(0);
        }
    }

    public bool PlayCard(CardData card)
    {
        if (!introComplete)
        {
            Debug.Log("인트로 중에는 카드 사용 불가!");
            return false;
        }

        if (!hand.Contains(card)) return false;
        if (currentMana < card.manaCost)
        {
            Debug.Log("마나가 부족해!");
            return false;
        }

        currentMana -= card.manaCost;
        ApplyCardEffect(card);
        hand.Remove(card);
        discardPile.Add(card);
        return true;
    }

    void ApplyCardEffect(CardData card)
    {
        if (!card.IsConditionMet(playerCurrentHp, playerMaxHp, monsterCurrentHp, monsterData.maxHp, currentMana))
        {
            Debug.Log($"{card.cardName} 사용 실패 — 조건 미충족!");
            currentMana += card.manaCost;
            return;
        }

        if (EffectManager.Instance != null)
        {
            bool targetIsMonster = card.effectType == CardData.CardEffectType.Damage ||
                                   card.effectType == CardData.CardEffectType.MultiHit ||
                                   card.effectType == CardData.CardEffectType.Execute ||
                                   card.effectType == CardData.CardEffectType.RageAttack ||
                                   card.effectType == CardData.CardEffectType.Poison ||
                                   card.effectType == CardData.CardEffectType.WeakenEnemy;

            EffectManager.Instance.PlayCardEffect(card, targetIsMonster);
        }

        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
                monsterCurrentHp -= card.value;
                if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                break;

            case CardData.CardEffectType.MultiHit:
                int totalDamage = card.value * card.hitCount;
                monsterCurrentHp -= totalDamage;
                if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                break;

            case CardData.CardEffectType.Execute:
                float monsterHpRatio = (float)monsterCurrentHp / monsterData.maxHp;
                if (monsterHpRatio <= card.conditionThreshold)
                {
                    monsterCurrentHp = 0;
                    if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                }
                else
                    currentMana += card.manaCost;
                break;

            case CardData.CardEffectType.RageAttack:
                float missingHpRatio = 1f - (float)playerCurrentHp / playerMaxHp;
                int rageDamage = card.value + Mathf.RoundToInt(card.value * missingHpRatio);
                monsterCurrentHp -= rageDamage;
                if (monsterHitEffect != null) monsterHitEffect.PlayHit();
                break;

            case CardData.CardEffectType.Shield:
                isShielded = true;
                break;

            case CardData.CardEffectType.Thorns:
                thornsValue = card.value;
                break;

            case CardData.CardEffectType.Dodge:
                isDodging = true;
                break;

            case CardData.CardEffectType.WeakenEnemy:
                isEnemyWeakened = true;
                break;

            case CardData.CardEffectType.Poison:
                poisonStacks += card.value;
                break;

            case CardData.CardEffectType.GainMana:
                currentMana = Mathf.Min(currentMana + card.value, maxMana);
                break;

            case CardData.CardEffectType.Heal:
                playerCurrentHp = Mathf.Min(playerCurrentHp + card.value, playerMaxHp);
                break;

            case CardData.CardEffectType.Taunt:
                Debug.Log($"{card.cardName} — 도발!");
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

        MonsterTurn();

        turnCount++;
        maxMana = Mathf.Min(3 + (turnCount - 1), 10);
        currentMana = maxMana;

        discardPile.AddRange(hand);
        hand.Clear();
        DrawCards(5);

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateUI();
        if (PlayerHand.Instance != null)
            PlayerHand.Instance.OnTurnEnd();
    }

    void MonsterTurn()
    {
        if (monsterNextCard == null) return;

        switch (monsterNextCard.effectType)
        {
            case CardData.CardEffectType.Damage:
            case CardData.CardEffectType.MultiHit:
                int damage = monsterNextCard.value;

                if (isDodging)
                {
                    isDodging = false;
                    Debug.Log("회피 성공!");
                    break;
                }

                if (isShielded)
                {
                    isShielded = false;
                    Debug.Log("방어막으로 차단!");
                    break;
                }

                if (thornsValue > 0)
                {
                    monsterCurrentHp -= thornsValue;
                    Debug.Log($"가시 반사! {thornsValue} 데미지!");
                }

                playerCurrentHp -= damage;
                if (playerHitEffect != null) playerHitEffect.PlayHit();
                CheckPlayerDeath();
                break;

            case CardData.CardEffectType.Taunt:
                Debug.Log("몬스터 도발!");
                break;
        }

        if (poisonStacks > 0)
        {
            monsterCurrentHp -= poisonStacks;
            poisonStacks--;
            CheckMonsterDeath();
        }

        monsterNextCard = monsterData.GetNextCard(monsterCurrentHp);

        if (BattleUI.Instance != null)
            BattleUI.Instance.UpdateMonsterIntent();
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