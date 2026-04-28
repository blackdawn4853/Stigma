using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// HP 바 위에 가로로 깔리는 버프/디버프 아이콘 줄.
// MonsterRuntimeUI / BattleUI 가 매 프레임 RefreshFromMonster / RefreshFromPlayer 호출.
//
// diff 기반 동기화: 매번 전부 destroy/create 하지 않고, 빠진 것만 추가하고 사라진 것만 제거.
// 새로 추가될 때만 슬라이드 인 애니메이션이 1회 재생되도록 한다.
public class StatusIconBar : MonoBehaviour
{
    public Vector2 iconSize = new Vector2(28f, 28f);
    public float spacing = 4f;

    private readonly List<StatusIconUI> icons = new List<StatusIconUI>();

    struct Entry { public StatusType type; public int value; public int turns; }

    public static StatusIconBar Create(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPos, Vector2 size, Vector2 iconSize)
    {
        GameObject go = new GameObject("StatusIconBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var bar = go.AddComponent<StatusIconBar>();
        bar.iconSize = iconSize;
        bar.spacing = 4f;
        return bar;
    }

    public void RefreshFromMonster(Monster m)
    {
        var desired = new List<Entry>();
        if (m != null)
        {
            if (m.strength != 0 && m.strengthTurns > 0)
                desired.Add(new Entry { type = StatusType.Strength, value = m.strength, turns = m.strengthTurns });
            if (m.debuffTurns > 0)
                desired.Add(new Entry { type = StatusType.Weak, value = 0, turns = m.debuffTurns });
        }
        Sync(desired);
    }

    public void RefreshFromPlayer(BattleManager bm)
    {
        var desired = new List<Entry>();
        if (bm != null)
        {
            if (bm.playerStrength != 0 && bm.playerStrengthTurns > 0)
                desired.Add(new Entry { type = StatusType.Strength, value = bm.playerStrength, turns = bm.playerStrengthTurns });
            if (bm.playerDebuffTurns > 0)
                desired.Add(new Entry { type = StatusType.Weak, value = 0, turns = bm.playerDebuffTurns });
            if (bm.RegenTurnsRemaining > 0)
                desired.Add(new Entry { type = StatusType.Regen, value = 0, turns = bm.RegenTurnsRemaining });
        }
        Sync(desired);
    }

    void Sync(List<Entry> desired)
    {
        // 1) 더 이상 필요없는 아이콘 제거
        for (int i = icons.Count - 1; i >= 0; i--)
        {
            var icon = icons[i];
            if (icon == null) { icons.RemoveAt(i); continue; }
            bool stillWanted = false;
            for (int j = 0; j < desired.Count; j++)
                if (desired[j].type == icon.type) { stillWanted = true; break; }
            if (!stillWanted)
            {
                Destroy(icon.gameObject);
                icons.RemoveAt(i);
            }
        }

        // 2) 기존 갱신 또는 신규 생성 (신규만 슬라이드 인)
        foreach (var d in desired)
        {
            StatusIconUI existing = null;
            foreach (var icon in icons)
                if (icon != null && icon.type == d.type) { existing = icon; break; }

            if (existing != null)
            {
                existing.Setup(d.type, d.value, d.turns);
            }
            else
            {
                var newIcon = StatusIconUI.Create(transform, iconSize);
                newIcon.Setup(d.type, d.value, d.turns);
                newIcon.PlaySlideIn();
                icons.Add(newIcon);
            }
        }
    }
}
