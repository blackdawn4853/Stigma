using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// MainMenu 의 Load_Button 을 런타임에 GameManager.LoadGameAndStart() 로 와이어링.
// 세이브 파일 없으면 버튼 비활성화 (회색 처리).
// 별도 GameObject 배치 없이 [RuntimeInitializeOnLoadMethod] 로 자동 동작.
public static class MainMenuWiring
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 이미 MainMenu 에 진입한 상태라면 즉시 실행
        if (SceneManager.GetActiveScene().name == "MainMenu")
            WireLoadButton();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu") return;
        WireLoadButton();
    }

    static void WireLoadButton()
    {
#if UNITY_2023_1_OR_NEWER
        var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
#else
        var buttons = Object.FindObjectsOfType<Button>();
#endif
        Button loadBtn = null;
        foreach (var b in buttons)
        {
            if (b.name == "Load_Button") { loadBtn = b; break; }
        }
        if (loadBtn == null)
        {
            Debug.LogWarning("[MainMenuWiring] Load_Button 못찾음");
            return;
        }

        loadBtn.onClick.RemoveAllListeners();
        loadBtn.onClick.AddListener(() =>
        {
            if (GameManager.Instance != null)
                GameManager.Instance.LoadGameAndStart();
        });

        // 세이브 파일 없으면 비활성화
        bool hasSave = GameManager.Instance != null && GameManager.Instance.HasSaveFile();
        loadBtn.interactable = hasSave;

        Debug.Log($"[MainMenuWiring] Load_Button 연결됨 (세이브: {(hasSave ? "있음" : "없음")})");
    }
}
