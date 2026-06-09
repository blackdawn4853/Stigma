using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 상점 — 노드맵 위에 뜨는 풀스크린 오버레이 (별도 씬 X).
//  카드 구매 / 체력 회복(포션) / 리롤. 골드는 상단 HUD 를 그대로 사용(여기선 안 그림).
//  카드 아래 가격 표시 + 못 사면 카드 회색 + 가격 빨강.
//  전부 코드 절차생성. sortingOrder 900 → HUD(1000) 아래라 HUD 골드/HP 가 위에 보임.
public class ShopUI : MonoBehaviour
{
    [Header("설정 (조정 가능)")]
    public int cardCount = 5;
    public int potionPrice = 85;   // 상점당 1회 · 조금 비싸게
    public int potionHeal = 30;
    public int rerollPrice = 20;

    [Header("외신 상인 (왼쪽) — 값만 바꿔 조정")]
    public Vector2 merchantPos = new Vector2(-600f, -10f);
    public float merchantHeight = 820f;
    // 와이드 일러스트에서 상인만 크롭 (정규화 0~1): x=좌, y=위(상단기준), z=우, w=아래(상단기준)
    public Vector4 merchantCrop = new Vector4(0.14f, 0.13f, 0.63f, 0.99f);

    // 색
    static readonly Color colDim = new Color(0.03f, 0.022f, 0.045f, 0.93f);   // 거의 검정 보랏빛 (상점 입장감)
    static readonly Color colPanel = new Color(0.07f, 0.062f, 0.092f, 0.985f); // 어두운 석재 제단
    static readonly Color colPanelEdge = new Color(0.5f, 0.42f, 0.22f, 0.85f);
    static readonly Color colText = new Color(0.93f, 0.9f, 0.95f, 1f);
    static readonly Color colGold = new Color(0.95f, 0.82f, 0.35f, 1f);
    static readonly Color colCantAfford = new Color(0.95f, 0.32f, 0.3f, 1f);
    static readonly Color colBtn = new Color(0.18f, 0.14f, 0.22f, 1f);
    static readonly Color colBtnDisabled = new Color(0.13f, 0.12f, 0.15f, 1f);
    static readonly Color colPotionGlass = new Color(0.16f, 0.2f, 0.26f, 0.92f);
    static readonly Color colPotionLiquid = new Color(0.86f, 0.22f, 0.26f, 1f);
    static readonly Color colCork = new Color(0.45f, 0.32f, 0.2f, 1f);

    class Offer
    {
        public CardData card;
        public int price;
        public GameObject root;
        public CanvasGroup cg;       // 회색 처리용
        public Button buyBtn;
        public TextMeshProUGUI priceText;
    }

    System.Action onClose;
    Canvas canvas;
    CanvasGroup group;
    TMP_FontAsset font;
    GameObject cardPrefab;
    GameObject drawingUICanvas;   // 상점 동안 숨길 노드맵 드로잉 버튼 바 (sortingOrder 1100)
    Transform cardRow;
    readonly List<Offer> offers = new List<Offer>();

    Button potionBtn; TextMeshProUGUI potionLabel; CanvasGroup potionGroup;
    Image potionLiquid; bool potionUsed;
    Button rerollBtn; TextMeshProUGUI rerollLabel;
    bool closing;

    // 상인 말풍선
    CanvasGroup bubbleCg; TextMeshProUGUI bubbleText;
    Coroutine bubbleRoutine; int lastLineIdx = -1;

    // 돈 없을 때 상인 대사 (클릭마다 랜덤)
    static readonly string[] NoGoldLines =
    {
        "대가 없이는… 아무것도 가질 수 없다.",
        "그 손, 비어 있군. 금을 가져오게.",
        "공짜를 바라는가? 어리석은 것.",
        "동전 소리가… 들리지 않는군.",
        "네 빈손이 나를 모욕하는구나.",
        "탐욕은 좋다. 허나 값은 치러야지.",
        "금이 부족해. 썩 물러나라.",
        "거래는 피와 금으로만 이루어진다.",
    };
    const string FullHpLine = "멀쩡한 자에게… 약은 사치다.";

    // ── 진입 ─────────────────────────────────────────────────────
    public static ShopUI Spawn(System.Action onClose)
    {
        var go = new GameObject("ShopUI");
        var ui = go.AddComponent<ShopUI>();
        ui.onClose = onClose;
        ui.Begin();
        return ui;
    }

    void Begin()
    {
        font = TMP_Settings.defaultFontAsset;
        cardPrefab = Resources.Load<GameObject>("CardPrefab");   // 전투와 동일한 카드 프리팹 (Assets/Resources)

        // 노드맵 드로잉 버튼 바(sortingOrder 1100)가 상점 위로 뚫고 나오므로 상점 동안 숨김.
        // GameObject 통째가 아니라 자식 캔버스만 끔 (같은 GO 의 MapDrawingManager 는 살려둠).
        var dui = FindFirstObjectByType<MapDrawingUI>();
        if (dui != null)
        {
            var t = dui.transform.Find("MapDrawingUICanvas");
            if (t != null) { drawingUICanvas = t.gameObject; drawingUICanvas.SetActive(false); }
        }

        BuildUI();
        RollCards();
        RefreshAffordability();
        StartCoroutine(FadeIn());
    }

    int Gold => GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
    int Hp => GameManager.Instance != null ? GameManager.Instance.playerCurrentHp : 0;
    int MaxHp => GameManager.Instance != null ? GameManager.Instance.playerMaxHp : 0;

    // ── UI 빌드 ──────────────────────────────────────────────────
    void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // HUD(1000) 아래 → HUD 가 위에 보임
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();

        // 딤 (맵 클릭 차단)
        var dim = NewImage(transform, "Dim", colDim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 음산한 분위기 (중앙 글로우 + 비네트 + 그레인 + 떠오르는 불티)
        BuildAtmosphere();

        // 외신 상인 (왼쪽)
        BuildMerchant();

        // 패널 (오른쪽 제단) — 어두운 석재. 상단 HUD(높이 64) 바로 아래 10px 간격에 고정.
        var panel = NewImage(transform, "Panel", colPanel);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 1f);   // 상단 앵커
        prt.anchoredPosition = new Vector2(385f, -74f);                       // top - 64(HUD) - 10(간격)
        prt.sizeDelta = new Vector2(1140f, 976f);
        StyleAltar(prt);

        // 제목
        AddText(prt, "✦  외신 상인의 제단  ✦", new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            46, FontStyles.Bold, colGold, new Vector2(1040f, 76f));

        // 카드 줄 (상단 절반) — 패널 중앙 앵커 기준
        var rowGO = new GameObject("CardRow", typeof(RectTransform));
        rowGO.transform.SetParent(prt, false);
        var rrt = rowGO.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(0f, 150f);
        rrt.sizeDelta = new Vector2(1100f, 420f);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        cardRow = rowGO.transform;

        // 포션(체력 회복) 모양 — 카드 줄 아래 중앙
        BuildPotion(prt, new Vector2(0f, -250f));

        // 유틸 줄: 리롤 / 돌아가기 (하단)
        rerollBtn = BuildButton(prt, "RerollButton", new Vector2(-270f, -420f), new Vector2(360f, 88f),
            out rerollLabel, () => OnReroll());

        TextMeshProUGUI retLbl;
        var ret = BuildButton(prt, "ReturnButton", new Vector2(270f, -420f), new Vector2(360f, 88f),
            out retLbl, () => Close());
        retLbl.text = "맵으로 돌아가기";
        var retImg = ret.GetComponent<Image>();
        if (retImg != null) retImg.color = new Color(0.22f, 0.22f, 0.28f, 1f);

        // 상인 말풍선 (왼쪽, 평소 숨김)
        BuildMerchantBubble();

        // 떠오르는 불티 (최상단, 클릭 비차단)
        BuildEmbers();
    }

    Button BuildButton(Transform parent, string name, Vector2 pos, Vector2 size,
                       out TextMeshProUGUI label, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = colBtn;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label = lblGO.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = 28f; label.fontStyle = FontStyles.Bold; label.color = colText;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return btn;
    }

    // ── 포션(체력 회복) 모양 — 코드 절차생성 ─────────────────────
    void BuildPotion(Transform parent, Vector2 centerPos)
    {
        // 컨테이너 (전체가 클릭 버튼)
        var go = new GameObject("Potion", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = centerPos;
        rt.sizeDelta = new Vector2(200f, 320f);
        rt.localScale = new Vector3(0.72f, 0.72f, 1f);   // 포션 살짝 작게 (호버 캡처 전에 설정)
        var hit = go.GetComponent<Image>();        // 투명 히트박스
        hit.color = new Color(0f, 0f, 0f, 0f);
        potionBtn = go.GetComponent<Button>();
        potionBtn.targetGraphic = hit;
        potionBtn.transition = Selectable.Transition.None;
        potionBtn.onClick.AddListener(() => OnPotion());
        potionGroup = go.GetComponent<CanvasGroup>();
        go.AddComponent<ShopHoverGrow>();          // 마우스 호버 시 살짝 확대

        var rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        // 코르크 (상단)
        var cork = PotionPart(rt, "Cork", new Vector2(0f, 132f), new Vector2(58f, 34f), colCork, rounded);
        // 코르크 테 (밝은 띠)
        PotionPart(cork.rectTransform, "CorkTop", new Vector2(0f, 12f), new Vector2(64f, 12f),
            new Color(0.56f, 0.42f, 0.28f, 1f), rounded);
        // 병목
        PotionPart(rt, "Neck", new Vector2(0f, 100f), new Vector2(46f, 46f), colPotionGlass, rounded);

        // 병 몸통 (둥근 유리)
        var body = PotionPart(rt, "Body", new Vector2(0f, 18f), new Vector2(152f, 172f), colPotionGlass, rounded);

        // 액체 (몸통 하단 채움) — 둥근 사각, 바닥 정렬
        var liq = new GameObject("Liquid", typeof(RectTransform), typeof(Image));
        liq.transform.SetParent(body.transform, false);
        var lrt = liq.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0f); lrt.anchorMax = new Vector2(0.5f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 8f);
        lrt.sizeDelta = new Vector2(130f, 112f);
        potionLiquid = liq.GetComponent<Image>();
        potionLiquid.color = colPotionLiquid;
        potionLiquid.sprite = rounded; potionLiquid.type = Image.Type.Sliced;
        potionLiquid.raycastTarget = false;

        // 유리 하이라이트 (좌측 세로 광택)
        var shine = PotionPart(body.transform, "Shine", new Vector2(-44f, 10f), new Vector2(14f, 110f),
            new Color(1f, 1f, 1f, 0.12f), rounded);
        shine.raycastTarget = false;

        // 하트 마크 (액체 위)
        var heart = AddText(body.transform, "❤", new Vector2(0f, -10f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            54, FontStyles.Bold, new Color(1f, 0.92f, 0.92f, 0.95f), new Vector2(120f, 80f));
        heart.raycastTarget = false;

        // 라벨 (회복량/가격) — 병 아래
        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(rt, false);
        var lblrt = lblGO.GetComponent<RectTransform>();
        lblrt.anchorMin = lblrt.anchorMax = lblrt.pivot = new Vector2(0.5f, 0.5f);
        lblrt.anchoredPosition = new Vector2(0f, -128f);
        lblrt.sizeDelta = new Vector2(220f, 46f);
        potionLabel = lblGO.AddComponent<TextMeshProUGUI>();
        potionLabel.font = font; potionLabel.fontSize = 30f; potionLabel.fontStyle = FontStyles.Bold;
        potionLabel.alignment = TextAlignmentOptions.Center; potionLabel.color = colGold;
        potionLabel.text = $"+{potionHeal}   {potionPrice} ⦿";
        potionLabel.raycastTarget = false;
    }

    Image PotionPart(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite rounded)
    {
        var img = NewImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
        img.raycastTarget = false;
        return img;
    }

    // ── 카드 진열 ────────────────────────────────────────────────
    void RollCards()
    {
        offers.Clear();
        if (cardRow != null)
            for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);

        var pool = GetCardPool();
        if (pool.Count == 0) return;

        // 중복 없이 N장 뽑기 (풀이 작으면 가능한 만큼)
        var bag = new List<CardData>(pool);
        int n = Mathf.Min(cardCount, bag.Count);
        for (int i = 0; i < n; i++)
        {
            int idx = Random.Range(0, bag.Count);
            var card = bag[idx];
            bag.RemoveAt(idx);
            offers.Add(BuildOffer(card));
        }
    }

    List<CardData> GetCardPool()
    {
        var list = new List<CardData>();
        var gm = GameManager.Instance;
        if (gm != null && gm.allCards != null)
            foreach (var c in gm.allCards) if (c != null) list.Add(c);
        if (list.Count == 0 && gm != null && gm.startingDeck != null)
            foreach (var c in gm.startingDeck) if (c != null && !list.Contains(c)) list.Add(c);
        return list;
    }

    // 카드 표시 배율 (프리팹 원본 180x240 → 화면에 맞게 확대)
    const float CardScale = 1.12f;
    const float CardW = 180f, CardH = 240f;

    Offer BuildOffer(CardData card)
    {
        var o = new Offer { card = card, price = PriceFor(card) };

        float w = CardW * CardScale, h = CardH * CardScale;

        // 카드 컨테이너 (HLG 레이아웃 단위 = 카드 + 가격). 스케일은 레이아웃에 안 잡히므로 컨테이너 크기로 간격 확보.
        var item = new GameObject("Offer", typeof(RectTransform), typeof(LayoutElement));
        item.transform.SetParent(cardRow, false);
        var irt = item.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(w, h + 64f);
        var le = item.GetComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h + 64f;

        // 카드 뒤 등급 글로우 (희귀할수록 강하게) — 카드보다 먼저 = 뒤
        var glow = NewImage(item.transform, "RarityGlow", Color.white);
        glow.sprite = GetRadialSprite();
        var grt = glow.rectTransform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(0f, 32f);
        float gi = RarityGlowIntensity(card.rarity);
        grt.sizeDelta = new Vector2(w * 1.7f, h * 1.5f);
        var gc = card.GetRarityColor(); gc.a = gi;
        glow.color = gc;
        glow.raycastTarget = false;
        if (card.rarity >= CardData.CardRarity.Legendary)
            StartCoroutine(PulseGlow(glow, gi));     // 전설+ 는 은은히 맥동

        // ── 전투와 동일한 카드 프리팹을 그대로 인스턴스화 ──
        var cardGO = Instantiate(cardPrefab, item.transform);
        cardGO.name = "Card";
        var crt = cardGO.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.localScale = new Vector3(CardScale, CardScale, 1f);
        crt.anchoredPosition = new Vector2(0f, 32f);

        var cardUI = cardGO.GetComponent<CardUI>();
        if (cardUI != null) cardUI.Setup(card);              // cost박스/아이콘/일러/이름/설명 자동 세팅

        o.root = cardGO;
        o.cg = cardGO.GetComponent<CanvasGroup>();
        if (o.cg == null) o.cg = cardGO.AddComponent<CanvasGroup>();   // 못 사면 회색 처리용
        o.buyBtn = cardGO.GetComponent<Button>();
        if (o.buyBtn != null)
        {
            o.buyBtn.onClick.RemoveAllListeners();           // 전투용 리스너 제거 → 구매로 교체
            o.buyBtn.onClick.AddListener(() => OnBuy(o));
        }

        // 가격 (카드 아래)
        var priceGO = new GameObject("Price", typeof(RectTransform));
        priceGO.transform.SetParent(item.transform, false);
        var prt = priceGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 6f);
        prt.sizeDelta = new Vector2(w, 44f);
        o.priceText = priceGO.AddComponent<TextMeshProUGUI>();
        o.priceText.font = font; o.priceText.fontSize = 30f; o.priceText.fontStyle = FontStyles.Bold;
        o.priceText.alignment = TextAlignmentOptions.Center; o.priceText.color = colGold;
        o.priceText.text = $"{o.price} ⦿";
        o.priceText.raycastTarget = false;

        return o;
    }

    int PriceFor(CardData c)
    {
        switch (c.rarity)
        {
            case CardData.CardRarity.Common: return Random.Range(45, 56);
            case CardData.CardRarity.Rare: return Random.Range(70, 91);
            case CardData.CardRarity.Advanced: return Random.Range(120, 146);
            case CardData.CardRarity.Legendary: return Random.Range(180, 216);
            case CardData.CardRarity.Mythic: return Random.Range(260, 301);
        }
        return 50;
    }

    // ── 거래 ─────────────────────────────────────────────────────
    void OnBuy(Offer o)
    {
        if (o == null) return;
        if (Gold < o.price) { ShowMerchantTaunt(); return; }   // 돈 없으면 상인이 한마디
        SpendGold(o.price);
        if (GameManager.Instance != null) GameManager.Instance.AddCardToDeck(o.card);
        Save();
        RefreshAffordability();
    }

    void OnPotion()
    {
        if (potionUsed) return;
        if (Hp >= MaxHp) { ShowMerchantLine(FullHpLine); return; }
        if (Gold < potionPrice) { ShowMerchantTaunt(); return; }   // 돈 없으면 상인이 한마디
        SpendGold(potionPrice);
        if (GameManager.Instance != null)
            GameManager.Instance.playerCurrentHp = Mathf.Min(MaxHp, Hp + potionHeal);
        Save();
        potionUsed = true;
        RefreshAffordability();
    }

    void OnReroll()
    {
        if (Gold < rerollPrice) return;
        SpendGold(rerollPrice);
        Save();
        RollCards();
        RefreshAffordability();
    }

    void SpendGold(int n)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.playerGold = Mathf.Max(0, GameManager.Instance.playerGold - n);
    }

    void Save() { if (GameManager.Instance != null) GameManager.Instance.Save(); }

    // ── 상태 갱신 (회색/빨강/비활성) ─────────────────────────────
    void RefreshAffordability()
    {
        foreach (var o in offers)
        {
            if (o == null) continue;
            bool afford = Gold >= o.price;
            if (o.cg != null) o.cg.alpha = afford ? 1f : 0.45f;           // 못 사면 카드 회색
            if (o.priceText != null) o.priceText.color = afford ? colGold : colCantAfford; // 가격 빨강 강조
            if (o.buyBtn != null) o.buyBtn.interactable = true;           // 못 사도 클릭은 받아 상인 대사 트리거
        }

        // 회복 포션 (상점당 1회)
        bool canPotion = !potionUsed && Gold >= potionPrice && Hp < MaxHp;
        if (potionGroup != null)
        {
            potionGroup.alpha = canPotion ? 1f : 0.45f;
            // 다 쓴 게 아니면 클릭은 받게(돈 없을 때 상인 대사). 다 쓰면 비활성.
            potionGroup.interactable = !potionUsed;
            potionGroup.blocksRaycasts = !potionUsed;
        }
        if (potionLiquid != null)
            potionLiquid.color = canPotion ? colPotionLiquid : new Color(0.45f, 0.2f, 0.22f, 1f);
        if (potionLabel != null)
        {
            if (potionUsed) { potionLabel.text = "회복 완료"; potionLabel.color = new Color(0.6f, 0.6f, 0.64f, 1f); }
            else if (Hp >= MaxHp) { potionLabel.text = "체력 가득"; potionLabel.color = new Color(0.6f, 0.6f, 0.64f, 1f); }
            else
            {
                potionLabel.text = $"+{potionHeal}   {potionPrice} ⦿";
                potionLabel.color = (Gold >= potionPrice) ? colGold : colCantAfford;
            }
        }

        // 리롤
        bool canReroll = Gold >= rerollPrice;
        if (rerollBtn != null)
        {
            rerollBtn.interactable = canReroll;
            var img = rerollBtn.GetComponent<Image>();
            if (img != null) img.color = canReroll ? colBtn : colBtnDisabled;
        }
        if (rerollLabel != null)
        {
            rerollLabel.text = $"🔄 리롤   {rerollPrice} ⦿";
            rerollLabel.color = canReroll ? colText : colCantAfford;
        }
    }

    // ── 열기/닫기 연출 ───────────────────────────────────────────
    IEnumerator FadeIn()
    {
        group.alpha = 0f;
        float t = 0f, dur = 0.25f;
        while (t < dur) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / dur); yield return null; }
        group.alpha = 1f;
    }

    void Close()
    {
        if (closing) return;
        closing = true;
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        float t = 0f, dur = 0.22f, start = group.alpha;
        while (t < dur) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Lerp(start, 0f, t / dur); yield return null; }
        if (drawingUICanvas != null) drawingUICanvas.SetActive(true);   // 드로잉 버튼 바 복구
        onClose?.Invoke();
        Destroy(gameObject);
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────
    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    TextMeshProUGUI AddText(Transform parent, string text, Vector2 pos, Vector2 aMin, Vector2 aMax,
                            int size, FontStyles style, Color color, Vector2 sizeDelta)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    void Border(RectTransform target, Color color)
    {
        Edge(target, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2.5f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2.5f));
        Edge(target, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2.5f, 0f));
        Edge(target, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2.5f, 0f));
    }

    void Edge(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 size)
    {
        var img = NewImage(parent, "Edge", color);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
    }

    // ── Phase 1: 분위기 연출 ─────────────────────────────────────
    static Sprite _spRadial, _spVignette, _spGrain;

    void BuildAtmosphere()
    {
        // 중앙 핏빛/보랏 글로우 — 오른쪽 제단 뒤로 (왼쪽은 어둡게 두어 상인 검정배경 블렌딩)
        var glow = NewImage(transform, "CenterGlow", new Color(0.42f, 0.16f, 0.4f, 0.5f));
        glow.sprite = GetRadialSprite();
        var grt = glow.rectTransform;
        grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = new Vector2(385f, 20f);
        grt.sizeDelta = new Vector2(1700f, 1400f);
        glow.raycastTarget = false;
        StartCoroutine(BreatheGlow(glow));

        // 가장자리 비네트
        var vig = NewImage(transform, "Vignette", new Color(0f, 0f, 0f, 0.8f));
        vig.sprite = GetVignetteSprite();
        Stretch(vig.rectTransform);
        vig.raycastTarget = false;

        // 그레인 (미세 노이즈)
        var grain = NewImage(transform, "Grain", new Color(1f, 1f, 1f, 0.028f));
        grain.sprite = GetGrainSprite();
        grain.type = Image.Type.Tiled;
        Stretch(grain.rectTransform);
        grain.raycastTarget = false;
    }

    // 외신 상인 — 와이드 일러스트에서 상인만 크롭해 왼쪽에 세움
    void BuildMerchant()
    {
        var sp = Resources.Load<Sprite>("Shop/Merchant");
        if (sp == null || sp.texture == null) return;
        var tex = sp.texture;
        float W = tex.width, H = tex.height;
        float xMin = merchantCrop.x, yTop = merchantCrop.y, xMax = merchantCrop.z, yBot = merchantCrop.w;
        var rect = new Rect(xMin * W, (1f - yBot) * H, (xMax - xMin) * W, (yBot - yTop) * H);
        var cropped = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);

        var go = new GameObject("Merchant", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = merchantPos;
        float aspect = rect.width / rect.height;
        rt.sizeDelta = new Vector2(merchantHeight * aspect, merchantHeight);
        var img = go.GetComponent<Image>();
        img.sprite = cropped;
        img.preserveAspect = true;
        img.raycastTarget = false;
        StartCoroutine(MerchantBreathe(rt));    // 미세한 호흡
    }

    IEnumerator MerchantBreathe(RectTransform rt)
    {
        Vector2 home = rt.anchoredPosition;
        float t = 0f;
        while (rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.anchoredPosition = home + new Vector2(0f, Mathf.Sin(t * 1.1f) * 6f);
            yield return null;
        }
    }

    // 상인 말풍선 (왼쪽 상인 곁) — 금빛 명패 톤. 평소 숨김, 클릭 시 대사 표시.
    void BuildMerchantBubble()
    {
        var rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        var go = new GameObject("MerchantBubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-470f, -90f);    // 얼굴(눈) 아래로
        rt.sizeDelta = new Vector2(540f, 160f);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.05f, 0.08f, 0.96f);
        bg.sprite = rounded; bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;
        Border(rt, new Color(0.62f, 0.5f, 0.26f, 0.9f));   // 금빛 테두리

        // 명패 (이름)
        AddText(rt, "✦  외신 상인  ✦", new Vector2(0f, -12f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            22, FontStyles.Bold, new Color(0.86f, 0.62f, 0.26f, 1f), new Vector2(500f, 32f)).raycastTarget = false;

        // 대사 본문
        bubbleText = AddText(rt, "", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            26, FontStyles.Italic, new Color(0.92f, 0.86f, 0.78f, 1f), new Vector2(496f, 96f));
        bubbleText.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        bubbleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bubbleText.alignment = TextAlignmentOptions.Center;
        bubbleText.enableWordWrapping = true;
        bubbleText.enableAutoSizing = true; bubbleText.fontSizeMin = 18f; bubbleText.fontSizeMax = 28f;
        bubbleText.raycastTarget = false;

        // 위쪽 꼬리 (얼굴 쪽을 향한 작은 마름모)
        var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tail.transform.SetParent(rt, false);
        var trt = tail.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(-40f, 6f);
        trt.sizeDelta = new Vector2(26f, 26f);
        trt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.08f, 0.96f);
        tail.GetComponent<Image>().raycastTarget = false;

        bubbleCg = go.GetComponent<CanvasGroup>();
        bubbleCg.alpha = 0f;
        bubbleCg.blocksRaycasts = false; bubbleCg.interactable = false;
    }

    void ShowMerchantTaunt()
    {
        int idx = Random.Range(0, NoGoldLines.Length);
        if (NoGoldLines.Length > 1 && idx == lastLineIdx) idx = (idx + 1) % NoGoldLines.Length;
        lastLineIdx = idx;
        ShowMerchantLine(NoGoldLines[idx]);
    }

    void ShowMerchantLine(string line)
    {
        if (bubbleText == null || bubbleCg == null) return;
        bubbleText.text = line;
        if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
        bubbleRoutine = StartCoroutine(BubbleRoutine());
    }

    IEnumerator BubbleRoutine()
    {
        // 등장 (살짝 펀치)
        float t = 0f;
        var rt = bubbleCg.GetComponent<RectTransform>();
        while (t < 0.16f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.16f;
            bubbleCg.alpha = k;
            float s = 0.92f + 0.08f * k;
            if (rt != null) rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        bubbleCg.alpha = 1f; if (rt != null) rt.localScale = Vector3.one;

        // 유지
        yield return new WaitForSecondsRealtime(2.6f);

        // 사라짐
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            bubbleCg.alpha = 1f - (t / 0.4f);
            yield return null;
        }
        bubbleCg.alpha = 0f;
        bubbleRoutine = null;
    }

    // 떠오르는 불티 — 패널 위에 은은하게 (BuildUI 마지막에 호출 → 최상단)
    void BuildEmbers()
    {
        var holder = new GameObject("Embers", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        Stretch(holder.GetComponent<RectTransform>());
        var hrt = holder.GetComponent<RectTransform>();
        hrt.SetAsLastSibling();

        for (int i = 0; i < 12; i++)
        {
            var e = NewImage(holder.transform, "Ember", new Color(0.95f, 0.55f, 0.25f, 0f));
            e.sprite = GetRadialSprite();
            e.raycastTarget = false;
            float s = Random.Range(5f, 13f);
            e.rectTransform.sizeDelta = new Vector2(s, s);
            StartCoroutine(EmberLoop(e));
        }
    }

    IEnumerator EmberLoop(Image e)
    {
        var rt = e.rectTransform;
        while (e != null)
        {
            float x = Random.Range(-980f, 980f);
            float startY = Random.Range(-560f, -400f);
            float rise = Random.Range(420f, 760f);
            float dur = Random.Range(4.5f, 8.5f);
            float sway = Random.Range(20f, 60f);
            float maxA = Random.Range(0.18f, 0.4f);
            float phase = Random.Range(0f, 6.28f);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                float a = Mathf.Sin(k * Mathf.PI) * maxA;        // 떠오르며 밝아졌다 사라짐
                rt.anchoredPosition = new Vector2(x + Mathf.Sin(phase + k * 6.28f) * sway, startY + rise * k);
                var c = e.color; c.a = a; e.color = c;
                yield return null;
            }
        }
    }

    IEnumerator BreatheGlow(Image g)
    {
        float baseA = g.color.a;
        float t = 0f;
        while (g != null)
        {
            t += Time.unscaledDeltaTime;
            var c = g.color;
            c.a = baseA + Mathf.Sin(t * 0.9f) * 0.08f;
            g.color = c;
            yield return null;
        }
    }

    IEnumerator PulseGlow(Image g, float baseA)
    {
        float t = 0f;
        while (g != null)
        {
            t += Time.unscaledDeltaTime;
            var c = g.color;
            c.a = baseA + Mathf.Sin(t * 2.2f) * (baseA * 0.4f);
            g.color = c;
            yield return null;
        }
    }

    float RarityGlowIntensity(CardData.CardRarity r)
    {
        switch (r)
        {
            case CardData.CardRarity.Common: return 0.16f;
            case CardData.CardRarity.Rare: return 0.26f;
            case CardData.CardRarity.Advanced: return 0.36f;
            case CardData.CardRarity.Legendary: return 0.48f;
            case CardData.CardRarity.Mythic: return 0.6f;
        }
        return 0.2f;
    }

    // 제단 패널 스타일 (이중 테두리 + 모서리 룬)
    void StyleAltar(RectTransform prt)
    {
        Border(prt, colPanelEdge);
        // 안쪽 얇은 테두리
        var inner = new GameObject("InnerEdge", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(prt, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(10f, 10f); irt.offsetMax = new Vector2(-10f, -10f);
        var iimg = inner.GetComponent<Image>();
        iimg.color = new Color(0f, 0f, 0f, 0f); iimg.raycastTarget = false;
        Border(irt, new Color(0.32f, 0.27f, 0.16f, 0.5f));
        // 네 모서리 룬 (작은 8각 인장 느낌 — 마름모 두 개 겹침)
        Vector2[] corners = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        foreach (var c in corners)
        {
            Vector2 off = new Vector2(c.x == 0f ? 30f : -30f, c.y == 1f ? -30f : 30f);
            CornerRune(prt, c, off);
        }
    }

    void CornerRune(RectTransform parent, Vector2 anchor, Vector2 offset)
    {
        for (int i = 0; i < 2; i++)
        {
            var r = new GameObject("Rune", typeof(RectTransform), typeof(Image));
            r.transform.SetParent(parent, false);
            var rt = r.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(20f, 20f);
            rt.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : 0f);
            var img = r.GetComponent<Image>();
            img.color = new Color(0.55f, 0.45f, 0.24f, 0.7f);
            img.raycastTarget = false;
        }
    }

    // ── 절차 스프라이트 ──────────────────────────────────────────
    Sprite GetRadialSprite() { if (_spRadial == null) _spRadial = MakeRadialSprite(128); return _spRadial; }
    Sprite GetVignetteSprite() { if (_spVignette == null) _spVignette = MakeVignetteSprite(128); return _spVignette; }
    Sprite GetGrainSprite() { if (_spGrain == null) _spGrain = MakeGrainSprite(64); return _spGrain; }

    Sprite MakeRadialSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                       // 가운데 진하고 가장자리로 부드럽게
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite MakeVignetteSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                a = a * a;
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite MakeGrainSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++)
        {
            float v = Random.value;
            px[i] = new Color(v, v, v, v);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

// 마우스 호버 시 살짝 확대 (상점 카드/포션 공용). 시간정지 무관하게 unscaled 사용.
public class ShopHoverGrow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScale = 1.1f;
    public float speed = 12f;
    Vector3 baseScale = Vector3.one;
    Vector3 target = Vector3.one;

    void Awake()
    {
        baseScale = transform.localScale;
        target = baseScale;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData e) { target = baseScale * hoverScale; }
    public void OnPointerExit(PointerEventData e) { target = baseScale; }
}
