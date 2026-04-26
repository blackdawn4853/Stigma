using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Slay the Spire 스타일 상태 효과 아이콘 행. HP바 아래에 동적으로 생성.
// 아트 미배정 상태이므로 색상 사각형 + 한글 약어 + 수치로 임시 표시.
public class StatusEffectUI : MonoBehaviour
{
    [Header("플레이어 상태 행 (HP바 아래)")]
    public Vector2 playerRowAnchoredPos = new Vector2(-550f, -175f);

    [Header("몬스터 상태 행 (HP바 아래)")]
    public Vector2 monsterRowAnchoredPos = new Vector2(550f, -185f);

    [Header("아이콘")]
    public Vector2 iconSize = new Vector2(48f, 48f);
    public float iconSpacing = 4f;

    [Header("임시 색상 (아트 미배정)")]
    public Color strengthColor = new Color(0.85f, 0.25f, 0.2f, 0.95f);
    public Color weakColor = new Color(0.5f, 0.3f, 0.65f, 0.95f);
    public Color regenColor = new Color(0.25f, 0.75f, 0.35f, 0.95f);
    public Color textColor = Color.white;

    // 아이콘 인스턴스
    StatusIcon playerStrength;
    StatusIcon playerWeak;
    StatusIcon playerRegen;
    StatusIcon monsterStrength;
    StatusIcon monsterWeak;

    void Start()
    {
        BuildPlayerRow();
        BuildMonsterRow();
    }

    void BuildPlayerRow()
    {
        Transform row = CreateRow("PlayerStatusRow", playerRowAnchoredPos);
        playerStrength = CreateIcon(row, "Strength", "근력", strengthColor);
        playerWeak     = CreateIcon(row, "Weak",     "약화", weakColor);
        playerRegen    = CreateIcon(row, "Regen",    "재생", regenColor);
    }

    void BuildMonsterRow()
    {
        Transform row = CreateRow("MonsterStatusRow", monsterRowAnchoredPos);
        monsterStrength = CreateIcon(row, "Strength", "근력", strengthColor);
        monsterWeak     = CreateIcon(row, "Weak",     "약화", weakColor);
    }

    Transform CreateRow(string name, Vector2 anchoredPos)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(transform, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(0f, iconSize.y);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = iconSpacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        return row.transform;
    }

    StatusIcon CreateIcon(Transform parent, string name, string shortLabel, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = iconSize;

        var img = root.GetComponent<Image>();
        img.color = color;

        // 중앙 약어
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(root.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.text = shortLabel;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.fontSize = 18;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = textColor;

        // 우하단 수치
        var valueGO = new GameObject("Value", typeof(RectTransform));
        valueGO.transform.SetParent(root.transform, false);
        var vrt = valueGO.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.5f, 0f);
        vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        var valueTmp = valueGO.AddComponent<TextMeshProUGUI>();
        valueTmp.text = "";
        valueTmp.alignment = TextAlignmentOptions.BottomRight;
        valueTmp.fontSize = 16;
        valueTmp.fontStyle = FontStyles.Bold;
        valueTmp.color = textColor;

        return new StatusIcon { root = root, label = labelTmp, value = valueTmp };
    }

    void Update()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        // ─── 플레이어 ─────────────────────────────────────────────
        bool pStrAct = bm.playerStrengthTurns > 0 && bm.playerStrength != 0;
        playerStrength.SetActive(pStrAct);
        if (pStrAct)
            playerStrength.value.text = (bm.playerStrength > 0 ? "+" : "") + bm.playerStrength + "/" + bm.playerStrengthTurns + "T";

        bool pWeakAct = bm.playerDebuffTurns > 0;
        playerWeak.SetActive(pWeakAct);
        if (pWeakAct) playerWeak.value.text = bm.playerDebuffTurns + "T";

        bool pRegenAct = GetRegenTurns(bm) > 0;
        playerRegen.SetActive(pRegenAct);
        if (pRegenAct) playerRegen.value.text = GetRegenTurns(bm) + "T";

        // ─── 몬스터 ───────────────────────────────────────────────
        bool mStrAct = bm.monsterStrengthTurns > 0 && bm.monsterStrength != 0;
        monsterStrength.SetActive(mStrAct);
        if (mStrAct)
            monsterStrength.value.text = (bm.monsterStrength > 0 ? "+" : "") + bm.monsterStrength + "/" + bm.monsterStrengthTurns + "T";

        bool mWeakAct = bm.monsterDebuffTurns > 0;
        monsterWeak.SetActive(mWeakAct);
        if (mWeakAct) monsterWeak.value.text = bm.monsterDebuffTurns + "T";
    }

    int GetRegenTurns(BattleManager bm)
    {
        var f = typeof(BattleManager).GetField("regenTurnsRemaining",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return f != null ? (int)f.GetValue(bm) : 0;
    }

    class StatusIcon
    {
        public GameObject root;
        public TextMeshProUGUI label;
        public TextMeshProUGUI value;
        public void SetActive(bool active) { if (root != null) root.SetActive(active); }
    }
}
