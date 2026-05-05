using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Practice 씬 좌측 상단의 "홈으로" 버튼과 확인 팝업을 관리.
// 홈 버튼 → 팝업 표시 → "돌아가기"면 Main_Home 씬 로드, "취소"면 팝업만 닫기.
public class HomeReturnConfirm : MonoBehaviour
{
    [SerializeField] private Button homeButton;        // 좌측 상단 홈 버튼
    [SerializeField] private GameObject confirmPopup;  // "홈으로 돌아가시겠습니까?" 팝업
    [SerializeField] private Button returnButton;      // 팝업의 "돌아가기" 버튼
    [SerializeField] private Button cancelButton;      // 팝업의 "취소" 버튼
    [SerializeField] private string homeSceneName = "Main_Home";

    private void Start()
    {
        if (confirmPopup != null)
            confirmPopup.SetActive(false);

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomeButtonClicked);
        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    public void OnHomeButtonClicked()
    {
        if (confirmPopup != null)
            confirmPopup.SetActive(true);
    }

    public void OnReturnClicked()
    {
        SceneManager.LoadScene(homeSceneName);
    }

    public void OnCancelClicked()
    {
        if (confirmPopup != null)
            confirmPopup.SetActive(false);
    }
}
