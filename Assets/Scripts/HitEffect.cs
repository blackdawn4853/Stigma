using UnityEngine;
using System.Collections;

// 피격 플래시 — 흰 번쩍 후 핏빛 틴트로 짧게 물들었다가 원래 색으로 페이드.
// 데미지가 클수록 더 밝고 더 길게. (HP 0 표현 X — 순수 시각 피드백)
public class HitEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private Animator anim;
    private Color baseColor = Color.white;
    private Coroutine co;

    [Tooltip("핏빛 틴트 색.")]
    public Color bloodColor = new Color(1f, 0.18f, 0.13f, 1f);
    [Tooltip("이 데미지 이상이면 연출이 최대 강도/길이 (정규화 기준).")]
    public float maxDamageRef = 25f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        if (sr != null) baseColor = sr.color;
    }

    // 구버전 호환 — 데미지 정보 없이 호출되면 기본 강도.
    public void PlayHit() => PlayHit(0);

    public void PlayHit(int damage)
    {
        if (!gameObject.activeInHierarchy) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(HitCoroutine(damage));
    }

    IEnumerator HitCoroutine(int damage)
    {
        if (anim != null) anim.SetTrigger("Hit");
        if (sr == null) yield break;

        float t = Mathf.Clamp01(damage / Mathf.Max(1f, maxDamageRef)); // 0~1 강도
        float flashDur = Mathf.Lerp(0.05f, 0.09f, t);
        float bloodDur = Mathf.Lerp(0.12f, 0.30f, t);

        // 1) 흰 번쩍 — 데미지 클수록 더 밝게 오버브라이트
        sr.color = Color.white * (1.6f + t * 0.8f);
        yield return new WaitForSeconds(flashDur);

        // 2) 핏빛으로 떨어졌다가 원래 색으로 페이드
        float e = 0f;
        while (e < bloodDur)
        {
            e += Time.deltaTime;
            float k = e / bloodDur;
            sr.color = Color.Lerp(bloodColor, baseColor, k);
            yield return null;
        }
        sr.color = baseColor;
        co = null;
    }
}
