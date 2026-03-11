using UnityEngine;
using System.Collections;

public class HitEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private Animator anim;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    public void PlayHit()
    {
        StartCoroutine(HitCoroutine());
    }

    IEnumerator HitCoroutine()
    {
        // 피격 애니메이션 트리거
        if (anim != null)
            anim.SetTrigger("Hit");

        // 흰색 반짝임
        sr.color = Color.white * 2f;
        yield return new WaitForSeconds(0.1f);
        sr.color = Color.white;
    }
}