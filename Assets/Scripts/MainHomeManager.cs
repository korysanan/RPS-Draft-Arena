using UnityEngine;
using UnityEngine.SceneManagement;

public class MainHomeManager : MonoBehaviour
{
    [SerializeField] private GameObject quitConfirmPanel;

    public void OnSinglePlayClicked()
    {
        SceneManager.LoadScene("Single_Play_Home");
    }

    public void OnPvpPlayClicked()
    {
        SceneManager.LoadScene("PVP_Play_Home");
    }

    public void OnTutorialClicked()
    {
        SceneManager.LoadScene("Tutorial");
    }

    public void OnSettingClicked()
    {
        SceneManager.LoadScene("Setting");
    }

    public void OnMyInfoClicked()
    {
        SceneManager.LoadScene("MyInfo");
    }

    public void OnQuitClicked()
    {
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(true);
    }

    public void CancelQuit()
    {
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(false);
    }

    public void ConfirmQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
