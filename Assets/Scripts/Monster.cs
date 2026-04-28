using UnityEngine;

// 전투 중 단일 몬스터의 런타임 상태와 시각/UI 참조를 캡슐화한다.
// 씬에 사전 배치되거나 BattleManager가 런타임에 스폰한다.
[DisallowMultipleComponent]
public class Monster : MonoBehaviour
{
    [Header("데이터")]
    public MonsterData data;

    [Header("시각 참조 (비워두면 자동 탐색)")]
    public SpriteRenderer spriteRenderer;
    public HitEffect hitEffect;
    public Animator animator;

    [Header("런타임 UI (자동 생성 가능)")]
    public MonsterRuntimeUI runtimeUI;
    public bool autoCreateRuntimeUI = true;
    public Vector3 uiWorldOffset = new Vector3(0f, 1.4f, 0f);

    [Header("인트로 진입 오프셋 (X 양수=오른쪽에서)")]
    public float introEnterOffsetX = 15f;

    // ─── 런타임 상태 ─────────────────────────────────────────────
    public int currentHp;
    public int defense;
    public int strength;
    public int strengthTurns;
    public int debuffTurns;
    public MonsterData.MonsterAction nextAction;

    private int sequentialIndex;
    private Vector3 finalPosition;
    private bool finalPositionCached;

    public bool IsAlive => currentHp > 0;
    public string DisplayName => data != null ? data.monsterName : name;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (hitEffect == null) hitEffect = GetComponent<HitEffect>();
        if (animator == null) animator = GetComponent<Animator>();
        if (gameObject.tag != "Monster") gameObject.tag = "Monster";
    }

    public void CacheFinalPosition()
    {
        finalPosition = transform.position;
        finalPositionCached = true;
    }

    public Vector3 FinalPosition => finalPositionCached ? finalPosition : transform.position;

    public void InitializeForBattle()
    {
        if (data == null)
        {
            Debug.LogWarning($"[Monster] {name}: MonsterData 미할당");
            return;
        }
        ApplyVisualOverride();
        currentHp = data.maxHp;
        defense = 0;
        strength = 0;
        strengthTurns = 0;
        debuffTurns = 0;
        sequentialIndex = 0;
        nextAction = PickNextAction();

        EnsureRuntimeUI();
        if (runtimeUI != null) runtimeUI.RefreshAll();
    }

    public void ApplyVisualOverride()
    {
        if (data == null) return;
        if (data.spriteOverride != null && spriteRenderer != null)
            spriteRenderer.sprite = data.spriteOverride;
        if (data.animatorOverride != null && animator != null)
            animator.runtimeAnimatorController = data.animatorOverride;
        if (data.visualScale != Vector3.zero)
            transform.localScale = data.visualScale;
    }

    public void EnsureRuntimeUI()
    {
        if (runtimeUI != null || !autoCreateRuntimeUI) return;
        runtimeUI = MonsterRuntimeUI.CreateFor(this);
    }

    public MonsterData.MonsterAction PickNextAction()
    {
        if (data == null || data.actionPool == null || data.actionPool.Length == 0)
            return null;

        if (data.actionMode == MonsterData.ActionMode.Sequential)
        {
            var action = data.actionPool[sequentialIndex % data.actionPool.Length];
            sequentialIndex++;
            return action;
        }
        return data.actionPool[Random.Range(0, data.actionPool.Length)];
    }

    // 카드/효과로부터 데미지를 받는다. ignoreDefense=true 면 방어도 무시.
    // 실제로 들어간 피해량을 반환.
    public int TakeDamage(int damage, bool ignoreDefense)
    {
        int actual;
        int defenseAbsorbed;
        if (ignoreDefense)
        {
            actual = damage;
            defenseAbsorbed = 0;
        }
        else
        {
            defenseAbsorbed = Mathf.Min(defense, damage);
            actual = Mathf.Max(0, damage - defense);
            defense = Mathf.Max(0, defense - damage);
        }
        currentHp -= actual;
        if (actual > 0 && hitEffect != null) hitEffect.PlayHit();
        Debug.Log($"[{DisplayName}] 받은 데미지 {damage}, 방어도 {defenseAbsorbed} 차감, HP -{actual}");
        return actual;
    }

    // 관통 등 방어/감산 모두 무시하고 그대로 적용
    public void DirectDamage(int damage)
    {
        currentHp -= damage;
        if (damage > 0 && hitEffect != null) hitEffect.PlayHit();
    }

    public void AddDefense(int amount) => defense += amount;

    public void ApplyStrength(int amount, int turns)
    {
        strength += amount;
        strengthTurns = Mathf.Max(strengthTurns, turns);
    }

    public void ApplyDebuff(int turns)
    {
        debuffTurns = Mathf.Max(debuffTurns, turns);
    }

    // 자기 턴 시작 시 방어도 리셋 (StS 방식 — 이전 턴에 쌓은 블록은 다음 자기 턴까지 유지)
    public void BeginTurn()
    {
        defense = 0;
    }

    // 턴 종료 시 버프/디버프 턴수 감소 (방어도는 BeginTurn에서 처리)
    public void EndOfTurnCleanup()
    {
        if (strengthTurns > 0) strengthTurns--;
        if (strengthTurns == 0) strength = 0;
        if (debuffTurns > 0) debuffTurns--;
    }

    public Vector3 GetUIAnchorWorld() => transform.position + uiWorldOffset;

    public void Die()
    {
        if (runtimeUI != null) runtimeUI.OnDeath();
        gameObject.SetActive(false);
    }
}
