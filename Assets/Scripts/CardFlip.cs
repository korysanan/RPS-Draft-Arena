using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CardFlip : MonoBehaviour
{
    [SerializeField] private Graphic frontGraphic;
    [SerializeField] private GameObject backFace;
    [SerializeField] private float duration = 0.3f;

    private bool flipped;
    private RectTransform rect;

    private void Awake()
    {
        rect = (RectTransform)transform;
        GetComponent<Button>().onClick.AddListener(Flip);
        if (backFace != null) backFace.SetActive(false);
    }

    public void Flip()
    {
        if (flipped) return;
        flipped = true;
        StartCoroutine(FlipRoutine());
    }

    public void SetBackTexture(Texture texture)
    {
        if (backFace == null) return;
        var raw = backFace.GetComponent<RawImage>();
        if (raw != null) raw.texture = texture;
        var tmp = backFace.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.gameObject.SetActive(false);
    }

    private IEnumerator FlipRoutine()
    {
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0f, t / half);
            rect.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        rect.localScale = new Vector3(0f, 1f, 1f);
        if (frontGraphic != null) frontGraphic.enabled = false;
        if (backFace != null) backFace.SetActive(true);
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1f, t / half);
            rect.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }
}
