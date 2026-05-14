using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Main_Home";

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
}
