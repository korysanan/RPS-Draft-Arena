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
    [SerializeField] private AudioClip matchPrevClip;
    [SerializeField] private AudioClip matchBgmClip;
    [SerializeField] private AudioClip cardAttackClip;
    [SerializeField] private AudioClip cardTieClip;
    [SerializeField] private AudioClip matchVictoryClip;
    [SerializeField] private AudioClip matchDrawClip;
    [SerializeField] private AudioClip matchDefeatClip;
    [SerializeField] private AudioClip seriesVictoryClip;
    [SerializeField] private AudioClip seriesDefeatClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 1f)] private float cardFlipVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float draftBgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float draftCardPickVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float matchPrevVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float matchBgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float cardAttackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float cardTieVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float matchVictoryVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float matchDrawVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float matchDefeatVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float seriesVictoryVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float seriesDefeatVolume = 0.6f;
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
        if (matchPrevClip == null) matchPrevClip = Resources.Load<AudioClip>("Match_Prev");
        if (matchBgmClip == null) matchBgmClip = Resources.Load<AudioClip>("Match_BGM");
        if (cardAttackClip == null) cardAttackClip = Resources.Load<AudioClip>("Card_Attack");
        if (cardTieClip == null) cardTieClip = Resources.Load<AudioClip>("Card_Tie");
        if (matchVictoryClip == null) matchVictoryClip = Resources.Load<AudioClip>("Match_Victory");
        if (matchDrawClip == null) matchDrawClip = Resources.Load<AudioClip>("Match_Draw");
        if (matchDefeatClip == null) matchDefeatClip = Resources.Load<AudioClip>("Match_Defeat");
        if (seriesVictoryClip == null) seriesVictoryClip = Resources.Load<AudioClip>("Series_Victory");
        if (seriesDefeatClip == null) seriesDefeatClip = Resources.Load<AudioClip>("Series_Defeat");

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

    public void PlayMatchPrev()
    {
        if (bgmSource == null || matchPrevClip == null) return;
        if (bgmSource.clip != matchPrevClip) bgmSource.clip = matchPrevClip;
        bgmSource.volume = matchPrevVolume;
        if (!bgmSource.isPlaying) bgmSource.Play();
    }

    public void StopMatchPrev()
    {
        if (bgmSource == null) return;
        if (bgmSource.clip != matchPrevClip) return;
        if (bgmSource.isPlaying) bgmSource.Stop();
        bgmSource.clip = null;
    }

    // 매치 BGM: 5매치 동안 루프로 끊김 없이 유지. 이미 재생 중이면 그대로 둠.
    public void PlayMatchBgm()
    {
        if (bgmSource == null || matchBgmClip == null) return;
        if (bgmSource.clip == matchBgmClip && bgmSource.isPlaying) return;
        bgmSource.clip = matchBgmClip;
        bgmSource.volume = matchBgmVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopMatchBgm()
    {
        if (bgmSource == null) return;
        if (bgmSource.clip != matchBgmClip) return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    // 시리즈(라운드 최종) 결과 BGM. 다음 라운드 진입 시 EnterHandReviewPhase 의 StopDraftBgm 으로,
    // 씬 이탈 시에는 OnSceneLoaded → StopDraftBgm 으로 자동 정리된다.
    public void PlaySeriesVictory()
    {
        if (bgmSource == null || seriesVictoryClip == null) return;
        if (bgmSource.clip == seriesVictoryClip && bgmSource.isPlaying) return;
        bgmSource.clip = seriesVictoryClip;
        bgmSource.volume = seriesVictoryVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlaySeriesDefeat()
    {
        if (bgmSource == null || seriesDefeatClip == null) return;
        if (bgmSource.clip == seriesDefeatClip && bgmSource.isPlaying) return;
        bgmSource.clip = seriesDefeatClip;
        bgmSource.volume = seriesDefeatVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // 라운드 결과(승리/패배) BGM 정지. "다음 라운드" 버튼처럼 결과 화면을 떠날 때 호출.
    // 현재 재생 중인 게 결과 BGM일 때만 멈춰, 다른 BGM(드래프트/매치)에는 영향을 주지 않는다.
    public void StopSeriesResult()
    {
        if (bgmSource == null) return;
        if (bgmSource.clip != seriesVictoryClip && bgmSource.clip != seriesDefeatClip) return;
        bgmSource.Stop();
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

    public void PlayCardAttack()
    {
        if (cardAttackClip != null) source.PlayOneShot(cardAttackClip, cardAttackVolume);
    }

    public void PlayCardTie()
    {
        if (cardTieClip != null) source.PlayOneShot(cardTieClip, cardTieVolume);
    }

    public void PlayMatchVictory()
    {
        if (matchVictoryClip != null) source.PlayOneShot(matchVictoryClip, matchVictoryVolume);
    }

    public void PlayMatchDraw()
    {
        if (matchDrawClip != null) source.PlayOneShot(matchDrawClip, matchDrawVolume);
    }

    public void PlayMatchDefeat()
    {
        if (matchDefeatClip != null) source.PlayOneShot(matchDefeatClip, matchDefeatVolume);
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
