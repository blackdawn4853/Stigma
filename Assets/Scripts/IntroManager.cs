using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroManager : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        if (FadeManager.Instance != null)
            FadeManager.Instance.FadeToScene("MainMenu");
        else
            SceneManager.LoadScene("MainMenu");
    }

    void Update()
    {
        // 스킵 기능 (아무 키나 누르면 스킵)
        if (Input.anyKeyDown)
        {
            videoPlayer.Stop();
            if (FadeManager.Instance != null)
                FadeManager.Instance.FadeToScene("MainMenu");
            else
                SceneManager.LoadScene("MainMenu");
        }
    }
}