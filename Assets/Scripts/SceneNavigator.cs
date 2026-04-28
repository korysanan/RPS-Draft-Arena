using UnityEngine;
using UnityEngine.SceneManagement;

// 단순한 "홈으로 돌아가기" 헬퍼.
// 각 씬의 Previous Button(혹은 Back/Home 버튼)에 연결되어 있고,
// 이동할 씬 이름은 인스펙터에서 씬마다 다르게 설정한다.
//   예) Tutorial → "Main_Home", PracticeMode → "Single_Play_Home"
public class SceneNavigator : MonoBehaviour
{
    // 인스펙터에서 씬마다 지정 (기본값은 Main_Home)
    [SerializeField] private string homeSceneName = "Main_Home";

    // 버튼 OnClick에 연결되는 메서드: 지정된 씬으로 이동
    public void GoToHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }
}
