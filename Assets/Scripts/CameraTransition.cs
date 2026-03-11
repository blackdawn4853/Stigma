using UnityEngine;
using System.Collections;

public class CameraTransition : MonoBehaviour
{
    public static CameraTransition Instance { get; private set; }

    public float transitionSpeed = 0.6f;
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    private bool isTransitioning = false;
    private Camera cam;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        cam = GetComponent<Camera>();
    }

    public bool IsTransitioning() => isTransitioning;

    public void StartTransition(string direction, PlayerController player, System.Action onComplete)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionCoroutine(direction, player, onComplete));
    }

    IEnumerator TransitionCoroutine(string direction, PlayerController player, System.Action onComplete)
    {
        isTransitioning = true;

        // 애니메이션 시작
        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isMoving", true);
            switch (direction)
            {
                case "Up":    anim.SetFloat("MoveX", 0);  anim.SetFloat("MoveY", 1);  break;
                case "Down":  anim.SetFloat("MoveX", 0);  anim.SetFloat("MoveY", -1); break;
                case "Left":  anim.SetFloat("MoveX", -1); anim.SetFloat("MoveY", 0);  break;
                case "Right": anim.SetFloat("MoveX", 1);  anim.SetFloat("MoveY", 0);  break;
            }
        }

        Vector3 camStart = cam.transform.position;
        Vector3 camEnd = camStart;
        Vector3 playerStart = player.transform.position;
        Vector3 playerEnd = playerStart;

        switch (direction)
        {
            case "Up":
                camEnd = camStart + new Vector3(0, roomHeight, 0);
                playerEnd = playerStart + new Vector3(0, roomHeight, 0);
                break;
            case "Down":
                camEnd = camStart + new Vector3(0, -roomHeight, 0);
                playerEnd = playerStart + new Vector3(0, -roomHeight, 0);
                break;
            case "Left":
                camEnd = camStart + new Vector3(-roomWidth, 0, 0);
                playerEnd = playerStart + new Vector3(-roomWidth, 0, 0);
                break;
            case "Right":
                camEnd = camStart + new Vector3(roomWidth, 0, 0);
                playerEnd = playerStart + new Vector3(roomWidth, 0, 0);
                break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            float smooth = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            cam.transform.position = Vector3.Lerp(camStart, camEnd, smooth);
            player.transform.position = Vector3.Lerp(playerStart, playerEnd, smooth);
            yield return null;
        }

        cam.transform.position = camEnd;
        player.transform.position = playerEnd;

        // 애니메이션 종료
        if (anim != null)
        {
            anim.SetBool("isMoving", false);
            anim.SetFloat("MoveX", 0);
            anim.SetFloat("MoveY", 0);
        }

        isTransitioning = false;
        onComplete?.Invoke();
    }
}