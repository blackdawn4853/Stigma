using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 플레이어 GameObject 위에 떠다니는 월드 스페이스 캔버스.
// HP 바 + 방어도 배지 + 상태 아이콘 바 — MonsterRuntimeUI 와 동일 외관.
// BattleManager.player* 상태를 직접 읽어 매 프레임 갱신.
public class PlayerRuntimeUI : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 1.4f, 0f);

    [Header("레이아웃")]
    public Vector2 canvasSize = new Vector2(3.2f, 2.0f);
    public float canvasScale = 0.01f;

    [Header("색상")]
    public Color hpFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Color hpBackColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color defendColor = new Color(0.15f, 0.55f, 0.85f);

    private Slider hpBar;
    private Image hpFill;
    private TextMeshProUGUI hpText;
    private DefenseBadgeUI defenseBadge;
    private BodyDefenseUI bodyDefense;
    private StatusIconBar statusBar;

    public static PlayerRuntimeUI CreateFor(Transform playerTransform)
    {
        if (playerTransform == null) return null;
        GameObject go = new GameObject("PlayerRuntimeUI");
        // 부모를 따라가지 않고 별도 루트로 두고 LateUpdate 위치 추적 (몬스터와 동일 패턴).
        // 플레이어 localScale 변동/플립 영향을 받지 않게 하기 위함.
        go.transform.SetParent(null);
        go.transform.localScale = Vector3.one;

        var ui = go.AddComponent<PlayerRuntimeUI>();
        ui.target = playerTransform;
        ui.Build();
        return ui;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        scaler.referencePixelsPerUnit = 100f;
        gameObject.AddComponent<GraphicRaycaster>();

        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(canvasSize.x / canvasScale, canvasSize.y / canvasScale);
        rt.localScale = Vector3.one * canvasScale;

        BuildHpBar(rt);
        BuildDefenseBadge(rt);
        BuildStatusBar(rt);

        // 방어도 — 캐릭터 몸에 큰 반투명 방패 오버레이 (자체 월드 캔버스, 작은 배지와 별도)
        bodyDefense = BodyDefenseUI.CreateFor(target, new Vector3(0f, 0.9f, 0f), flipX: false); // 방패 면이 오른쪽(몬스터)을 향함 — 스프라이트 기본 방향
    }

    void BuildHpBar(RectTransform parent)
    {
        GameObject bg = new GameObject("HPBarBack", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = new Vector2(0.5f, 0f);
        bgRt.anchorMax = new Vector2(0.5f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = new Vector2(0f, 60f);
        bgRt.sizeDelta = new Vector2(220f, 28f);
        bg.GetComponent<Image>().color = hpBackColor;

        GameObject slider = new GameObject("HPBar", typeof(RectTransform), typeof(Slider));
        slider.transform.SetParent(bgRt, false);
        var sRt = (RectTransform)slider.transform;
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(2f, 2f);
        sRt.offsetMax = new Vector2(-2f, -2f);

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

        GameObject txt = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(bgRt, false);
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        hpText = txt.GetComponent<TextMeshProUGUI>();
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 18;
        hpText.color = Color.white;
        hpText.fontStyle = FontStyles.Bold;
        hpText.raycastTarget = false;
        hpText.text = "0/0";
    }

    void BuildDefenseBadge(RectTransform parent)
    {
        defenseBadge = DefenseBadgeUI.Create(parent,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(-100f, 74f),
            size: new Vector2(60f, 60f),
            bgColor: defendColor);
    }

    void BuildStatusBar(RectTransform parent)
    {
        statusBar = StatusIconBar.Create(parent,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            pivot: new Vector2(0f, 0f),
            anchoredPos: new Vector2(-110f, 92f),
            size: new Vector2(220f, 28f),
            iconSize: new Vector2(28f, 28f));
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        transform.position = target.position + worldOffset;
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
        Refresh();
    }

    public void Refresh()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        float ratio = bm.playerMaxHp > 0 ? (float)bm.playerCurrentHp / bm.playerMaxHp : 0f;
        if (hpBar != null) hpBar.value = Mathf.Clamp01(ratio);
        if (hpText != null) hpText.text = $"{Mathf.Max(0, bm.playerCurrentHp)}/{bm.playerMaxHp}";

        if (defenseBadge != null) defenseBadge.SetValue(bm.playerDefense);
        if (bodyDefense != null) bodyDefense.SetValue(bm.playerDefense);
        if (statusBar != null) statusBar.RefreshFromPlayer(bm);
    }

    void OnDestroy()
    {
        if (bodyDefense != null) Destroy(bodyDefense.gameObject);
    }
}
