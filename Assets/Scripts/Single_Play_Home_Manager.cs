using UnityEngine;
using UnityEngine.SceneManagement;

public class Single_Play_Home_Manager : MonoBehaviour
{
    public void OnPracticeModeClicked()
    {
        SceneManager.LoadScene("PracticeMode");
    }

    public void OnTournamentModeClicked()
    {
        SceneManager.LoadScene("TournamentMode");
    }

    public void OnLeagueModeClicked()
    {
        SceneManager.LoadScene("LeagueMode");
    }

    public void OnChallengeModeClicked()
    {
        SceneManager.LoadScene("ChallengeMode");
    }
}
