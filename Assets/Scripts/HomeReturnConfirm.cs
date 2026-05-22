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
        if (confirmPopup == null) return;
        // 런타임에 같은 부모 안에서 다른 팝업이 뒤늦게 추가될 수 있어 z-order가 밀린다.
        // 표시 직전에 마지막 자식으로 올려 항상 다른 UI 위에 그려지도록 강제.
        confirmPopup.transform.SetAsLastSibling();
        confirmPopup.SetActive(true);
        // 팝업이 떠 있는 동안 게임을 일시정지 — 타이머/AI 진행 모두 정지.
        Time.timeScale = 0f;
    }

    public void OnReturnClicked()
    {
        // 다음 씬으로 넘어가기 전에 timeScale을 복구하지 않으면 새 씬도 멈춘 상태로 시작된다.
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    public void OnCancelClicked()
    {
        if (confirmPopup != null)
            confirmPopup.SetActive(false);
        Time.timeScale = 1f;
    }

    // 컴포넌트가 비활성/파괴될 때 timeScale이 0으로 묶여 있지 않도록 보호.
    private void OnDisable()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }
}
