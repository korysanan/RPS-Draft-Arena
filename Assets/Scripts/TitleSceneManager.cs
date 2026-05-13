using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Main_Home";

    private bool transitioning;

    public void GoToHome()
    {
        if (transitioning) return;
        transitioning = true;
        SceneManager.LoadScene(nextSceneName);
    }
}
