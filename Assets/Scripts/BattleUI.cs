using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    [Header("플레이어 UI (씬에 있던 옛 슬라이더 — 런타임 HP 바가 대체하면 비활성화됨)")]
    public Slider playerHPBar;
    public TextMeshProUGUI playerHPText;

    [Header("플레이어 HP 바 스타일 (런타임 자동 생성, 몬스터와 동일 외관)")]
    public bool useMonsterStyleHpBar = true;
    public Color playerHpBackColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    public Color playerHpFillColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Vector2 playerHpBarSizeOverride = Vector2.zero; // (0,0) 이면 씬 슬라이더 크기 유지
    private Slider runtimeHpBar;
    private TextMeshProUGUI runtimeHpText;
    private RectTransform runtimeHpBarBack;

    [Header("마나 UI")]
    public TextMeshProUGUI manaText;

    [Header("방어도 UI")]
    public TextMeshProUGUI playerDefenseText;

    [Header("플레이어 상태 아이콘 (런타임 자동 생성)")]
    public RectTransform playerStatusBarAnchor; // 비워두면 playerHPBar 위쪽에 자동 부착
    public Vector2 playerStatusBarOffset = new Vector2(0f, 36f);
    public Vector2 playerStatusIconSize = new Vector2(28f, 28f);
    private StatusIconBar playerStatusBar;

    [Header("상태 아이콘 스프라이트 (Assets/Sprites/Icons/ 의 파일을 각 칸에 드래그)")]
    public Sprite statusIconStrength;
    public Sprite statusIconWeak;
    public Sprite statusIconRegen;

    [Header("배틀 그래픽 (방어 배지 / 몬스터 인텐트 아이콘)")]
    public Sprite defenseIcon;  // 방어도 배지 배경 — 플레이어/몬스터 공통
    public Sprite strikeIcon;   // 몬스터 인텐트의 공격 표시

    [Header("플레이어 방어도 배지 (런타임 자동 생성, 몬스터 배지와 동일 스타일)")]
    public Color playerDefenseBadgeColor = new Color(0.15f, 0.55f, 0.85f);
    public Vector2 playerDefenseBadgeSize = new Vector2(60f, 60f);
    public Vector2 playerDefenseBadgeOffset = new Vector2(0f, 0f); // playerHPBar 좌단 기준 미세 조정
    private DefenseBadgeUI playerDefenseBadge;

    [Header("시선 게이지 UI")]
    public Slider gazeBar;
    public TextMeshProUGUI gazeText;

    [Header("덱 UI")]
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI discardCountText;

    [Header("시선 로그 UI")]
    public GameObject gazeLogPanel;
    public TextMeshProUGUI increaseTitleText;
    public TextMeshProUGUI increaseContentText;
    public TextMeshProUGUI decreaseTitleText;
    public TextMeshProUGUI decreaseContentText;
    public float gazeLogDisplayTime = 3f;

    private Color gazeBarDefaultColor;
    private Image gazeBarFillImage;
    private Coroutine gazeFlashCoroutine;
    private Coroutine gazeLogCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 인스펙터에 드래그된 스프라이트를 StatusInfo 정적 맵에 등록 — 비어있으면 폴백 사각형.
        StatusInfo.RegisterSprite(StatusType.Strength, statusIconStrength);
        StatusInfo.RegisterSprite(StatusType.Weak, statusIconWeak);
        StatusInfo.RegisterSprite(StatusType.Regen, statusIconRegen);

        // 배틀 공통 아이콘
        BattleIcons.RegisterDefense(defenseIcon);
        BattleIcons.RegisterStrike(strikeIcon);
    }

    void Start()
    {
        if (gazeBar != null)
        {
            gazeBarFillImage = gazeBar.fillRect.GetComponent<Image>();
            if (gazeBarFillImage != null)
                gazeBarDefaultColor = gazeBarFillImage.color;
        }

        if (gazeLogPanel != null)
            gazeLogPanel.SetActive(false);

        BuildPlayerHpBar();
        BuildPlayerDefenseBadge();
        BuildPlayerStatusBar();
    }

    // 몬스터 HP 바와 동일 구조: 다크 백드롭 + 빨간 슬라이더 + 중앙 HP 텍스트 오버레이.
    // 옛 씬 playerHPBar/playerHPText 는 비활성화하고 새 바가 대체.
    void BuildPlayerHpBar()
    {
        if (!useMonsterStyleHpBar || playerHPBar == null) return;
        var oldHp = (RectTransform)playerHPBar.transform;
        var parent = oldHp.parent;
        if (parent == null) return;

        Vector2 size = playerHpBarSizeOverride.sqrMagnitude > 0.01f
            ? playerHpBarSizeOverride
            : oldHp.rect.size;

        // 백그라운드 (위치/앵커는 옛 슬라이더 기준 그대로 보존)
        GameObject bg = new GameObject("PlayerHPBarBack", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        runtimeHpBarBack = (RectTransform)bg.transform;
        runtimeHpBarBack.anchorMin = oldHp.anchorMin;
        runtimeHpBarBack.anchorMax = oldHp.anchorMax;
        runtimeHpBarBack.pivot = oldHp.pivot;
        runtimeHpBarBack.anchoredPosition = oldHp.anchoredPosition;
        runtimeHpBarBack.sizeDelta = size;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = playerHpBackColor;
        bgImg.raycastTarget = false;

        // 슬라이더
        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(runtimeHpBarBack, false);
        var sRt = (RectTransform)sliderGo.transform;
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
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = playerHpFillColor;
        fillImg.raycastTarget = false;

        runtimeHpBar = sliderGo.GetComponent<Slider>();
        runtimeHpBar.transition = Selectable.Transition.None;
        runtimeHpBar.fillRect = fRt;
        runtimeHpBar.targetGraphic = null;
        runtimeHpBar.minValue = 0f;
        runtimeHpBar.maxValue = 1f;
        runtimeHpBar.value = 1f;
        runtimeHpBar.interactable = false;

        // 중앙 HP 텍스트 오버레이
        GameObject txt = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txt.transform.SetParent(runtimeHpBarBack, false);
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        runtimeHpText = txt.GetComponent<TextMeshProUGUI>();
        runtimeHpText.alignment = TextAlignmentOptions.Center;
        runtimeHpText.fontSize = 18;
        runtimeHpText.color = Color.white;
        runtimeHpText.fontStyle = FontStyles.Bold;
        runtimeHpText.raycastTarget = false;

        // 옛 씬 슬라이더/텍스트 비활성
        playerHPBar.gameObject.SetActive(false);
        if (playerHPText != null) playerHPText.gameObject.SetActive(false);
    }

    // 배지/상태바가 위치 기준으로 삼을 RectTransform — 런타임 바가 있으면 그것을, 없으면 옛 슬라이더.
    RectTransform GetHpBarReference()
    {
        if (runtimeHpBarBack != null) return runtimeHpBarBack;
        if (playerHPBar != null) return (RectTransform)playerHPBar.transform;
        return null;
    }

    // HP 바 좌단에 방어도 배지 부착. 옛 playerDefenseText 는 비활성화.
    void BuildPlayerDefenseBadge()
    {
        var hpRt = GetHpBarReference();
        if (hpRt == null || hpRt.parent == null) return;

        float leftX = hpRt.anchoredPosition.x - hpRt.rect.width * hpRt.pivot.x;
        float midY = hpRt.anchoredPosition.y + hpRt.rect.height * (0.5f - hpRt.pivot.y);
        Vector2 anchored = new Vector2(leftX, midY) + playerDefenseBadgeOffset;

        playerDefenseBadge = DefenseBadgeUI.Create(hpRt.parent,
            anchorMin: hpRt.anchorMin,
            anchorMax: hpRt.anchorMax,
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: anchored,
            size: playerDefenseBadgeSize,
            bgColor: playerDefenseBadgeColor);

        if (playerDefenseText != null)
            playerDefenseText.gameObject.SetActive(false);
    }

    void BuildPlayerStatusBar()
    {
        var hpRt = GetHpBarReference();
        Transform parent = playerStatusBarAnchor != null
            ? (Transform)playerStatusBarAnchor
            : (hpRt != null ? hpRt.parent : null);
        if (parent == null) return;

        Vector2 anchorMin = new Vector2(0f, 1f);
        Vector2 anchorMax = new Vector2(0f, 1f);
        Vector2 pivot = new Vector2(0f, 0f);
        Vector2 anchored = playerStatusBarOffset;
        Vector2 size = new Vector2(240f, playerStatusIconSize.y + 4f);

        if (hpRt != null && playerStatusBarAnchor == null)
        {
            // HP 바의 좌상단 + 위쪽 offset 으로 정렬
            anchored = new Vector2(hpRt.anchoredPosition.x - hpRt.rect.width * hpRt.pivot.x,
                                   hpRt.anchoredPosition.y + hpRt.rect.height * (1f - hpRt.pivot.y) + playerStatusBarOffset.y);
            anchorMin = hpRt.anchorMin;
            anchorMax = hpRt.anchorMax;
        }

        playerStatusBar = StatusIconBar.Create(parent,
            anchorMin: anchorMin,
            anchorMax: anchorMax,
            pivot: pivot,
            anchoredPos: anchored,
            size: size,
            iconSize: playerStatusIconSize);
    }

    public void UpdateUI()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        float hpRatio = bm.playerMaxHp > 0 ? (float)bm.playerCurrentHp / bm.playerMaxHp : 0f;
        if (runtimeHpBar != null)
        {
            runtimeHpBar.value = Mathf.Clamp01(hpRatio);
            if (runtimeHpText != null)
                runtimeHpText.text = $"{Mathf.Max(0, bm.playerCurrentHp)}/{bm.playerMaxHp}";
        }
        else
        {
            if (playerHPBar != null) playerHPBar.value = Mathf.Clamp01(hpRatio);
            if (playerHPText != null) playerHPText.text = $"{bm.playerCurrentHp}/{bm.playerMaxHp}";
        }

        if (manaText != null)
            manaText.text = $"Mana: {bm.currentMana}/{bm.maxMana}";

        if (playerDefenseBadge != null)
            playerDefenseBadge.SetValue(bm.playerDefense);
        else if (playerDefenseText != null) // 폴백: playerHPBar 가 없으면 옛 텍스트 사용
            playerDefenseText.text = bm.playerDefense > 0 ? $"방어 {bm.playerDefense}" : "";

        if (gazeBar != null)
            gazeBar.value = (float)bm.gazeLevel / 100f;
        if (gazeText != null)
            gazeText.text = $"시선: {bm.gazeLevel}";

        if (deckCountText != null)
            deckCountText.text = $"덱: {bm.deck.Count}";
        if (discardCountText != null)
            discardCountText.text = $"버림: {bm.discardPile.Count}";

        if (playerStatusBar != null)
            playerStatusBar.RefreshFromPlayer(bm);
    }

    // 레거시 인텐트 UI 갱신 - 다중 몬스터에서는 MonsterRuntimeUI 가 자동 갱신.
    public void UpdateMonsterIntent()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.runtimeUI == null) continue;
            m.runtimeUI.UpdateIntent();
        }
    }

    public void FlashGazeBar(bool isIncrease)
    {
        if (gazeBarFillImage == null) return;
        if (gazeFlashCoroutine != null) StopCoroutine(gazeFlashCoroutine);
        gazeFlashCoroutine = StartCoroutine(GazeFlashCoroutine(isIncrease));
    }

    IEnumerator GazeFlashCoroutine(bool isIncrease)
    {
        Color flashColor = isIncrease ? Color.red : Color.cyan;
        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            gazeBarFillImage.color = Color.Lerp(gazeBarDefaultColor, flashColor, elapsed / (duration * 0.5f));
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            gazeBarFillImage.color = Color.Lerp(flashColor, gazeBarDefaultColor, elapsed / (duration * 0.5f));
            yield return null;
        }

        gazeBarFillImage.color = gazeBarDefaultColor;
    }

    public void ShowGazeLog(List<string> log)
    {
        if (gazeLogPanel == null) return;
        if (log == null || log.Count == 0) return;

        if (gazeLogCoroutine != null) StopCoroutine(gazeLogCoroutine);
        gazeLogCoroutine = StartCoroutine(ShowGazeLogCoroutine(log));
    }

    IEnumerator ShowGazeLogCoroutine(List<string> log)
    {
        string increaseContent = "";
        string decreaseContent = "";

        foreach (string entry in log)
        {
            int lastSpace = entry.LastIndexOf(' ');
            if (lastSpace < 0)
            {
                increaseContent += entry + "\n";
                continue;
            }

            string cardName = entry.Substring(0, lastSpace);
            string amountStr = entry.Substring(lastSpace + 1);

            if (amountStr.StartsWith("+"))
                increaseContent += $"{cardName} <color=red>{amountStr}</color>\n";
            else if (amountStr.StartsWith("-"))
                decreaseContent += $"{cardName} <color=green>{amountStr}</color>\n";
        }

        if (increaseTitleText != null)
            increaseTitleText.text = "- 증가";
        if (decreaseTitleText != null)
            decreaseTitleText.text = "- 감소";
        if (increaseContentText != null)
            increaseContentText.text = increaseContent;
        if (decreaseContentText != null)
            decreaseContentText.text = decreaseContent;

        gazeLogPanel.SetActive(true);

        yield return new WaitForSeconds(gazeLogDisplayTime);

        gazeLogPanel.SetActive(false);
    }
}