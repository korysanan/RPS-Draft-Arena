using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "Main_Home";

    public void GoToHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }
}
