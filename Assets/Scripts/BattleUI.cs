using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    [Header("플레이어 UI")]
    public Slider playerHPBar;
    public TextMeshProUGUI playerHPText;

    [Header("마나 UI")]
    public TextMeshProUGUI manaText;

    [Header("방어도 UI")]
    public TextMeshProUGUI playerDefenseText;

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
        else Destroy(gameObject);
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
    }

    public void UpdateUI()
    {
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        if (playerHPBar != null)
            playerHPBar.value = (float)bm.playerCurrentHp / bm.playerMaxHp;
        if (playerHPText != null)
            playerHPText.text = $"{bm.playerCurrentHp}/{bm.playerMaxHp}";

        if (manaText != null)
            manaText.text = $"Mana: {bm.currentMana}/{bm.maxMana}";

        if (playerDefenseText != null)
            playerDefenseText.text = bm.playerDefense > 0 ? $"방어 {bm.playerDefense}" : "";

        if (gazeBar != null)
            gazeBar.value = (float)bm.gazeLevel / 100f;
        if (gazeText != null)
            gazeText.text = $"시선: {bm.gazeLevel}";

        if (deckCountText != null)
            deckCountText.text = $"덱: {bm.deck.Count}";
        if (discardCountText != null)
            discardCountText.text = $"버림: {bm.discardPile.Count}";
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