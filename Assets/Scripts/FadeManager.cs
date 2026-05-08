using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [Header("페이드 UI")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadCutscene()
    {
        FadeToScene("Cutscene");
    }

    // duration <= 0 이면 인스펙터 fadeDuration 사용. 컷씬 스킵 등에서 빠르게 넘기고 싶을 때 명시.
    public void FadeToScene(string sceneName, float duration = -1f)
    {
        float dur = duration > 0f ? duration : fadeDuration;
        StartCoroutine(FadeRoutine(sceneName, dur));
    }

    IEnumerator FadeRoutine(string sceneName, float dur)
    {
        yield return StartCoroutine(Fade(0f, 1f, dur));
        SceneManager.LoadScene(sceneName);
        yield return null;
        yield return StartCoroutine(Fade(1f, 0f, dur));
    }

    IEnumerator Fade(float from, float to, float dur)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color color = fadeImage.color;
        color.a = from;
        fadeImage.color = color;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, to, elapsed / dur);
            fadeImage.color = color;
            yield return null;
        }

        color.a = to;
        fadeImage.color = color;
    }
}