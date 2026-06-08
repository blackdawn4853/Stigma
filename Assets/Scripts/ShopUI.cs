using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 상점 — 노드맵 위에 뜨는 풀스크린 오버레이 (별도 씬 X).
//  카드 구매 / 체력 회복(포션) / 리롤. 골드는 상단 HUD 를 그대로 사용(여기선 안 그림).
//  카드 아래 가격 표시 + 못 사면 카드 회색 + 가격 빨강.
//  전부 코드 절차생성. sortingOrder 900 → HUD(1000) 아래라 HUD 골드/HP 가 위에 보임.
public class ShopUI : MonoBehaviour
{
    [Header("설정 (조정 가능)")]
    public int cardCount = 5;
    public int potionPrice = 35;
    public int potionHeal = 25;
    public int rerollPrice = 20;

    // 색
    static readonly Color colDim = new Color(0f, 0f, 0f, 0.78f);
    static readonly Color colPanel = new Color(0.09f, 0.075f, 0.11f, 0.98f);
    static readonly Color colPanelEdge = new Color(0.5f, 0.42f, 0.22f, 0.85f);
    static readonly Color colText = new Color(0.93f, 0.9f, 0.95f, 1f);
    static readonly Color colGold = new Color(0.95f, 0.82f, 0.35f, 1f);
    static readonly Color colCantAfford = new Color(0.95f, 0.32f, 0.3f, 1f);
    static readonly Color colBtn = new Color(0.18f, 0.14f, 0.22f, 1f);
    static readonly Color colBtnDisabled = new Color(0.13f, 0.12f, 0.15f, 1f);

    class Offer
    {
        public CardData card;
        public int price;
        public GameObject root;
        public CanvasGroup cg;       // 회색 처리용
        public Button buyBtn;
        public TextMeshProUGUI priceText;
        public GameObject soldOverlay;
        public bool sold;
    }

    System.Action onClose;
    Canvas canvas;
    CanvasGroup group;
    TMP_FontAsset font;
    Transform cardRow;
    readonly List<Offer> offers = new List<Offer>();

    Button potionBtn; TextMeshProUGUI potionLabel; bool potionUsed;
    Button rerollBtn; TextMeshProUGUI rerollLabel;
    bool closing;

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

        // 패널 (HUD 아래로 내려 배치)
        var panel = NewImage(transform, "Panel", colPanel);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0f, -40f);
        prt.sizeDelta = new Vector2(1560f, 780f);
        Border(prt, colPanelEdge);

        // 제목
        AddText(prt, "✦  상  점  ✦", new Vector2(0f, -18f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            46, FontStyles.Bold, colGold, new Vector2(900f, 70f));
        AddText(prt, "골드는 상단에 표시됩니다", new Vector2(0f, -82f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            22, FontStyles.Italic, new Color(0.7f, 0.66f, 0.74f, 1f), new Vector2(900f, 36f));

        // 카드 줄
        var rowGO = new GameObject("CardRow", typeof(RectTransform));
        rowGO.transform.SetParent(prt, false);
        var rrt = rowGO.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(0f, 70f);
        rrt.sizeDelta = new Vector2(1480f, 360f);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 28f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        cardRow = rowGO.transform;

        // 유틸 줄: 리롤 / 회복
        rerollBtn = BuildButton(prt, "RerollButton", new Vector2(-260f, -250f), new Vector2(360f, 92f),
            out rerollLabel, () => OnReroll());
        potionBtn = BuildButton(prt, "PotionButton", new Vector2(260f, -250f), new Vector2(360f, 92f),
            out potionLabel, () => OnPotion());

        // 돌아가기
        TextMeshProUGUI retLbl;
        var ret = BuildButton(prt, "ReturnButton", new Vector2(0f, -350f), new Vector2(420f, 84f),
            out retLbl, () => Close());
        retLbl.text = "맵으로 돌아가기";
        var retImg = ret.GetComponent<Image>();
        if (retImg != null) retImg.color = new Color(0.22f, 0.22f, 0.28f, 1f);
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

    Offer BuildOffer(CardData card)
    {
        var o = new Offer { card = card, price = PriceFor(card) };

        // 카드 컨테이너 (가격까지 포함하는 세로 묶음)
        var item = new GameObject("Offer", typeof(RectTransform), typeof(LayoutElement));
        item.transform.SetParent(cardRow, false);
        var irt = item.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(210f, 330f);
        var le = item.GetComponent<LayoutElement>();
        le.preferredWidth = 210f; le.preferredHeight = 330f;

        // 카드 비주얼 (클릭 = 구매)
        var cardGO = BuildCardVisual(item.transform, card, new Vector2(0f, 22f), new Vector2(200f, 270f));
        o.root = cardGO;
        o.cg = cardGO.GetComponent<CanvasGroup>();
        o.buyBtn = cardGO.GetComponent<Button>();
        o.buyBtn.onClick.AddListener(() => OnBuy(o));

        // 가격 (카드 아래)
        var priceGO = new GameObject("Price", typeof(RectTransform));
        priceGO.transform.SetParent(item.transform, false);
        var prt = priceGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 4f);
        prt.sizeDelta = new Vector2(200f, 40f);
        o.priceText = priceGO.AddComponent<TextMeshProUGUI>();
        o.priceText.font = font; o.priceText.fontSize = 28f; o.priceText.fontStyle = FontStyles.Bold;
        o.priceText.alignment = TextAlignmentOptions.Center; o.priceText.color = colGold;
        o.priceText.text = $"{o.price} ⦿";
        o.priceText.raycastTarget = false;

        // SOLD 오버레이 (구매 후)
        var sold = NewImage(cardGO.transform, "Sold", new Color(0f, 0f, 0f, 0.62f));
        Stretch(sold.rectTransform);
        sold.raycastTarget = false;
        var soldTxt = new GameObject("Txt", typeof(RectTransform));
        soldTxt.transform.SetParent(sold.transform, false);
        var strt = soldTxt.GetComponent<RectTransform>();
        strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one; strt.offsetMin = strt.offsetMax = Vector2.zero;
        var stmp = soldTxt.AddComponent<TextMeshProUGUI>();
        stmp.font = font; stmp.text = "SOLD"; stmp.fontSize = 40f; stmp.fontStyle = FontStyles.Bold;
        stmp.color = new Color(0.95f, 0.25f, 0.25f, 1f); stmp.alignment = TextAlignmentOptions.Center;
        stmp.raycastTarget = false;
        sold.gameObject.SetActive(false);
        o.soldOverlay = sold.gameObject;

        return o;
    }

    // 절차생성 카드 비주얼 (등급 프레임 + 일러스트 + 코스트 + 이름 + 설명)
    GameObject BuildCardVisual(Transform parent, CardData card, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var frame = go.GetComponent<Image>();
        frame.color = card.GetRarityColor();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = frame;
        btn.transition = Selectable.Transition.None;

        // 일러스트 (프레임 4px 안쪽)
        var art = NewImage(go.transform, "Art", new Color(0.16f, 0.12f, 0.2f, 1f));
        var art_rt = art.rectTransform;
        art_rt.anchorMin = Vector2.zero; art_rt.anchorMax = Vector2.one;
        art_rt.offsetMin = new Vector2(4f, 4f); art_rt.offsetMax = new Vector2(-4f, -4f);
        if (card.cardImage != null) { art.sprite = card.cardImage; art.color = Color.white; }
        art.raycastTarget = false;

        // 코스트 (좌상단)
        AddText(go.transform, card.manaCost.ToString(), new Vector2(14f, -10f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            26, FontStyles.Bold, new Color(0.4f, 0.8f, 1f, 1f), new Vector2(44f, 44f)).raycastTarget = false;

        // 이름 (상단)
        AddText(go.transform, card.cardName, new Vector2(0f, -8f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            20, FontStyles.Bold, colText, new Vector2(size.x - 56f, 30f)).raycastTarget = false;

        // 설명 (하단)
        var desc = AddText(go.transform, card.description, new Vector2(0f, 8f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            16, FontStyles.Normal, colText, new Vector2(size.x - 20f, 80f));
        desc.alignment = TextAlignmentOptions.Top;
        desc.enableWordWrapping = true;
        desc.raycastTarget = false;

        return go;
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
        if (o == null || o.sold) return;
        if (Gold < o.price) return;
        SpendGold(o.price);
        if (GameManager.Instance != null) GameManager.Instance.AddCardToDeck(o.card);
        Save();
        o.sold = true;
        if (o.soldOverlay != null) o.soldOverlay.SetActive(true);
        if (o.buyBtn != null) o.buyBtn.interactable = false;
        RefreshAffordability();
    }

    void OnPotion()
    {
        if (potionUsed) return;
        if (Gold < potionPrice || Hp >= MaxHp) return;
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
            if (o.sold)
            {
                if (o.cg != null) o.cg.alpha = 0.6f;
                if (o.priceText != null) { o.priceText.text = "구매함"; o.priceText.color = new Color(0.6f, 0.6f, 0.64f, 1f); }
                continue;
            }
            bool afford = Gold >= o.price;
            if (o.cg != null) o.cg.alpha = afford ? 1f : 0.45f;           // 못 사면 카드 회색
            if (o.priceText != null) o.priceText.color = afford ? colGold : colCantAfford; // 가격 빨강 강조
            if (o.buyBtn != null) o.buyBtn.interactable = afford;
        }

        // 회복
        bool canPotion = !potionUsed && Gold >= potionPrice && Hp < MaxHp;
        if (potionBtn != null)
        {
            potionBtn.interactable = canPotion;
            var img = potionBtn.GetComponent<Image>();
            if (img != null) img.color = canPotion ? colBtn : colBtnDisabled;
        }
        if (potionLabel != null)
        {
            if (potionUsed) { potionLabel.text = "회복 완료"; potionLabel.color = new Color(0.6f, 0.6f, 0.64f, 1f); }
            else if (Hp >= MaxHp) { potionLabel.text = "체력 가득"; potionLabel.color = new Color(0.6f, 0.6f, 0.64f, 1f); }
            else
            {
                potionLabel.text = $"❤ 회복  +{potionHeal}   {potionPrice} ⦿";
                potionLabel.color = (Gold >= potionPrice) ? colText : colCantAfford;
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
}
