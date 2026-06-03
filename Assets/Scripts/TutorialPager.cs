using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 튜토리얼 페이지 넘기기 컨트롤러.
// 가운데 이미지(displayImage)와 그 아래 설명 텍스트(descriptionText)를
// 페이지 단위로 교체한다. 다음/이전 버튼으로 페이지를 이동한다.
//
// pages 와 descriptions 는 인스펙터에서 페이지 순서대로 채운다.
// (지금은 비어 있어도 동작하며, 표시 영역과 버튼만 보인다.)
public class TutorialPager : MonoBehaviour
{
    [Header("표시 영역")]
    [SerializeField] private Image displayImage;        // 가운데 이미지
    [SerializeField] private TMP_Text descriptionText;  // 이미지 아래 설명 텍스트

    [Header("페이지 이동 버튼")]
    [SerializeField] private Button prevButton;          // 이전 페이지
    [SerializeField] private Button nextButton;          // 다음 페이지

    [Header("페이지 내용 (인스펙터에서 채우기)")]
    [SerializeField] private Sprite[] pages;             // 페이지별 이미지
    [TextArea(2, 5)]
    [SerializeField] private string[] descriptions;      // 페이지별 설명 (pages 와 같은 순서)

    private int index;

    private void Start()
    {
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        ShowPage(0);
    }

    // 다음 페이지 버튼
    public void NextPage()
    {
        ShowPage(index + 1);
    }

    // 이전 페이지 버튼
    public void PrevPage()
    {
        ShowPage(index - 1);
    }

    private void ShowPage(int newIndex)
    {
        int count = pages != null ? pages.Length : 0;
        if (count == 0)
        {
            // 아직 페이지가 등록되지 않은 경우: 버튼만 비활성 처리
            UpdateButtons(0);
            return;
        }

        index = Mathf.Clamp(newIndex, 0, count - 1);

        if (displayImage != null)
            displayImage.sprite = pages[index];

        if (descriptionText != null)
            descriptionText.text =
                (descriptions != null && index < descriptions.Length) ? descriptions[index] : string.Empty;

        UpdateButtons(count);
    }

    // 첫/마지막 페이지에서는 해당 방향 버튼을 비활성화
    private void UpdateButtons(int count)
    {
        if (prevButton != null) prevButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < count - 1;
    }
}
