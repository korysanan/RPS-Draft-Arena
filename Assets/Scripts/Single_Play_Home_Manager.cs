using UnityEngine;
using UnityEngine.SceneManagement;

// Single_Play_Home 씬에 붙는 매니저.
// 4개 모드 버튼(OnClick)에 연결되어 클릭 시 해당 모드 씬으로 이동시킨다.
public class Single_Play_Home_Manager : MonoBehaviour
{
    // Practice 모드 버튼 클릭 → PracticeMode 씬 로드 (설정 화면)
    public void OnPracticeModeClicked()
    {
        SceneManager.LoadScene("PracticeMode");
    }

    // Tournament 모드 버튼 클릭 → TournamentMode 씬 로드
    public void OnTournamentModeClicked()
    {
        SceneManager.LoadScene("TournamentMode");
    }

    // League 모드 버튼 클릭 → LeagueMode 씬 로드
    public void OnLeagueModeClicked()
    {
        SceneManager.LoadScene("LeagueMode");
    }

    // Challenge 모드 버튼 클릭 → ChallengeMode 씬 로드
    public void OnChallengeModeClicked()
    {
        SceneManager.LoadScene("ChallengeMode");
    }
}
