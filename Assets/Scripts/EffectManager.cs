using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

// 카드 발동/타격 임팩트 — 전부 절차 생성 (아트 0).
// 몬스터가 피해를 받는 순간(Monster.TakeDamage/DirectDamage)에 호출되어
// "베기 호(arc) + 퍼지는 충격 링 + 검붉은 스파크 파편" 을 타격 지점에 그린다.
// 절충 톤: 베기는 창백한 흰빛, 충격/파편은 핏빛.
// BattleScene 진입 시 자동 부트스트랩.
public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("강도 기준")]
    [Tooltip("이 데미지 이상이면 임팩트가 최대 크기.")]
    public float maxDamageRef = 25f;
    [Tooltip("임팩트 전체 크기 배수 (작은 데미지~큰 데미지 보간 위에 곱).")]
    [Range(0.3f, 2.5f)] public float globalScale = 1f;

    [Header("베기 호 (slash arc)")]
    public Color slashColor = new Color(1f, 0.97f, 0.92f, 1f); // 창백한 흰빛
    [Tooltip("베기 표시 시간 (초).")]
    public float slashDuration = 0.16f;

    [Header("충격 링 (impact ring)")]
    public Color ringColor = new Color(0.95f, 0.25f, 0.2f, 1f); // 핏빛
    public float ringDuration = 0.22f;

    [Header("스파크 파편")]
    public Color sparkColor = new Color(0.7f, 0.12f, 0.1f, 1f); // 검붉은
    public int sparkBaseCount = 5;

    Material lineMat;
    static Sprite dotSprite;

    // ─── 부트스트랩 ──────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoad()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == "BattleScene" && Instance == null) Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleScene" && Instance == null) Spawn();
    }

    static void Spawn()
    {
        var go = new GameObject("EffectManager");
        go.AddComponent<EffectManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        lineMat = new Material(Shader.Find("Sprites/Default"));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── 타격 임팩트 (공개 API) ──────────────────────────────────
    public void PlayHitImpact(Vector3 worldPos, int damage)
    {
        float t = Mathf.Clamp01(damage / Mathf.Max(1f, maxDamageRef)); // 0~1
        float scale = Mathf.Lerp(0.7f, 1.5f, t) * globalScale;
        int slashes = damage >= maxDamageRef * 0.6f ? 2 : 1;

        for (int i = 0; i < slashes; i++)
            StartCoroutine(SlashRoutine(worldPos, scale, i * 0.04f));
        StartCoroutine(RingRoutine(worldPos, scale));
        SpawnSparks(worldPos, scale, t);
    }

    // ─── 베기 호 ─────────────────────────────────────────────────
    IEnumerator SlashRoutine(Vector3 pos, float scale, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var obj = new GameObject("SlashArc");
        var lr = obj.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.useWorldSpace = true;
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.sortingOrder = 250;

        const int n = 14;
        lr.positionCount = n;
        float angle = Random.Range(-55f, 55f) * Mathf.Deg2Rad;
        float len = scale * Random.Range(1.7f, 2.3f);
        float bowAmt = scale * Random.Range(0.35f, 0.6f) * (Random.value < 0.5f ? 1f : -1f);
        Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)(n - 1);
            float along = (u - 0.5f) * len;
            float bow = Mathf.Sin(u * Mathf.PI) * bowAmt;
            Vector3 p = pos + dir * along + perp * bow;
            p.z = -1f;
            lr.SetPosition(i, p);
        }

        // 가운데 굵고 양끝 가늘게
        var wc = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        lr.widthCurve = wc;
        lr.widthMultiplier = scale * 0.24f;

        float dur = slashDuration;
        float e = 0f;
        while (e < dur && obj != null)
        {
            e += Time.deltaTime;
            float k = e / dur;
            // 살짝 늘어나며 빠르게 사라짐
            lr.widthMultiplier = scale * 0.24f * (1f - k * 0.6f);
            Color c = slashColor; c.a = 1f - k;
            lr.startColor = lr.endColor = c;
            yield return null;
        }
        if (obj != null) Destroy(obj);
    }

    // ─── 충격 링 ─────────────────────────────────────────────────
    IEnumerator RingRoutine(Vector3 pos, float scale)
    {
        var obj = new GameObject("ImpactRing");
        obj.transform.position = pos + Vector3.back;
        var lr = obj.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sortingOrder = 248;

        const int seg = 28;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        float startR = scale * 0.25f;
        float endR = scale * 1.3f;
        float dur = ringDuration;
        float e = 0f;
        while (e < dur && obj != null)
        {
            e += Time.deltaTime;
            float k = e / dur;
            float r = Mathf.Lerp(startR, endR, Mathf.Sqrt(k)); // 처음 빠르게 퍼짐
            obj.transform.localScale = Vector3.one * r;
            lr.widthMultiplier = scale * 0.1f * (1f - k);
            Color c = ringColor; c.a = 1f - k;
            lr.startColor = lr.endColor = c;
            yield return null;
        }
        if (obj != null) Destroy(obj);
    }

    // ─── 스파크 파편 ─────────────────────────────────────────────
    void SpawnSparks(Vector3 pos, float scale, float t)
    {
        int count = sparkBaseCount + Mathf.RoundToInt(t * 6f);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = Random.insideUnitCircle.normalized * Random.Range(2.5f, 6f) * scale;
            StartCoroutine(SparkRoutine(pos, vel, scale));
        }
    }

    IEnumerator SparkRoutine(Vector3 pos, Vector2 vel, float scale)
    {
        var obj = new GameObject("Spark");
        obj.transform.position = pos + Vector3.back * 1.2f;
        obj.transform.localScale = Vector3.one * Random.Range(0.06f, 0.13f) * scale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetDot();
        sr.color = sparkColor;
        sr.sortingOrder = 252;

        float dur = Random.Range(0.18f, 0.34f);
        Vector3 p = obj.transform.position;
        float e = 0f;
        while (e < dur && obj != null)
        {
            float dt = Time.deltaTime;
            e += dt;
            vel *= 0.88f;                       // 감속
            vel.y -= 9f * dt * scale;           // 중력
            p += (Vector3)(vel * dt);
            obj.transform.position = p;
            Color c = sparkColor; c.a = 1f - (e / dur);
            sr.color = c;
            yield return null;
        }
        if (obj != null) Destroy(obj);
    }

    static Sprite GetDot()
    {
        if (dotSprite != null) return dotSprite;
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f)) / 3.5f;
                px[y * 8 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 64f);
        return dotSprite;
    }
}
