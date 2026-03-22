using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("이펙트 대상 위치")]
    public Transform monsterTransform;
    public Transform playerTransform;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlayCardEffect(CardData card, bool targetIsMonster = true)
    {
        Transform target = targetIsMonster ? monsterTransform : playerTransform;

        switch (card.effectType)
        {
            case CardData.CardEffectType.Damage:
                StartCoroutine(SlashEffect(target));
                break;

            case CardData.CardEffectType.Shield:
                StartCoroutine(ShieldEffect(playerTransform));
                break;
        }
    }

    IEnumerator SlashEffect(Transform target)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject slash = CreateLine(target.position, Color.red);
            Destroy(slash, 0.3f);
            yield return new WaitForSeconds(0.05f);
        }
        StartCoroutine(FlashColor(target, Color.red));
    }

    IEnumerator ShieldEffect(Transform target)
    {
        for (int i = 0; i < 6; i++)
        {
            GameObject dot = CreateDot(
                target.position + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f), 0),
                new Color(0.2f, 0.5f, 1f)
            );
            Destroy(dot, 0.4f);
            yield return new WaitForSeconds(0.05f);
        }
        StartCoroutine(FlashColor(target, new Color(0.2f, 0.5f, 1f)));
    }

    GameObject CreateLine(Vector3 position, Color color)
    {
        GameObject obj = new GameObject("SlashEffect");
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, 0f);
        lr.startWidth = 0.15f;
        lr.endWidth = 0f;
        lr.positionCount = 2;

        float angle = Random.Range(-45f, 45f) * Mathf.Deg2Rad;
        float length = Random.Range(0.8f, 1.5f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * length;
        lr.SetPosition(0, position + offset * 0.5f - new Vector3(0, 0, 1f));
        lr.SetPosition(1, position - offset * 0.5f - new Vector3(0, 0, 1f));

        return obj;
    }

    GameObject CreateDot(Vector3 position, Color color)
    {
        GameObject obj = new GameObject("DotEffect");
        obj.transform.position = position;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        sr.color = color;
        obj.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);
        return obj;
    }

    IEnumerator FlashColor(Transform target, Color flashColor)
    {
        SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color original = sr.color;
        sr.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        sr.color = original;
    }
}