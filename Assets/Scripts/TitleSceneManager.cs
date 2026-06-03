using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Main_Home";
    [SerializeField] private string tutorialSceneName = "Tutorial";
    [SerializeField] private GameObject quitConfirmPanel;   // Exit 버튼 → 종료 확인 팝업

    [Header("Audio")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip enterButtonClip;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private bool transitioning;

    private void Awake()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

    private void Start()
    {
        if (bgmClip != null) bgmSource.Play();
    }

    public void GoToHome()
    {
        if (transitioning) return;
        transitioning = true;

        if (enterButtonClip != null)
        {
            sfxSource.PlayOneShot(enterButtonClip);
            StartCoroutine(LoadAfterDelay(enterButtonClip.length));
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private IEnumerator LoadAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SceneManager.LoadScene(nextSceneName);
    }

    // 튜토리얼 버튼: Tutorial 씬으로 이동
    public void GoToTutorial()
    {
        if (transitioning) return;
        transitioning = true;
        SceneManager.LoadScene(tutorialSceneName);
    }

    // Exit 버튼: 종료 확인 팝업 표시 (PracticeMode와 동일 흐름)
    public void OnQuitClicked()
    {
        if (quitConfirmPanel != null)
        {
            // 풀스크린 블로커가 뒤 UI를 막도록 항상 최상단으로 올린다.
            quitConfirmPanel.transform.SetAsLastSibling();
            quitConfirmPanel.SetActive(true);
        }
    }

    // 확인 팝업의 "닫기" 버튼: 팝업만 닫음
    public void CancelQuit()
    {
        if (quitConfirmPanel != null)
            quitConfirmPanel.SetActive(false);
    }

    // 확인 팝업의 "종료" 버튼: 애플리케이션 종료
    public void ConfirmQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
