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

    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeRoutine(sceneName));
    }

    IEnumerator FadeRoutine(string sceneName)
    {
        yield return StartCoroutine(Fade(0f, 1f));
        SceneManager.LoadScene(sceneName);
        yield return null;
        yield return StartCoroutine(Fade(1f, 0f));
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color color = fadeImage.color;
        color.a = from;
        fadeImage.color = color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = to;
        fadeImage.color = color;
    }
}