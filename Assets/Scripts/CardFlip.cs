using System;
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

    // 카드에 할당된 속성. PracticeCardController가 SetCard로 주입한다.
    public ElementType Element { get; private set; }
    // 뒤집기 애니메이션이 막 시작된 순간(=클릭/외부 트리거가 확정된 즉시) 동기적으로 호출.
    // "먼저 클릭한 사람이 임자" 판정처럼 픽을 즉시 확정해야 할 때 사용한다.
    public event Action<CardFlip> OnFlipStarted;
    // 뒤집기 애니메이션이 끝난 직후에 한 번 호출되는 이벤트 (자기 자신을 인자로 전달).
    public event Action<CardFlip> OnFlipped;
    public bool IsFlipped => flipped;

    private void Awake()
    {
        rect = (RectTransform)transform;
        // 같은 GameObject의 Button 클릭 → Flip() 자동 연결
        GetComponent<Button>().onClick.AddListener(Flip);
        // 시작 시점엔 뒷면 숨김
        if (backFace != null) backFace.SetActive(false);
    }

    // 외부에서도 호출 가능한 뒤집기 트리거 (이미 뒤집혔으면 무시).
    // flipped 플래그와 OnFlipStarted 통지가 코루틴 시작 전에 동기적으로 처리되므로,
    // 이 호출 직후엔 다른 카드 클릭을 즉시 잠궈도 race condition이 없다.
    public void Flip()
    {
        if (flipped) return;
        flipped = true;
        OnFlipStarted?.Invoke(this);
        StartCoroutine(FlipRoutine());
    }

    // 다음 라운드/결판전 진입을 위해 카드 상태를 뒤집기 전(앞면) 상태로 되돌린다.
    // Element는 외부에서 SetCard로 재배정해도 되고 그대로 둬도 무방.
    public void ResetFlip()
    {
        if (!flipped) return;
        StopAllCoroutines(); // 진행 중이던 FlipRoutine 중단
        flipped = false;
        if (frontGraphic != null) frontGraphic.enabled = true;
        if (backFace != null) backFace.SetActive(false);
        if (rect != null) rect.localScale = Vector3.one;
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

    // 속성 + 텍스처를 한 번에 주입하는 편의 메서드.
    // 이후 시합 판정에서 Element 프로퍼티로 카드 속성을 조회한다.
    public void SetCard(ElementType element, Texture texture)
    {
        Element = element;
        SetBackTexture(texture);
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

        // 뒤집기 완료 알림 (PracticeCardController가 1번째/2번째 픽 추적용으로 사용)
        OnFlipped?.Invoke(this);
    }
}
