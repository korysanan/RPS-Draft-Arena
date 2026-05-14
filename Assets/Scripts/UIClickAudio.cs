using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIClickAudio : MonoBehaviour
{
    public static UIClickAudio Instance { get; private set; }

    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip cardFlipClip;
    [SerializeField] private AudioClip draftBgmClip;
    [SerializeField] private AudioClip draftCardPickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 1f)] private float cardFlipVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float draftBgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float draftCardPickVolume = 1f;
    [SerializeField] private string[] excludedScenes = { "Practice", "PracticeMode" };

    private AudioSource source;
    private AudioSource bgmSource;
    private readonly HashSet<int> hookedButtons = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("UIAudioManager (Auto)");
        go.AddComponent<UIClickAudio>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (clickClip == null) clickClip = Resources.Load<AudioClip>("UI_Btn_Click");
        if (cardFlipClip == null) cardFlipClip = Resources.Load<AudioClip>("Card_Flip");
        if (draftBgmClip == null) draftBgmClip = Resources.Load<AudioClip>("Draft_BGM");
        if (draftCardPickClip == null) draftCardPickClip = Resources.Load<AudioClip>("Draft_Card_Pick");

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = volume;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        HookButtonsIn(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookButtonsIn(scene);
        StopDraftBgm();
    }

    public void PlayDraftBgm()
    {
        if (bgmSource == null || draftBgmClip == null) return;
        if (bgmSource.clip != draftBgmClip) bgmSource.clip = draftBgmClip;
        bgmSource.volume = draftBgmVolume;
        if (!bgmSource.isPlaying) bgmSource.Play();
    }

    public void StopDraftBgm()
    {
        if (bgmSource == null) return;
        if (bgmSource.isPlaying) bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void HookButtonsIn(Scene scene)
    {
        if (IsExcluded(scene.name)) return;

        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            if (btn.GetComponent<UIClickSoundIgnore>() != null) continue;

            int id = btn.GetInstanceID();
            if (!hookedButtons.Add(id)) continue;
            btn.onClick.AddListener(PlayClick);
        }
    }

    public void Register(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<UIClickSoundIgnore>() != null) return;
        int id = btn.GetInstanceID();
        if (!hookedButtons.Add(id)) return;
        btn.onClick.AddListener(PlayClick);
    }

    public void PlayClick()
    {
        if (clickClip != null) source.PlayOneShot(clickClip, volume);
    }

    public void PlayCardFlip()
    {
        if (cardFlipClip != null) source.PlayOneShot(cardFlipClip, cardFlipVolume);
    }

    public void PlayDraftCardPick()
    {
        if (draftCardPickClip != null) source.PlayOneShot(draftCardPickClip, draftCardPickVolume);
    }

    private bool IsExcluded(string sceneName)
    {
        if (excludedScenes == null) return false;
        for (int i = 0; i < excludedScenes.Length; i++)
        {
            if (excludedScenes[i] == sceneName) return true;
        }
        return false;
    }
}
