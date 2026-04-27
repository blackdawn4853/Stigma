using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 각 몬스터 GameObject 위에 떠다니는 월드 스페이스 캔버스 UI를 런타임에 생성한다.
// HP 바, HP 텍스트, 방어도, 상태(근력/약화), 인텐트 패널 포함.
public class MonsterRuntimeUI : MonoBehaviour
{
    public Monster monster;

    [Header("레이아웃")]
    public Vector2 canvasSize = new Vector2(3.2f, 2.0f);
    public float canvasScale = 0.01f;

    [Header("색상")]
    public Color hpFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Color hpBackColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color attackColor = new Color(0.85f, 0.15f, 0.15f);
    public Color defendColor = new Color(0.15f, 0.55f, 0.85f);
    public Color buffColor = new Color(0.85f, 0.65f, 0.15f);
    public Color debuffColor = new Color(0.55f, 0.15f, 0.85f);

    private Canvas canvas;
    private Slider hpBar;
    private Image hpFill;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI defenseText;
    private TextMeshProUGUI strengthText;
    private TextMeshProUGUI weakText;

    private GameObject intentRoot;
    private Image intentIcon;
    private TextMeshProUGUI intentText;
    private TextMeshProUGUI intentTurnText;

    public static MonsterRuntimeUI CreateFor(Monster m)
    {
        if (m == null) return null;
        GameObject go = new GameObject("MonsterRuntimeUI");
        go.transform.SetParent(m.transform, false);

        // monster 의 localScale 이 음수/큰 값일 수 있어 부모를 직접 따라가면 UI 가 깨진다.
        // 캔버스를 자식으로 두지 않고 별도 루트로 만들고 LateUpdate 에서 위치 추적.
        go.transform.SetParent(null);
        go.transform.localScale = Vector3.one;

        var ui = go.AddComponent<MonsterRuntimeUI>();
        ui.monster = m;
        ui.Build();
        return ui;
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        scaler.referencePixelsPerUnit = 100f;
        gameObject.AddComponent<GraphicRaycaster>();

        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(canvasSize.x / canvasScale, canvasSize.y / canvasScale);
        rt.localScale = Vector3.one * canvasScale;

        BuildHpBar(rt);
        BuildStatusRow(rt);
        BuildIntent(rt);
    }

    void BuildHpBar(RectTransform parent)
    {
        // 배경
        GameObject bg = new GameObject("HPBarBack", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = new Vector2(0.5f, 0f);
        bgRt.anchorMax = new Vector2(0.5f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = new Vector2(0f, 60f);
        bgRt.sizeDelta = new Vector2(220f, 28f);
        bg.GetComponent<Image>().color = hpBackColor;

        // 슬라이더
        GameObject slider = new GameObject("HPBar", typeof(RectTransform), typeof(Slider));
        slider.transform.SetParent(bgRt, false);
        var sRt = (RectTransform)slider.transform;
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(2f, 2f);
        sRt.offsetMax = new Vector2(-2f, -2f);

        // Fill
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sRt, false);
        var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(faRt, false);
        var fRt = (RectTransform)fill.transform;
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        hpFill = fill.GetComponent<Image>();
        hpFill.color = hpFillColor;

        hpBar = slider.GetComponent<Slider>();
        hpBar.transition = Selectable.Transition.None;
        hpBar.fillRect = fRt;
        hpBar.targetGraphic = null;
        hpBar.minValue = 0f;
        hpBar.maxValue = 1f;
        hpBar.value = 1f;
        hpBar.interactable = false;

        // HP 텍스트
        hpText = AddText(bgRt, "HPText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 28f), 18, TextAlignmentOptions.Center);
        hpText.text = "0/0";
        hpText.color = Color.white;
        hpText.fontStyle = FontStyles.Bold;

        // 방어도 텍스트 (HP바 좌측 위)
        defenseText = AddText(parent, "DefenseText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-130f, 75f), new Vector2(80f, 24f), 16, TextAlignmentOptions.Left);
        defenseText.color = new Color(0.4f, 0.7f, 1f);
        defenseText.fontStyle = FontStyles.Bold;
        defenseText.text = "";
    }

    void BuildStatusRow(RectTransform parent)
    {
        // 근력
        strengthText = AddText(parent, "StrengthText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(-60f, 30f), new Vector2(110f, 22f), 14, TextAlignmentOptions.Left);
        strengthText.color = new Color(1f, 0.55f, 0.55f);
        strengthText.fontStyle = FontStyles.Bold;
        strengthText.text = "";

        // 약화
        weakText = AddText(parent, "WeakText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(60f, 30f), new Vector2(110f, 22f), 14, TextAlignmentOptions.Left);
        weakText.color = new Color(0.8f, 0.6f, 1f);
        weakText.fontStyle = FontStyles.Bold;
        weakText.text = "";
    }

    void BuildIntent(RectTransform parent)
    {
        intentRoot = new GameObject("IntentRoot", typeof(RectTransform), typeof(Image));
        intentRoot.transform.SetParent(parent, false);
        var iRt = (RectTransform)intentRoot.transform;
        iRt.anchorMin = new Vector2(0.5f, 0f);
        iRt.anchorMax = new Vector2(0.5f, 0f);
        iRt.pivot = new Vector2(0.5f, 0f);
        iRt.anchoredPosition = new Vector2(0f, 105f);
        iRt.sizeDelta = new Vector2(220f, 60f);
        var bg = intentRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        // 아이콘
        GameObject iconGo = new GameObject("IntentIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(iRt, false);
        var icRt = (RectTransform)iconGo.transform;
        icRt.anchorMin = new Vector2(0f, 0.5f);
        icRt.anchorMax = new Vector2(0f, 0.5f);
        icRt.pivot = new Vector2(0f, 0.5f);
        icRt.anchoredPosition = new Vector2(6f, 0f);
        icRt.sizeDelta = new Vector2(40f, 40f);
        intentIcon = iconGo.GetComponent<Image>();
        intentIcon.color = attackColor;

        // 텍스트 영역: 아이콘 우측(54)부터 패널 우측(-8)까지 anchor stretch + offsetMin/Max 로 확장
        intentText = AddTextStretch(iRt, "IntentText",
            new Vector2(54f, 4f),    // offsetMin (왼쪽/아래 여백)
            new Vector2(-8f, -6f),   // offsetMax (오른쪽/위 여백)
            new Vector2(0f, 0.5f), new Vector2(1f, 1f),
            18, TextAlignmentOptions.Left);
        intentText.color = Color.white;
        intentText.fontStyle = FontStyles.Bold;
        intentText.enableAutoSizing = true;
        intentText.fontSizeMin = 12;
        intentText.fontSizeMax = 18;

        intentTurnText = AddTextStretch(iRt, "IntentTurnText",
            new Vector2(54f, 4f),
            new Vector2(-8f, -4f),
            new Vector2(0f, 0f), new Vector2(1f, 0.5f),
            13, TextAlignmentOptions.Left);
        intentTurnText.color = new Color(0.85f, 0.85f, 0.85f);
        intentTurnText.enableAutoSizing = true;
        intentTurnText.fontSizeMin = 10;
        intentTurnText.fontSizeMax = 13;

        intentRoot.SetActive(false);
    }

    // 패널 한쪽 끝(왼쪽 아이콘 옆)부터 반대편(오른쪽 끝)까지 늘어나는 가변 텍스트 영역
    TextMeshProUGUI AddTextStretch(RectTransform parent, string name,
        Vector2 offsetMin, Vector2 offsetMax,
        Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    TextMeshProUGUI AddText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    void LateUpdate()
    {
        if (monster == null || monster.gameObject == null)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = monster.GetUIAnchorWorld();
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        // 사망 후에는 위치만 유지하고 정보 갱신 중단 (인텐트도 항상 숨김)
        if (!monster.IsAlive)
        {
            if (intentRoot != null && intentRoot.activeSelf) intentRoot.SetActive(false);
            return;
        }
        Refresh();
    }

    public void RefreshAll()
    {
        Refresh();
        UpdateIntent();
    }

    public void Refresh()
    {
        if (monster == null || monster.data == null) return;

        float ratio = monster.data.maxHp > 0 ? (float)monster.currentHp / monster.data.maxHp : 0f;
        if (hpBar != null) hpBar.value = Mathf.Clamp01(ratio);
        if (hpText != null) hpText.text = $"{Mathf.Max(0, monster.currentHp)}/{monster.data.maxHp}";

        if (defenseText != null)
            defenseText.text = monster.defense > 0 ? $"방어 {monster.defense}" : "";

        if (strengthText != null)
        {
            bool active = monster.strengthTurns > 0 && monster.strength != 0;
            strengthText.text = active
                ? $"근력 {(monster.strength > 0 ? "+" : "")}{monster.strength}/{monster.strengthTurns}T"
                : "";
        }

        if (weakText != null)
            weakText.text = monster.debuffTurns > 0 ? $"약화 {monster.debuffTurns}T" : "";
    }

    public void UpdateIntent()
    {
        if (intentRoot == null) return;
        // 죽었거나 유효한 행동이 없으면 인텐트 숨김
        if (monster == null || !monster.IsAlive)
        {
            intentRoot.SetActive(false);
            return;
        }
        var action = monster.nextAction;
        if (action == null)
        {
            intentRoot.SetActive(false);
            return;
        }
        intentRoot.SetActive(true);
        if (intentTurnText != null) intentTurnText.text = "";

        // 인텐트는 기획서 기술명 대신 generic 명칭만 사용 (공격/방어/강화/약화)
        switch (action.actionType)
        {
            case MonsterData.ActionType.Attack:
            {
                int dmg = action.value + monster.strength;
                if (GazeEffectManager.Instance != null)
                    dmg += GazeEffectManager.Instance.GetMonsterBonusAttack();
                if (intentText != null) intentText.text = $"공격 {dmg}";
                if (intentIcon != null) intentIcon.color = attackColor;
                break;
            }

            case MonsterData.ActionType.Defend:
                if (intentText != null) intentText.text = $"방어 {action.value}";
                if (intentIcon != null) intentIcon.color = defendColor;
                break;

            case MonsterData.ActionType.Buff:
                if (intentText != null) intentText.text = "강화";
                if (intentIcon != null) intentIcon.color = buffColor;
                if (intentTurnText != null) intentTurnText.text = $"{action.duration}턴";
                break;

            case MonsterData.ActionType.Debuff:
                if (intentText != null) intentText.text = "약화";
                if (intentIcon != null) intentIcon.color = debuffColor;
                if (intentTurnText != null) intentTurnText.text = $"{action.duration}턴";
                break;

            case MonsterData.ActionType.AttackAndDebuff:
            {
                int dmg2 = action.value + monster.strength;
                if (GazeEffectManager.Instance != null)
                    dmg2 += GazeEffectManager.Instance.GetMonsterBonusAttack();
                if (intentText != null) intentText.text = $"공격 {dmg2}";
                if (intentIcon != null) intentIcon.color = attackColor;
                if (intentTurnText != null) intentTurnText.text = $"약화 {action.duration}턴";
                break;
            }
        }
    }

    public void OnDeath()
    {
        if (intentRoot != null) intentRoot.SetActive(false);
        if (hpText != null) hpText.text = "0/0";
        if (hpBar != null) hpBar.value = 0f;
        Destroy(gameObject, 0.5f);
    }
}
