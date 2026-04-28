using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 카드 뒤집기 컴포넌트.
// 카드의 Button을 클릭하면 가로 스케일을 1 → 0 → 1로 줄였다 늘리며 뒤집는 효과를 주고,
// 중간 시점에 앞면(face-down)을 숨기고 뒷면(face-up)을 켜서 카드 내용을 드러낸다.
// 한 번 뒤집힌 카드는 다시 뒤집히지 않음(다른 카드는 영향 없음).
[RequireComponent(typeof(Button))]
public class CardFlip : MonoBehaviour
{
    // 뒤집기 전 보이는 그래픽 (현재 카드의 RawImage = Card_Back).
    // RawImage / Image 둘 다 Graphic을 상속하므로 어느 쪽이든 OK.
    [SerializeField] private Graphic frontGraphic;
    // 뒤집은 후 보일 GameObject (속성 카드 이미지가 들어 있는 Back GO).
    [SerializeField] private GameObject backFace;
    // 뒤집기 애니메이션 총 길이(초). 절반(=duration*0.5)에서 그래픽 스왑이 일어남.
    [SerializeField] private float duration = 0.3f;

    private bool flipped;        // 한 번만 뒤집히도록 막는 플래그
    private RectTransform rect;  // 스케일 애니메이션 대상

    private void Awake()
    {
        rect = (RectTransform)transform;
        // 같은 GameObject의 Button 클릭 → Flip() 자동 연결
        GetComponent<Button>().onClick.AddListener(Flip);
        // 시작 시점엔 뒷면 숨김
        if (backFace != null) backFace.SetActive(false);
    }

    // 외부에서도 호출 가능한 뒤집기 트리거 (이미 뒤집혔으면 무시)
    public void Flip()
    {
        if (flipped) return;
        flipped = true;
        StartCoroutine(FlipRoutine());
    }

    // 뒷면 RawImage에 속성 카드 텍스처를 주입하고, 남아 있던 글자 라벨은 숨긴다.
    // (PracticeCardController.Start에서 카드별로 1회 호출)
    public void SetBackTexture(Texture texture)
    {
        if (backFace == null) return;
        var raw = backFace.GetComponent<RawImage>();
        if (raw != null) raw.texture = texture;
        // 예전에 글자(A,B,C...)를 표시하던 TMP 라벨은 더 이상 쓰지 않으므로 비활성화
        var tmp = backFace.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.gameObject.SetActive(false);
    }

    // 카드 뒤집기 애니메이션 코루틴.
    // 1단계: scale.x 1 → 0 (앞면이 가로로 줄어듦)
    // 중간:    앞면 비활성, 뒷면 활성
    // 2단계: scale.x 0 → 1 (뒷면이 가로로 펼쳐짐)
    private IEnumerator FlipRoutine()
    {
        float half = duration * 0.5f;
        float t = 0f;
        // 1단계: 가로로 압축 (앞면이 사라지는 듯한 효과)
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0f, t / half);
            rect.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        rect.localScale = new Vector3(0f, 1f, 1f);

        // 가로 폭이 0인 순간에 앞/뒷면 스왑 → 시각적으로 자연스러운 전환
        if (frontGraphic != null) frontGraphic.enabled = false;
        if (backFace != null) backFace.SetActive(true);

        // 2단계: 가로로 다시 펼치기 (뒷면이 나타나는 듯한 효과)
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
