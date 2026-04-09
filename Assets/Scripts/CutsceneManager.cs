using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneManager : MonoBehaviour
{
    [Header("UI 연결")]
    public RectTransform cutsceneImage;
    public TextMeshProUGUI narrationText;

    [Header("설정")]
    public float typingSpeed = 0.05f;
    public string nextScene = "NodeMap";

    private bool isSkipping = false;

    private struct CutsceneShot
    {
        public string narration;
        public Vector2 targetPos;
        public float targetScale;
        public float moveDuration;
        public float waitDuration;

        public CutsceneShot(string n, Vector2 p, float s, float m, float w)
        {
            narration = n; targetPos = p; targetScale = s;
            moveDuration = m; waitDuration = w;
        }
    }

    private CutsceneShot[] shots;

    void Start()
    {
        shots = new CutsceneShot[]
        {
            // 4번째 집 지붕/하늘 클로즈업 (사람 안 보임)
            new CutsceneShot("인간들은 신에게 간청했다.",
                new Vector2(300f, 120f), 3.5f, 0f, 3f),

            // 왼쪽 집들로 스크롤 (사람 안 보임)
            new CutsceneShot("번영을, 힘을, 그리고 구원을.",
                new Vector2(2310f, -1015f), 3.5f, 2f, 2f),

            // 멈춤
            new CutsceneShot("침묵 속에서 마침내, 응답이 내려왔으니.",
                new Vector2(2310f, -1015f), 3.5f, 0f, 3f),

            // ✅ 최하단 우측 얼굴 남자 (더 오른쪽 아래로)
            new CutsceneShot("환희.",
                new Vector2(-1715f, 1330f), 3.5f, 0.6f, 1.5f),

            // ✅ 유일한 여자 클로즈업 (우측 중상단)
            new CutsceneShot("믿음.",
                new Vector2(-1365f, -560f), 3.5f, 0.6f, 1.5f),

            // ✅ 사제 오른쪽으로 살짝 이동
            new CutsceneShot("구원.",
                new Vector2(1500f, -540f), 3f, 0.6f, 1.5f),

            // 전체 줌아웃 + 번쩍
            new CutsceneShot("그 누구도 거부 할 수 없으리.",
                new Vector2(0f, 0f), 1f, 2f, 3f),
        };

        StartCoroutine(PlayCutscene());
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
            isSkipping = true;
    }

    IEnumerator PlayCutscene()
    {
        cutsceneImage.anchoredPosition = shots[0].targetPos;
        cutsceneImage.localScale = Vector3.one * shots[0].targetScale;

        for (int i = 0; i < shots.Length; i++)
        {
            if (isSkipping) break;

            CutsceneShot shot = shots[i];

            if (shot.moveDuration > 0f)
                StartCoroutine(MoveImage(shot.targetPos, shot.targetScale, shot.moveDuration));

            yield return StartCoroutine(TypeText(shot.narration));
            yield return new WaitForSeconds(shot.waitDuration);

            if (i == shots.Length - 1)
                yield return StartCoroutine(FlashEffect());

            yield return StartCoroutine(FadeText(1f, 0f));
        }

        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene(nextScene);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    IEnumerator MoveImage(Vector2 targetPos, float targetScale, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = cutsceneImage.anchoredPosition;
        float startScale = cutsceneImage.localScale.x;

        while (elapsed < duration && !isSkipping)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cutsceneImage.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            cutsceneImage.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);
            yield return null;
        }

        cutsceneImage.anchoredPosition = targetPos;
        cutsceneImage.localScale = Vector3.one * targetScale;
    }

    IEnumerator FlashEffect()
    {
        GameObject flashObj = new GameObject("Flash");
        flashObj.transform.SetParent(cutsceneImage.transform.parent);
        Image flashImage = flashObj.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        RectTransform rt = flashObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.8f, elapsed / 0.1f));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, elapsed / 0.3f));
            yield return null;
        }

        Destroy(flashObj);
    }

    IEnumerator TypeText(string text)
    {
        narrationText.text = "";
        narrationText.color = new Color(
            narrationText.color.r,
            narrationText.color.g,
            narrationText.color.b, 1f);

        foreach (char c in text)
        {
            if (isSkipping) { narrationText.text = text; yield break; }
            narrationText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    IEnumerator FadeText(float from, float to)
    {
        float elapsed = 0f;
        Color color = narrationText.color;

        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, to, elapsed / 0.5f);
            narrationText.color = color;
            yield return null;
        }
    }
}