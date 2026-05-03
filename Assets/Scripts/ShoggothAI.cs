using UnityEngine;
using System.Collections;

// 1막 보스 쇼거스. HP 기반 3페이즈 + 페이즈별 4턴 사이클.
// 페이즈 전환 임계값: HP 60% / 30%. 사이클 인덱스는 페이즈 전환 시 0 으로 리셋.
//
// 일반 액션 (Attack/Defend/...) 을 쓰지 않고 Kind enum 으로 5종 패턴을 분기 실행.
// 개안 스택은 Monster.openEyeStacks 에 누적되며 모든 데미지 액션에 + 적용.
//
// 향후 시선 페이즈 추가 시: PrepareNextAction 시작부에서 시선 임계값 체크하여
// currentSubPhase 분기 → patternForCycle 다른 테이블 참조 형태로 확장 가능.
[DisallowMultipleComponent]
public class ShoggothAI : MonoBehaviour, IBossAI
{
    public enum Kind
    {
        TentacleSlam,    // 촉수 후려치기 — 단일 강타
        GazeEcho,        // 응시 반향 — 시선 +X, 자기 방어 +Y
        FissionBarrage,  // 분열성 난타 — 멀티히트
        AbyssalDevour,   // 심연의 포식 — 단일 + 시선 50 이상이면 추가 데미지
        OpenEyeBloom,    // 육안 개화 — 자기 개안 +1
    }

    [System.Serializable]
    public class Pattern
    {
        public Kind kind;
        public int value;       // 데미지 / 시선 / 방어 등 주값
        public int hits;        // 멀티히트 횟수 (FissionBarrage 만 사용)
        public int gazeAdd;     // 응시 반향의 시선 증가량
        public int defenseAdd;  // 응시 반향의 자기 방어도
        public int bonusValue;  // 조건부 보너스 (분열성 추가 히트 / 심연포식 추가 데미지)
        public int gazeThreshold; // 보너스 발동 시선 임계값
    }

    [Header("페이즈 임계값 (HP 비율)")]
    [Range(0f, 1f)] public float phase2Threshold = 0.6f;
    [Range(0f, 1f)] public float phase3Threshold = 0.3f;

    [Header("개안")]
    public int maxOpenEyeStacks = 3;

    [Header("멀티히트 텀")]
    public float multiHitInterval = 0.2f;

    private int currentPhase = 1;
    private int cycleIndex = 0;          // 0 ~ 3 — 페이즈 내 4턴 사이클
    private Pattern queuedPattern;       // 다음 턴에 실행할 패턴 (인텐트 표시용)

    // 시선 게이지 조건 "락" — 인텐트가 결정된 시점의 시선 기준으로 한 번만 평가하고
    // 실행 시점에는 다시 평가하지 않는다 (슬더스 보스 인텐트 락 방식).
    // 예: 인텐트가 "공격 4 x 3" 으로 떴으면 그 턴에 플레이어가 시선 50 을 넘겨도 4 x 3 그대로 들어옴.
    private int lockedHits;
    private int lockedBonusDamage;

    // 페이즈별 4턴 사이클 패턴 정의. 인덱스 = cycleIndex.
    private static readonly Pattern[] Phase1Patterns = {
        new Pattern { kind = Kind.TentacleSlam, value = 12 },
        new Pattern { kind = Kind.GazeEcho, gazeAdd = 6, defenseAdd = 6 },
        new Pattern { kind = Kind.FissionBarrage, value = 4, hits = 3 },
        new Pattern { kind = Kind.OpenEyeBloom },
    };

    private static readonly Pattern[] Phase2Patterns = {
        new Pattern { kind = Kind.FissionBarrage, value = 4, hits = 3, bonusValue = 1, gazeThreshold = 50 },
        new Pattern { kind = Kind.GazeEcho, gazeAdd = 6, defenseAdd = 8 },
        new Pattern { kind = Kind.AbyssalDevour, value = 10, bonusValue = 6, gazeThreshold = 50 },
        new Pattern { kind = Kind.OpenEyeBloom },
    };

    private static readonly Pattern[] Phase3Patterns = {
        new Pattern { kind = Kind.FissionBarrage, value = 5, hits = 4 },
        new Pattern { kind = Kind.GazeEcho, gazeAdd = 8, defenseAdd = 8 },
        new Pattern { kind = Kind.AbyssalDevour, value = 12, bonusValue = 8, gazeThreshold = 50 },
        new Pattern { kind = Kind.TentacleSlam, value = 14 },
    };

    public void OnBattleStart(Monster m)
    {
        currentPhase = 1;
        cycleIndex = 0;
        queuedPattern = null;
        lockedHits = 0;
        lockedBonusDamage = 0;
        Debug.Log("[Shoggoth] 전투 시작 — 페이즈 1");
    }

    // 매 턴 호출. 먼저 HP 비율로 페이즈 전환 검사 → 그 페이즈 패턴 테이블에서 cycleIndex 위치 추출.
    // 페이즈 전환 시 cycleIndex 를 0 으로 리셋하여 새 페이즈 1턴부터 시작.
    public void PrepareNextAction(Monster m)
    {
        TryPhaseTransition(m);

        var table = GetPatternsForPhase(currentPhase);
        queuedPattern = table[cycleIndex % table.Length];

        // 시선 조건 "락" — 인텐트 결정 시점에만 평가. 실행 시 시선이 바뀌어도 영향 없음.
        lockedHits = queuedPattern.hits;
        lockedBonusDamage = 0;
        int gaze = BattleManager.Instance != null ? BattleManager.Instance.gazeLevel : 0;
        bool gazeMet = queuedPattern.bonusValue > 0
            && queuedPattern.gazeThreshold > 0
            && gaze >= queuedPattern.gazeThreshold;
        if (gazeMet)
        {
            switch (queuedPattern.kind)
            {
                case Kind.FissionBarrage: lockedHits += queuedPattern.bonusValue; break;
                case Kind.AbyssalDevour:  lockedBonusDamage = queuedPattern.bonusValue; break;
            }
        }
    }

    void TryPhaseTransition(Monster m)
    {
        if (m.data == null || m.data.maxHp <= 0) return;
        float ratio = (float)m.currentHp / m.data.maxHp;

        int newPhase = currentPhase;
        if (ratio <= phase3Threshold) newPhase = 3;
        else if (ratio <= phase2Threshold) newPhase = 2;
        else newPhase = 1;

        // 페이즈는 단방향 진행 (회복으로 되돌아가지 않음).
        if (newPhase > currentPhase)
        {
            currentPhase = newPhase;
            cycleIndex = 0;
            Debug.Log($"[Shoggoth] 페이즈 {currentPhase} 진입 (HP {m.currentHp}/{m.data.maxHp}, 비율 {ratio:P0})");
        }
    }

    Pattern[] GetPatternsForPhase(int phase)
    {
        switch (phase)
        {
            case 1: return Phase1Patterns;
            case 2: return Phase2Patterns;
            case 3: return Phase3Patterns;
            default: return Phase1Patterns;
        }
    }

    public IEnumerator ExecuteAction(Monster m, BattleManager bm)
    {
        if (queuedPattern == null) yield break;
        var p = queuedPattern;

        switch (p.kind)
        {
            case Kind.TentacleSlam:
                DealDamageToPlayer(m, bm, p.value);
                break;

            case Kind.GazeEcho:
                bm.ChangeGaze(p.gazeAdd, m.DisplayName);
                m.AddDefense(p.defenseAdd);
                Debug.Log($"[Shoggoth] 응시 반향 — 플레이어 시선 +{p.gazeAdd}, 자기 방어 +{p.defenseAdd}");
                break;

            case Kind.FissionBarrage:
            {
                int hits = lockedHits; // 인텐트 결정 시점에 락된 값 사용
                Debug.Log($"[Shoggoth] 분열성 난타 — {p.value} x {hits}회 (락된 히트수)");
                for (int i = 0; i < hits; i++)
                {
                    if (bm.playerCurrentHp <= 0) yield break;
                    DealDamageToPlayer(m, bm, p.value);
                    if (i < hits - 1) yield return new WaitForSeconds(multiHitInterval);
                }
                break;
            }

            case Kind.AbyssalDevour:
            {
                int dmg = p.value + lockedBonusDamage; // 락된 보너스 사용
                if (lockedBonusDamage > 0)
                    Debug.Log($"[Shoggoth] 심연의 포식 — 락된 보너스 +{lockedBonusDamage} 적용");
                DealDamageToPlayer(m, bm, dmg);
                break;
            }

            case Kind.OpenEyeBloom:
            {
                int before = m.openEyeStacks;
                m.openEyeStacks = Mathf.Min(maxOpenEyeStacks, m.openEyeStacks + 1);
                Debug.Log($"[Shoggoth] 육안 개화 — 개안 {before} → {m.openEyeStacks}");
                break;
            }
        }

        // 사이클 진행 (다음 턴은 다음 인덱스 패턴)
        cycleIndex = (cycleIndex + 1) % 4;
    }

    // 플레이어 데미지 처리 — Monster.strength + openEyeStacks + 시선 보정 모두 반영.
    // BattleManager 의 기존 일반 Attack 처리와 동일한 공식을 따른다.
    void DealDamageToPlayer(Monster m, BattleManager bm, int baseDamage)
    {
        int damage = baseDamage + m.strength + m.openEyeStacks;
        if (GazeEffectManager.Instance != null)
            damage += GazeEffectManager.Instance.GetMonsterBonusAttack();

        int actualDamage = Mathf.Max(0, damage - bm.playerDefense);
        bm.playerDefense = Mathf.Max(0, bm.playerDefense - damage);

        if (actualDamage > 0)
        {
            bm.playerCurrentHp -= actualDamage;
            if (bm.playerHitEffect != null) bm.playerHitEffect.PlayHit();
        }
        bm.CheckPlayerDeathPublic();
    }

    public string GetIntentText(Monster m)
    {
        if (queuedPattern == null) return "";
        var p = queuedPattern;
        switch (p.kind)
        {
            case Kind.TentacleSlam:
                return $"공격 {DisplayDamage(m, p.value)}{OpenEyeSuffix(m)}";

            case Kind.GazeEcho:
                return $"시선 +{p.gazeAdd} / 방어 +{p.defenseAdd}";

            case Kind.FissionBarrage:
                // 락된 hits 사용 — 시선이 그 턴에 변동돼도 인텐트는 변하지 않음
                return $"공격 {DisplayDamage(m, p.value)} x {lockedHits}{OpenEyeSuffix(m)}";

            case Kind.AbyssalDevour:
                return $"공격 {DisplayDamage(m, p.value + lockedBonusDamage)}{OpenEyeSuffix(m)}";

            case Kind.OpenEyeBloom:
                return "강화 (개안)";
        }
        return "";
    }

    int DisplayDamage(Monster m, int baseDamage)
    {
        int d = baseDamage + m.strength + m.openEyeStacks;
        if (GazeEffectManager.Instance != null)
            d += GazeEffectManager.Instance.GetMonsterBonusAttack();
        return d;
    }

    string OpenEyeSuffix(Monster m)
    {
        return m.openEyeStacks > 0 ? $" (+개안{m.openEyeStacks})" : "";
    }

    public Sprite GetIntentIcon(Monster m)
    {
        if (queuedPattern == null) return null;
        switch (queuedPattern.kind)
        {
            case Kind.GazeEcho:        return BattleIcons.Defense;
            case Kind.OpenEyeBloom:    return null; // 색상 폴백
            default:                   return BattleIcons.Strike;
        }
    }

    public Color GetIntentColor(Monster m)
    {
        if (queuedPattern == null) return Color.white;
        switch (queuedPattern.kind)
        {
            case Kind.GazeEcho:        return new Color(0.15f, 0.55f, 0.85f); // 방어 색
            case Kind.OpenEyeBloom:    return new Color(0.3f, 0.75f, 1f);     // 개안 색
            default:                   return new Color(0.85f, 0.15f, 0.15f); // 공격 색
        }
    }

    // 디버그용
    public int CurrentPhase => currentPhase;
    public int CycleIndex => cycleIndex;
}
