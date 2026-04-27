using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Main_Home";

    private bool transitioning;

    void Update()
    {
        if (transitioning) return;

        if (IsAnyClickOrTouchThisFrame())
        {
            GoToHome();
        }
    }

    public void GoToHome()
    {
        if (transitioning) return;
        transitioning = true;
        SceneManager.LoadScene(nextSceneName);
    }

    private static bool IsAnyClickOrTouchThisFrame()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            return true;

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        return false;
    }
}
