using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GazeEffectManager : MonoBehaviour
{
    public static GazeEffectManager Instance { get; private set; }

    // ─── 효과 풀 (Inspector에서 4개씩 배정) ────────────────────────────
    [Header("20 구간 효과 풀")] public GazeEffectData[] pool20 = new GazeEffectData[4];
    [Header("40 구간 효과 풀")] public GazeEffectData[] pool40 = new GazeEffectData[4];
    [Header("60 구간 효과 풀")] public GazeEffectData[] pool60 = new GazeEffectData[4];
    [Header("80 구간 효과 풀")] public GazeEffectData[] pool80 = new GazeEffectData[4];
    [Header("100 구간 효과 풀")] public GazeEffectData[] pool100 = new GazeEffectData[4];

    [Header("100-1 외신의 강림용 크툴루 카드 풀")]
    public CardData[] forbiddenCardPool;

    [Header("미션 UI (100-4)")]
    public GameObject missionPanel;
    public TextMeshProUGUI missionTitleText;
    public TextMeshProUGUI missionConditionText;
    public TextMeshProUGUI missionRewardText;
    public Image missionImage;

    // ─── 효과 수치 (튜닝용 변수) ──────────────────────────────────────
    [Header("효과 수치")]
    public int forbiddenResonanceDamage = 2;
    public int forbiddenResonanceExtraGaze = 1;
    public int sharpIntuitionExtraDraw = 1;
    public int sharpIntuitionPenaltyGaze = 2;
    public int thinBarrierShieldBonus = 2;
    public int thinBarrierPenaltyGaze = 2;
    public int weaknessTrackingDamage = 3;
    public int weaknessTrackingPenaltyGaze = 2;
    public int abyssalGrimoireCostReduction = 1;
    public int abyssalGrimoirePenaltyGaze = 2;
    public int abyssalGrimoireThreshold = 3;
    public int onslaughtDrawThreshold = 3;
    public int onslaughtPenaltyHpThreshold = 5;
    public int onslaughtPenaltyHp = 2;
    public int bloodyBreathEnergyGain = 1;
    public int bloodyBreathPenaltyGaze = 3;
    public int bloodyBreathTurnThreshold = 2;
    public int gapVisionCostReduction = 1;
    public int gapVisionPenaltyGaze = 2;
    public int gapVisionHandThreshold = 5;
    public int openingOmenExtraDraw = 1;
    public int gluttonyDamage = 5;
    public int gluttonyPenaltyGaze = 4;
    public int deepContactPenaltyGaze = 3;
    public int firstStrikeDamage = 6;
    public int firstStrikePenaltyGaze = 4;
    public int outerGodDescend80EnemyAtk = 1;
    public int tornMomentPenaltyGaze = 3;
    public float doomContractDamageMultiplier = 1.5f;
    public int doomContractPenaltyHp = 3;
    public int doomContractTurnThreshold = 6;
    public int madnessCyclePenaltyGaze = 4;
    public int outerGodDescend100MaxCards = 3;
    public int throttlingHandAttackBonus = 4;
    public float openEyeDamageMultiplier = 1.5f;
    public float openEyeNextTurnShieldMultiplier = 0.5f;
    public int abyssalCommandSuccessEnergy = 1;
    public int abyssalCommandFailHp = 30;
    public int gazeResetOn100 = 30;

    // ─── 런 단위 상태 ────────────────────────────────────────────────
    public GazeEffectData activeEffect20 { get; private set; }
    public GazeEffectData activeEffect40 { get; private set; }
    public GazeEffectData activeEffect60 { get; private set; }
    public GazeEffectData activeEffect80 { get; private set; }
    public GazeEffectData activeEffect100 { get; private set; }

    // ─── 턴 단위 상태 ────────────────────────────────────────────────
    private int cardsPlayedThisTurn;
    private int forbiddenPlayedThisTurn;
    private int nonForbiddenPlayedThisTurn;
    private int attackPlayedThisTurn;
    private bool firstAttackResolvedThisTurn;
    private bool firstAttackDealtDamageThisTurn;
    private bool gazeIncreasedThisTurn;
    private bool madnessCycleDrawnThisTurn;
    private bool usedJeopshinThisTurn;
    private bool abyssalGrimoirePenaltyTriggeredThisTurn;
    private bool tornMomentFirstCardConsumed;
    private bool abyssalGrimoireFirstUsed;
    private CardData gapVisionDiscountCard;
    private int consecutiveTurnsWithoutKill;

    // ─── 100 트리거 상태 ─────────────────────────────────────────────
    private bool deathProtectionActive;
    private bool openEyeMarkActive;
    private bool nextTurnShieldReduction;
    private bool openEyePending;            // N+1 턴에 활성화 대기
    private bool abyssalCommandPending;     // N+1 턴에 미션 시작 대기
    private bool nextTurnNonForbiddenCostIncrease;
    private bool currentTurnNonForbiddenCostIncrease;
    private HashSet<CardData> outerGodFreeCards = new HashSet<CardData>();
    private CardData hiddenTextCard;

    // ─── 미션 (100-4) ────────────────────────────────────────────────
    public enum MissionType { DealDamage, UseCards, GetDefense, UseForbidden }
    private bool missionActive;
    private MissionType currentMission;
    private int missionDamageDealt;
    private int missionForbiddenUsed;
    private const int missionDamageGoal = 15;
    private const int missionCardsGoal = 4;
    private const int missionDefenseGoal = 13;
    private const int missionForbiddenGoal = 2;

    public CardData HiddenTextCard => hiddenTextCard;
    public bool IsDeathProtected => deathProtectionActive;
    public bool MissionActive => missionActive;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (missionPanel != null) missionPanel.SetActive(false);
        }
        else Destroy(gameObject);
    }

    // ────────────────────────────────────────────────────────────────
    // 런 초기화
    // ────────────────────────────────────────────────────────────────
    public void InitializeRun()
    {
        activeEffect20 = PickRandom(pool20);
        activeEffect40 = PickRandom(pool40);
        activeEffect60 = PickRandom(pool60);
        activeEffect80 = PickRandom(pool80);
        activeEffect100 = PickRandom(pool100);
        Debug.Log($"[Gaze] 런 효과: 20={Name(activeEffect20)} / 40={Name(activeEffect40)} / 60={Name(activeEffect60)} / 80={Name(activeEffect80)} / 100={Name(activeEffect100)}");
    }

    GazeEffectData PickRandom(GazeEffectData[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        List<GazeEffectData> valid = new List<GazeEffectData>();
        foreach (var e in pool) if (e != null) valid.Add(e);
        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
    }

    string Name(GazeEffectData e) => e != null ? e.displayName : "(none)";

    public void ReplaceEffect(int threshold, GazeEffectData newEffect)
    {
        switch (threshold)
        {
            case 20: activeEffect20 = newEffect; break;
            case 40: activeEffect40 = newEffect; break;
            case 60: activeEffect60 = newEffect; break;
            case 80: activeEffect80 = newEffect; break;
            case 100: activeEffect100 = newEffect; break;
        }
        Debug.Log($"[Gaze] {threshold} 구간 효과 교체: {Name(newEffect)}");
    }

    public GazeEffectData GetEffectAt(int threshold)
    {
        switch (threshold)
        {
            case 20: return activeEffect20;
            case 40: return activeEffect40;
            case 60: return activeEffect60;
            case 80: return activeEffect80;
            case 100: return activeEffect100;
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────
    // 전투 초기화
    // ────────────────────────────────────────────────────────────────
    public void InitializeBattle()
    {
        ResetTurnState();
        consecutiveTurnsWithoutKill = 0;
        deathProtectionActive = false;
        openEyeMarkActive = false;
        nextTurnShieldReduction = false;
        openEyePending = false;
        abyssalCommandPending = false;
        nextTurnNonForbiddenCostIncrease = false;
        currentTurnNonForbiddenCostIncrease = false;
        outerGodFreeCards.Clear();
        hiddenTextCard = null;
        missionActive = false;
        if (missionPanel != null) missionPanel.SetActive(false);

        // 20-2 예민한 직감: 전투 시작 추가 드로우
        if (IsActive(GazeEffectType.SharpIntuition) && BattleManager.Instance != null)
            BattleManager.Instance.DrawCards(sharpIntuitionExtraDraw);
    }

    void ResetTurnState()
    {
        cardsPlayedThisTurn = 0;
        forbiddenPlayedThisTurn = 0;
        nonForbiddenPlayedThisTurn = 0;
        attackPlayedThisTurn = 0;
        firstAttackResolvedThisTurn = false;
        firstAttackDealtDamageThisTurn = false;
        gazeIncreasedThisTurn = false;
        madnessCycleDrawnThisTurn = false;
        usedJeopshinThisTurn = false;
        abyssalGrimoirePenaltyTriggeredThisTurn = false;
        tornMomentFirstCardConsumed = false;
        abyssalGrimoireFirstUsed = false;
    }

    // ────────────────────────────────────────────────────────────────
    // 턴 시작 / 종료 훅
    // ────────────────────────────────────────────────────────────────
    public void OnTurnStart()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        ResetTurnState();

        // 100-1: 다음 턴 비크툴루 비용 +1 (이번 턴에 적용)
        currentTurnNonForbiddenCostIncrease = nextTurnNonForbiddenCostIncrease;
        nextTurnNonForbiddenCostIncrease = false;

        // 100-3 개안: 트리거 다음 턴(=현재 턴)에 표식+방어 감소 활성화
        if (openEyePending)
        {
            openEyePending = false;
            openEyeMarkActive = true;
            nextTurnShieldReduction = true;
        }

        // 100-4 심연의 명령: 트리거 다음 턴(=현재 턴)에 미션 시작
        if (abyssalCommandPending)
        {
            abyssalCommandPending = false;
            currentMission = (MissionType)Random.Range(0, 4);
            missionActive = true;
            missionDamageDealt = 0;
            missionForbiddenUsed = 0;
            if (missionPanel != null) missionPanel.SetActive(true);
            RefreshMissionUI();
        }

        // 60-1 개안 전조: 매 턴 추가 드로우
        if (IsActive(GazeEffectType.OpeningOmen))
            bm.DrawCards(openingOmenExtraDraw);

        // 60-1 디버프: 손패 1장 효과 문구 가려짐
        if (IsActive(GazeEffectType.OpeningOmen) && bm.hand.Count > 0)
            hiddenTextCard = bm.hand[Random.Range(0, bm.hand.Count)];
        else
            hiddenTextCard = null;

        // 40-4 틈새 시야: 첫 드로우 카드 비용 -1
        if (IsActive(GazeEffectType.GapVision) && bm.hand.Count > 0)
            gapVisionDiscountCard = bm.hand[0];
        else
            gapVisionDiscountCard = null;

        // 80-1 외신의 강림: 상단 5장에 크툴루 없으면 1장 교체
        if (IsActive(GazeEffectType.OuterGodDescend80) && bm.gazeLevel >= 80)
            EnsureForbiddenInTopFive();
    }

    public void OnTurnEnd()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // 20-2 예민한 직감 디버프: 1턴 종료 시 마나 남으면 시선 +2
        if (IsActive(GazeEffectType.SharpIntuition) && bm.turnCount == 1 && bm.currentMana > 0 && bm.gazeLevel >= 20)
            bm.ChangeGaze(sharpIntuitionPenaltyGaze, "예민한 직감");

        // 20-3 얇은 방벽 디버프
        if (IsActive(GazeEffectType.ThinBarrier) && attackPlayedThisTurn == 0 && bm.gazeLevel >= 20)
            bm.ChangeGaze(thinBarrierPenaltyGaze, "얇은 방벽");

        // 40-2 몰아치기 디버프
        if (IsActive(GazeEffectType.Onslaught) && cardsPlayedThisTurn >= onslaughtPenaltyHpThreshold && bm.gazeLevel >= 40)
        {
            bm.playerCurrentHp -= onslaughtPenaltyHp;
            if (bm.playerHitEffect != null) bm.playerHitEffect.PlayHit();
        }

        // 40-3 핏빛 호흡 디버프 (적 처치 실패가 누적되면 시선 +3)
        if (IsActive(GazeEffectType.BloodyBreath) && consecutiveTurnsWithoutKill >= bloodyBreathTurnThreshold && bm.gazeLevel >= 40)
            bm.ChangeGaze(bloodyBreathPenaltyGaze, "핏빛 호흡");

        // 40-4 틈새 시야 디버프
        if (IsActive(GazeEffectType.GapVision) && bm.hand.Count >= gapVisionHandThreshold && bm.gazeLevel >= 40)
            bm.ChangeGaze(gapVisionPenaltyGaze, "틈새 시야");

        // 60-2 폭식 디버프
        if (IsActive(GazeEffectType.Gluttony) && forbiddenPlayedThisTurn == 0 && bm.gazeLevel >= 60)
            bm.ChangeGaze(gluttonyPenaltyGaze, "폭식");

        // 60-3 깊은 접촉 디버프
        if (IsActive(GazeEffectType.DeepContact) && usedJeopshinThisTurn && bm.gazeLevel >= 60)
            bm.ChangeGaze(deepContactPenaltyGaze, "깊은 접촉");

        // 60-4 첫 일격 디버프
        if (IsActive(GazeEffectType.FirstStrike) && firstAttackResolvedThisTurn && !firstAttackDealtDamageThisTurn && bm.gazeLevel >= 60)
            bm.ChangeGaze(firstStrikePenaltyGaze, "첫 일격");

        // 80-2 찢긴 찰나 디버프
        if (IsActive(GazeEffectType.TornMoment) && cardsPlayedThisTurn > 0 && bm.gazeLevel >= 80)
            bm.ChangeGaze(tornMomentPenaltyGaze, "찢긴 찰나");

        // 80-3 파멸 계약 디버프
        if (IsActive(GazeEffectType.DoomContract) && bm.turnCount >= doomContractTurnThreshold && bm.gazeLevel >= 80)
        {
            bm.playerCurrentHp -= doomContractPenaltyHp;
            if (bm.playerHitEffect != null) bm.playerHitEffect.PlayHit();
        }

        // 80-4 광기 순환 디버프
        if (IsActive(GazeEffectType.MadnessCycle) && !gazeIncreasedThisTurn && bm.gazeLevel >= 80)
            bm.ChangeGaze(madnessCyclePenaltyGaze, "광기 순환");

        // 100-2 목을 조르는 손: 턴 종료 시 체력 1
        if (deathProtectionActive)
        {
            bm.playerCurrentHp = 1;
            deathProtectionActive = false;
        }

        // 100-4 심연의 명령: 미션 결과 처리
        if (missionActive)
            ResolveMission();

        // 100-3 개안: 표식 / 방어도 감소는 한 턴만 유효
        openEyeMarkActive = false;
        nextTurnShieldReduction = false;

        // 100-1 무료 카드 만료
        outerGodFreeCards.Clear();

        // 처치 누적 카운트 (한 마리라도 살아있으면 처치 실패로 간주)
        if (bm.AnyMonsterAlive)
            consecutiveTurnsWithoutKill++;
    }

    // ────────────────────────────────────────────────────────────────
    // 카드 사용 훅
    // ────────────────────────────────────────────────────────────────
    public void OnCardPlayed(CardData card, bool targetIsMonster)
    {
        if (card == null) return;
        cardsPlayedThisTurn++;

        bool isForbidden = card.cardType == CardData.CardType.Forbidden;
        if (isForbidden) forbiddenPlayedThisTurn++;
        else nonForbiddenPlayedThisTurn++;

        // "공격 카드" 로 카운트되는 기준은 cardType 이 아니라 실제 데미지를 주는지로 판단.
        // 금단(크툴루) 카드라도 데미지를 주면 얇은 방벽/첫 일격 등에서 공격으로 인식됨.
        if (IsDamageCard(card)) attackPlayedThisTurn++;

        // 80-2 첫 카드 무료 소비 표시
        if (IsActive(GazeEffectType.TornMoment) && !tornMomentFirstCardConsumed)
            tornMomentFirstCardConsumed = true;

        // 40-1 심연의 독본: 첫 크툴루 비용 -1 사용 마킹
        if (IsActive(GazeEffectType.AbyssalGrimoire) && isForbidden && !abyssalGrimoireFirstUsed)
            abyssalGrimoireFirstUsed = true;

        // 20-1 금단의 감응 디버프
        if (IsActive(GazeEffectType.ForbiddenResonance) && isForbidden && BattleManager.Instance.gazeLevel >= 20)
            BattleManager.Instance.ChangeGaze(forbiddenResonanceExtraGaze, "금단의 감응");

        // 40-1 심연의 독본 디버프
        if (IsActive(GazeEffectType.AbyssalGrimoire) && !abyssalGrimoirePenaltyTriggeredThisTurn
            && nonForbiddenPlayedThisTurn >= abyssalGrimoireThreshold && BattleManager.Instance.gazeLevel >= 40)
        {
            abyssalGrimoirePenaltyTriggeredThisTurn = true;
            BattleManager.Instance.ChangeGaze(abyssalGrimoirePenaltyGaze, "심연의 독본");
        }

        // 40-2 몰아치기 버프 (3장째에 1장 드로우, 한 번만)
        if (IsActive(GazeEffectType.Onslaught) && BattleManager.Instance.gazeLevel >= 40
            && cardsPlayedThisTurn == onslaughtDrawThreshold)
            BattleManager.Instance.DrawCards(1);

        // 60-3 깊은 접촉 버프
        if (IsActive(GazeEffectType.DeepContact) && BattleManager.Instance.gazeLevel >= 60
            && card.cardName == "응시")
        {
            usedJeopshinThisTurn = true;
            BattleManager.Instance.DrawCards(1);
        }

        // 100-4 미션 진행도
        if (missionActive)
        {
            if (currentMission == MissionType.UseCards) RefreshMissionUI();
            if (currentMission == MissionType.UseForbidden && isForbidden)
            {
                missionForbiddenUsed++;
                RefreshMissionUI();
            }
        }
    }

    // 카드가 적에게 입힌 데미지 보고 (효과 적용 후 호출)
    public void OnDamageDealt(CardData card, int damageDealt)
    {
        if (card == null) return;

        // 첫 일격 추적도 "데미지 카드" 기준 — 금단의 데미지 카드 포함.
        if (IsDamageCard(card) && !firstAttackResolvedThisTurn)
        {
            firstAttackResolvedThisTurn = true;
            firstAttackDealtDamageThisTurn = damageDealt > 0;
        }

        if (missionActive && currentMission == MissionType.DealDamage)
        {
            missionDamageDealt += damageDealt;
            RefreshMissionUI();
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 비용 / 데미지 / 방어 모디파이어
    // ────────────────────────────────────────────────────────────────
    public int GetEffectiveCost(CardData card)
    {
        if (card == null) return 0;
        int cost = card.manaCost;

        if (outerGodFreeCards.Contains(card)) return 0;

        bool isForbidden = card.cardType == CardData.CardType.Forbidden;

        // 80-2 찢긴 찰나: 첫 카드 무료
        if (IsActive(GazeEffectType.TornMoment) && BattleManager.Instance.gazeLevel >= 80
            && !tornMomentFirstCardConsumed)
            return 0;

        // 40-1 심연의 독본: 첫 크툴루 -1
        if (IsActive(GazeEffectType.AbyssalGrimoire) && BattleManager.Instance.gazeLevel >= 40
            && isForbidden && !abyssalGrimoireFirstUsed)
            cost -= abyssalGrimoireCostReduction;

        // 40-4 틈새 시야: 첫 드로우 카드 -1
        if (IsActive(GazeEffectType.GapVision) && BattleManager.Instance.gazeLevel >= 40
            && card == gapVisionDiscountCard)
            cost -= gapVisionCostReduction;

        // 100-1 외신의 강림 디버프 (다음 턴): 비크툴루 +1
        if (currentTurnNonForbiddenCostIncrease && !isForbidden)
            cost += 1;

        return Mathf.Max(0, cost);
    }

    public int GetFlatDamageBonus(CardData card, Monster target = null)
    {
        if (card == null || BattleManager.Instance == null) return 0;
        int bonus = 0;
        BattleManager bm = BattleManager.Instance;
        bool isForbidden = card.cardType == CardData.CardType.Forbidden;
        // "공격 카드" 인지 여부 — 금단이라도 데미지를 주면 첫 일격 보너스 적용 대상이 됨.
        bool isAttack = IsDamageCard(card);

        // 20-1 금단의 감응 버프
        if (IsActive(GazeEffectType.ForbiddenResonance) && bm.gazeLevel >= 20 && isForbidden)
            bonus += forbiddenResonanceDamage;

        // 20-4 약점 추적: 타겟 몬스터 체력 50% 이하 (전체타겟 카드는 각 몬스터별 평가)
        if (IsActive(GazeEffectType.WeaknessTracking) && bm.gazeLevel >= 20
            && target != null && target.data != null
            && target.currentHp * 2 <= target.data.maxHp)
            bonus += weaknessTrackingDamage;

        // 60-2 폭식 버프
        if (IsActive(GazeEffectType.Gluttony) && bm.gazeLevel >= 60 && isForbidden)
            bonus += gluttonyDamage;

        // 60-4 첫 일격: 첫 공격 카드 +6 (아직 첫 공격 미해결시)
        if (IsActive(GazeEffectType.FirstStrike) && bm.gazeLevel >= 60 && isAttack && !firstAttackResolvedThisTurn)
            bonus += firstStrikeDamage;

        // 100-2 목을 조르는 손: 공격 +4
        if (deathProtectionActive && isAttack)
            bonus += throttlingHandAttackBonus;

        return bonus;
    }

    public float GetDamageMultiplier(CardData card, Monster target = null)
    {
        if (card == null || BattleManager.Instance == null) return 1f;
        BattleManager bm = BattleManager.Instance;
        float mul = 1f;

        // 80-3 파멸 계약: 타겟 몬스터 30% 이하면 +50%
        if (IsActive(GazeEffectType.DoomContract) && bm.gazeLevel >= 80
            && target != null && target.data != null
            && target.currentHp * 10 <= target.data.maxHp * 3)
            mul *= doomContractDamageMultiplier;

        // 100-3 개안 표식: +50%
        if (openEyeMarkActive)
            mul *= openEyeDamageMultiplier;

        return mul;
    }

    public bool IgnoresMonsterDefense(CardData card, Monster target = null)
    {
        return openEyeMarkActive;
    }

    public int GetFlatShieldBonus(CardData card)
    {
        if (card == null || BattleManager.Instance == null) return 0;
        BattleManager bm = BattleManager.Instance;

        if (IsActive(GazeEffectType.ThinBarrier) && bm.gazeLevel >= 20 && IsShieldCard(card))
            return thinBarrierShieldBonus;
        return 0;
    }

    public float GetShieldMultiplier()
    {
        // 100-3 개안 디버프: 다음 턴 방어 카드 효과 -50%
        return nextTurnShieldReduction ? openEyeNextTurnShieldMultiplier : 1f;
    }

    public int GetMonsterBonusAttack()
    {
        if (BattleManager.Instance == null) return 0;
        BattleManager bm = BattleManager.Instance;

        // 80-1 외신의 강림 디버프: 손패에 크툴루 있으면 적 +1
        if (IsActive(GazeEffectType.OuterGodDescend80) && bm.gazeLevel >= 80 && HandHasForbidden())
            return outerGodDescend80EnemyAtk;
        return 0;
    }

    bool HandHasForbidden()
    {
        if (BattleManager.Instance == null) return false;
        foreach (var c in BattleManager.Instance.hand)
            if (c != null && c.cardType == CardData.CardType.Forbidden) return true;
        return false;
    }

    // ─── 카드 분류 헬퍼 (외부에서도 재사용) ──────────────────────────
    // "데미지를 주는 카드" — 어떤 cardType 이든(Attack/Skill/Forbidden) 데미지 효과면 true.
    // 얇은 방벽 / 첫 일격 / 약점 추적 등 "공격 카드" 기반 시선 효과의 통일된 판정 기준.
    public static bool IsDamageCard(CardData card)
    {
        if (card == null) return false;
        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
            case CardData.CardEffectType.DamageAndShield:
            case CardData.CardEffectType.MultiHit:
            case CardData.CardEffectType.PenetratingDamage:
            case CardData.CardEffectType.RandomDamage:
            case CardData.CardEffectType.AllDamage:
            case CardData.CardEffectType.AllMultiHit:
            case CardData.CardEffectType.DamageSelfDamage:
                return true;
        }
        return false;
    }

    public static bool IsShieldCard(CardData card)
    {
        if (card == null) return false;
        switch (card.effectType)
        {
            case CardData.CardEffectType.Shield:
            case CardData.CardEffectType.DamageAndShield:
            case CardData.CardEffectType.ShieldAndDraw:
                return true;
        }
        return false;
    }

    // 카드 설명에 표시되는 기본 데미지/방어도 값 — 시선 효과로 변경 시 색상 강조에 사용.
    public static int GetCardBaseDamageValue(CardData card)
    {
        if (card == null) return 0;
        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
            case CardData.CardEffectType.DamageAndShield:
            case CardData.CardEffectType.MultiHit:
            case CardData.CardEffectType.PenetratingDamage:
            case CardData.CardEffectType.AllDamage:
            case CardData.CardEffectType.AllMultiHit:
            case CardData.CardEffectType.DamageSelfDamage:
                return card.value;
        }
        return 0;
    }

    public static int GetCardBaseShieldValue(CardData card)
    {
        if (card == null) return 0;
        switch (card.effectType)
        {
            case CardData.CardEffectType.Shield:
            case CardData.CardEffectType.ShieldAndDraw:
                return card.value;
            case CardData.CardEffectType.DamageAndShield:
                return card.value2;
        }
        return 0;
    }

    // ────────────────────────────────────────────────────────────────
    // 시선 변동 훅
    // ────────────────────────────────────────────────────────────────
    public void OnGazeChanged(int delta)
    {
        if (delta <= 0) return;
        gazeIncreasedThisTurn = true;

        // 80-4 광기 순환 버프: 시선 오를 때마다 카드 1장 드로우 (턴당 1회)
        if (IsActive(GazeEffectType.MadnessCycle) && BattleManager.Instance != null
            && BattleManager.Instance.gazeLevel >= 80 && !madnessCycleDrawnThisTurn)
        {
            madnessCycleDrawnThisTurn = true;
            BattleManager.Instance.DrawCards(1);
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 처치 / 사망 훅
    // ────────────────────────────────────────────────────────────────
    public void OnMonsterKilled()
    {
        consecutiveTurnsWithoutKill = 0;

        // 40-3 핏빛 호흡: 적 처치 시 에너지 +1
        if (IsActive(GazeEffectType.BloodyBreath) && BattleManager.Instance != null
            && BattleManager.Instance.gazeLevel >= 40)
            BattleManager.Instance.currentMana += bloodyBreathEnergyGain;
    }

    // ────────────────────────────────────────────────────────────────
    // 100 트리거
    // ────────────────────────────────────────────────────────────────
    public void TriggerGaze100()
    {
        if (BattleManager.Instance == null) return;
        BattleManager bm = BattleManager.Instance;

        if (activeEffect100 == null)
        {
            // 폴백: 기존 저주 동작 (모든 살아있는 몬스터에 힘 +3 영구)
            bm.playerCurrentHp -= 20;
            foreach (var mm in bm.GetAliveMonsters()) mm.ApplyStrength(3, 99);
            bm.gazeLevel = gazeResetOn100;
            if (bm.playerHitEffect != null) bm.playerHitEffect.PlayHit();
            return;
        }

        switch (activeEffect100.effectType)
        {
            case GazeEffectType.OuterGodDescend100: TriggerOuterGodDescend100(); break;
            case GazeEffectType.ThrottlingHand:     TriggerThrottlingHand(); break;
            case GazeEffectType.OpenEye:            TriggerOpenEye(); break;
            case GazeEffectType.AbyssalCommand:     TriggerAbyssalCommand(); break;
        }

        bm.gazeLevel = gazeResetOn100;
    }

    void TriggerOuterGodDescend100()
    {
        BattleManager bm = BattleManager.Instance;
        int taken = 0;
        List<CardData> sources = new List<CardData>();

        for (int i = bm.deck.Count - 1; i >= 0 && taken < outerGodDescend100MaxCards; i--)
        {
            if (bm.deck[i].cardType == CardData.CardType.Forbidden)
            {
                sources.Add(bm.deck[i]);
                bm.deck.RemoveAt(i);
                taken++;
            }
        }
        for (int i = bm.discardPile.Count - 1; i >= 0 && taken < outerGodDescend100MaxCards; i--)
        {
            if (bm.discardPile[i].cardType == CardData.CardType.Forbidden)
            {
                sources.Add(bm.discardPile[i]);
                bm.discardPile.RemoveAt(i);
                taken++;
            }
        }

        foreach (var c in sources)
        {
            bm.hand.Add(c);
            outerGodFreeCards.Add(c);
        }

        nextTurnNonForbiddenCostIncrease = true;

        if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();
        Debug.Log($"[Gaze100] 외신의 강림: {taken}장 강제 드로우 (이번 턴 무료)");
    }

    void TriggerThrottlingHand()
    {
        BattleManager bm = BattleManager.Instance;
        deathProtectionActive = true;
        bm.DrawCards(1);
        Debug.Log("[Gaze100] 목을 조르는 손: 사망 보호 + 드로우 1");
    }

    void TriggerOpenEye()
    {
        openEyePending = true;
        Debug.Log("[Gaze100] 개안: 다음 턴부터 표식 + 방어 -50% 적용 예정");
    }

    void TriggerAbyssalCommand()
    {
        abyssalCommandPending = true;
        Debug.Log("[Gaze100] 심연의 명령: 다음 턴부터 미션 시작 예정");
    }

    void RefreshMissionUI()
    {
        if (missionTitleText != null) missionTitleText.text = "심연의 명령";
        if (missionConditionText != null)
        {
            switch (currentMission)
            {
                case MissionType.DealDamage:
                    missionConditionText.text = $"적에게 {missionDamageGoal} 피해 ({missionDamageDealt}/{missionDamageGoal})"; break;
                case MissionType.UseCards:
                    missionConditionText.text = $"카드 {missionCardsGoal}장 사용 ({cardsPlayedThisTurn}/{missionCardsGoal})"; break;
                case MissionType.GetDefense:
                    int def = BattleManager.Instance != null ? BattleManager.Instance.playerDefense : 0;
                    missionConditionText.text = $"방어도 {missionDefenseGoal} 이상 ({def}/{missionDefenseGoal})"; break;
                case MissionType.UseForbidden:
                    missionConditionText.text = $"크툴루 카드 {missionForbiddenGoal}장 사용 ({missionForbiddenUsed}/{missionForbiddenGoal})"; break;
            }
        }
        if (missionRewardText != null)
            missionRewardText.text = $"성공: 크툴루 카드 1장(비용 0) + 행동력 +{abyssalCommandSuccessEnergy}\n실패: 체력 -{abyssalCommandFailHp}";
    }

    void ResolveMission()
    {
        BattleManager bm = BattleManager.Instance;
        bool success = false;
        switch (currentMission)
        {
            case MissionType.DealDamage:    success = missionDamageDealt >= missionDamageGoal; break;
            case MissionType.UseCards:      success = cardsPlayedThisTurn >= missionCardsGoal; break;
            case MissionType.GetDefense:    success = bm.playerDefense >= missionDefenseGoal; break;
            case MissionType.UseForbidden:  success = missionForbiddenUsed >= missionForbiddenGoal; break;
        }

        if (success)
        {
            if (forbiddenCardPool != null && forbiddenCardPool.Length > 0)
            {
                CardData reward = forbiddenCardPool[Random.Range(0, forbiddenCardPool.Length)];
                bm.hand.Add(reward);
                outerGodFreeCards.Add(reward);
                if (PlayerHand.Instance != null) PlayerHand.Instance.RefreshHand();
            }
            bm.currentMana += abyssalCommandSuccessEnergy;
            Debug.Log("[Gaze100] 미션 성공!");
        }
        else
        {
            bm.playerCurrentHp -= abyssalCommandFailHp;
            if (bm.playerHitEffect != null) bm.playerHitEffect.PlayHit();
            Debug.Log("[Gaze100] 미션 실패!");
        }

        missionActive = false;
        if (missionPanel != null) missionPanel.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────
    // 80-1 보조: 상단 5장에 크툴루 있는지 확인 후 교체
    // ────────────────────────────────────────────────────────────────
    void EnsureForbiddenInTopFive()
    {
        BattleManager bm = BattleManager.Instance;
        int top = Mathf.Min(5, bm.deck.Count);
        for (int i = 0; i < top; i++)
            if (bm.deck[i].cardType == CardData.CardType.Forbidden) return;

        for (int i = top; i < bm.deck.Count; i++)
        {
            if (bm.deck[i].cardType == CardData.CardType.Forbidden)
            {
                CardData fb = bm.deck[i];
                bm.deck.RemoveAt(i);
                bm.deck.Insert(0, fb);
                return;
            }
        }
        for (int i = 0; i < bm.discardPile.Count; i++)
        {
            if (bm.discardPile[i].cardType == CardData.CardType.Forbidden)
            {
                CardData fb = bm.discardPile[i];
                bm.discardPile.RemoveAt(i);
                bm.deck.Insert(0, fb);
                return;
            }
        }
    }

    bool IsActive(GazeEffectType type)
    {
        if (activeEffect20 != null && activeEffect20.effectType == type) return true;
        if (activeEffect40 != null && activeEffect40.effectType == type) return true;
        if (activeEffect60 != null && activeEffect60.effectType == type) return true;
        if (activeEffect80 != null && activeEffect80.effectType == type) return true;
        if (activeEffect100 != null && activeEffect100.effectType == type) return true;
        return false;
    }
}
