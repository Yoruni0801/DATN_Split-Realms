using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    private const string MasterKey = "MasterVolume";
    private const string BgmKey = "BGMVolume";
    private const string SfxKey = "SFXVolume";
    private const string MuteKey = "GameMuted";

    private const int InitialPoolSize = 20;
    private const int MaxPoolSize = 50;
    private const float MinLinearVolume = 0.0001f;
    private const float AudibleThreshold = 0.05f;
    private const float MinDecibelVolume = -80f;
    private const float DecibelMultiplier = 20f;
    private const float DefaultFadeDuration = 1.2f;

    private static AudioManager _instance;
    private static bool isApplicationQuitting = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitQuittingCheck()
    {
        isApplicationQuitting = false;
        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;
    }

    private static void HandleApplicationQuitting()
    {
        isApplicationQuitting = true;
    }

    public static bool HasInstance => _instance != null && !isApplicationQuitting;

    public static AudioManager Instance
    {
        get
        {
            if (isApplicationQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<AudioManager>();
#else
                _instance = FindObjectOfType<AudioManager>();
#endif
            }
            return _instance;
        }
        set => _instance = value;
    }

    [Header("Audio Mixer")]
    public AudioMixer mainMixer;
    public string masterParam = "MasterVolume";
    public string bgmParam = "BGMVolume";
    public string sfxParam = "SFXVolume";

    [Header("Sources")]
    public AudioSource bgmSource;

    [System.Serializable]
    public class SceneAudioProfile
    {
        public string sceneName;
        public AudioClip sceneBGM;
        public bool loop = true;
        public AudioClip defaultBossBGM;
        public bool bossLoop = true;
        [Range(0f, 5f)] public float fadeDuration = DefaultFadeDuration;
    }

    [Header("Scene Audio Registry")]
    public List<SceneAudioProfile> sceneAudioProfiles = new List<SceneAudioProfile>();
    public bool stopBgmIfSceneUnregistered = false;

    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private bool isMuted;

    private bool mixerWorking = false;
    private AudioMixerGroup masterGroup;
    private AudioMixerGroup bgmGroup;
    private AudioMixerGroup sfxGroup;

    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnBGMVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action<bool> OnMuteChanged;

    private readonly Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private int totalSfxCreated = 0;
    private bool isSubscribed = false;

    private AudioClip defaultSceneBGM;
    private AudioClip defaultBossBGM;
    private AudioClip previousBgmClip;
    private float previousBgmTime = 0f;
    private bool isBossMusicActive = false;

    private readonly HashSet<PlayableDirector> activePlayingDirectors = new HashSet<PlayableDirector>();
    private readonly HashSet<PlayableDirector> trackedDirectors = new HashSet<PlayableDirector>();
    private AudioClip bgmClipBeforeTimeline;
    private float bgmTimeBeforeTimeline = 0f;
    private bool bgmLoopBeforeTimeline = true;
    private bool isBgmPausedByTimeline = false;

    private CancellationTokenSource fadeCts;
    private CancellationTokenSource sceneApplyCts;
    private AudioClip currentFadingTargetClip;

    private void Awake()
    {
        if (_instance == null || _instance == this)
        {
            _instance = this;
            transform.SetParent(null);
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            AudioListener listener = GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
                Destroy(listener);
            }

            EnsureBgmSource();
            TryLoadMixer();
            InitializeSFXPool();
            LoadSettings();
            EnsureAudioListener();
            SubscribeEvents();
        }
        else
        {
            UnsubscribeEvents();
            enabled = false;
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (_instance != null && _instance != this)
        {
            return;
        }

        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        CancelActiveOperations();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        CancelActiveOperations();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void CancelActiveOperations()
    {
        if (fadeCts != null)
        {
            fadeCts.Cancel();
            fadeCts.Dispose();
            fadeCts = null;
        }

        if (sceneApplyCts != null)
        {
            sceneApplyCts.Cancel();
            sceneApplyCts.Dispose();
            sceneApplyCts = null;
        }
    }

    private void SubscribeEvents()
    {
        if (isSubscribed) return;
        isSubscribed = true;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        ScanAndRegisterSceneDirectors();
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed) return;
        isSubscribed = false;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterAllDirectors();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Do not cancel fadeCts here. If the previous scene was fading out a track, 
        // let it finish so we don't get a jarring max-volume spike of the old BGM.
        
        isBossMusicActive = false;
        previousBgmClip = null;
        previousBgmTime = 0f;

        isBgmPausedByTimeline = false;
        bgmClipBeforeTimeline = null;
        bgmTimeBeforeTimeline = 0f;

        EnsureBgmSource();
        EnsureAudioListener();
        ApplyVolume();

        UnregisterAllDirectors();
        ScanAndRegisterSceneDirectors();

        if (sceneApplyCts != null)
        {
            sceneApplyCts.Cancel();
            sceneApplyCts.Dispose();
        }
        sceneApplyCts = new CancellationTokenSource();
        _ = AutoApplySceneAudioDelayedAsync(scene.name, sceneApplyCts.Token);
    }

    private async Task AutoApplySceneAudioDelayedAsync(string sceneName, CancellationToken ct)
    {
        await Task.Yield();
        if (ct.IsCancellationRequested || this == null) return;

        // Re-scan scene directors after delay in case some spawned or initialized on Start
        ScanAndRegisterSceneDirectors();

#if UNITY_2023_1_OR_NEWER
        var localController = UnityEngine.Object.FindFirstObjectByType<Duy.Audio.SceneBGMController>();
#else
        var localController = UnityEngine.Object.FindObjectOfType<Duy.Audio.SceneBGMController>();
#endif
        if (localController != null)
        {
            return;
        }

        SceneAudioProfile profile = FindAudioProfileForScene(sceneName);
        if (profile != null)
        {
            defaultSceneBGM = profile.sceneBGM;
            defaultBossBGM = profile.defaultBossBGM;

            PreloadAudioClip(profile.sceneBGM);
            PreloadAudioClip(profile.defaultBossBGM);

            if (IsTimelinePlaying())
            {
                bgmClipBeforeTimeline = profile.sceneBGM;
                bgmTimeBeforeTimeline = 0f;
                bgmLoopBeforeTimeline = profile.loop;
                return;
            }

            if (profile.sceneBGM != null)
            {
                PlayMusic(profile.sceneBGM, profile.loop, profile.fadeDuration);
            }
            else if (stopBgmIfSceneUnregistered)
            {
                StopMusic(profile.fadeDuration > 0f ? profile.fadeDuration : 1f);
            }
        }
        else 
        {
            if (IsTimelinePlaying())
            {
                if (stopBgmIfSceneUnregistered)
                {
                    bgmClipBeforeTimeline = null;
                }
                return;
            }

            if (stopBgmIfSceneUnregistered)
            {
                StopMusic(1f);
            }
        }
    }

    public SceneAudioProfile FindAudioProfileForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || sceneAudioProfiles == null) return null;

        string cleanName = sceneName.Trim();
        foreach (var profile in sceneAudioProfiles)
        {
            if (profile == null || string.IsNullOrEmpty(profile.sceneName)) continue;
            string profileName = profile.sceneName.Trim();
            if (string.Equals(cleanName, profileName, StringComparison.OrdinalIgnoreCase)
                || cleanName.EndsWith("/" + profileName, StringComparison.OrdinalIgnoreCase)
                || cleanName.EndsWith("\\" + profileName, StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }
        return null;
    }

    private void EnsureBgmSource()
    {
        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>();

        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        bgmSource.spatialBlend = 0f;
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.ignoreListenerPause = true;

        if (mixerWorking && bgmGroup != null && bgmSource.outputAudioMixerGroup != bgmGroup)
        {
            bgmSource.outputAudioMixerGroup = bgmGroup;
        }
    }

    public void EnsureAudioListener()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
        {
            AudioListener mainListener = Camera.main.GetComponent<AudioListener>();
            if (mainListener != null)
            {
                if (!mainListener.enabled) mainListener.enabled = true;
                return;
            }
        }

#if UNITY_2023_1_OR_NEWER
        var allListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allListeners = UnityEngine.Object.FindObjectsOfType<AudioListener>(true);
#endif
        if (allListeners != null && allListeners.Length > 0)
        {
            foreach (var listener in allListeners)
            {
                if (listener != null && listener.gameObject.activeInHierarchy)
                {
                    listener.enabled = true;
                    return;
                }
            }
        }

        if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
        {
            var newListener = Camera.main.gameObject.AddComponent<AudioListener>();
            newListener.enabled = true;
        }
    }

    private void TryLoadMixer()
    {
        if (mainMixer == null)
        {
            mainMixer = Resources.Load<AudioMixer>("MainMixer");
        }

        if (mainMixer != null)
        {
            mixerWorking = mainMixer.GetFloat(bgmParam, out float _) || mainMixer.GetFloat(masterParam, out float _);

            if (mixerWorking)
            {
                var masterGroups = mainMixer.FindMatchingGroups("Master");
                if (masterGroups != null && masterGroups.Length > 0) masterGroup = masterGroups[0];

                var bgmGroups = mainMixer.FindMatchingGroups("BGM");
                if (bgmGroups != null && bgmGroups.Length > 0) bgmGroup = bgmGroups[0];

                var sfxGroups = mainMixer.FindMatchingGroups("SFX");
                if (sfxGroups != null && sfxGroups.Length > 0) sfxGroup = sfxGroups[0];
            }
        }
    }

    private void InitializeSFXPool()
    {
        for (int i = 0; i < InitialPoolSize; i++)
        {
            CreateNewSFXSource();
        }
    }

    private AudioSource CreateNewSFXSource(bool enqueue = true)
    {
        if (totalSfxCreated >= MaxPoolSize) return null;
        totalSfxCreated++;

        GameObject sfxObj = new GameObject("SFX_PoolItem_" + totalSfxCreated);
        sfxObj.transform.SetParent(transform);
        AudioSource src = sfxObj.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        if (mixerWorking && sfxGroup != null)
        {
            src.outputAudioMixerGroup = sfxGroup;
        }
        if (enqueue) sfxPool.Enqueue(src);
        return src;
    }

    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        bgmVolume = PlayerPrefs.GetFloat(BgmKey, 1f);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        isMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        ApplyVolume();
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MasterKey, masterVolume);
        PlayerPrefs.SetFloat(BgmKey, bgmVolume);
        PlayerPrefs.SetFloat(SfxKey, sfxVolume);
        PlayerPrefs.SetInt(MuteKey, isMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void UpdateVolume(float master, float bgm, float sfx, bool muted, bool savePrefs = false)
    {
        masterVolume = master;
        bgmVolume = bgm;
        sfxVolume = sfx;
        isMuted = muted;

        ApplyVolume();

        if (savePrefs)
        {
            SaveSettings();
        }
    }

    public void UpdateVolume(float bgm, float sfx, bool muted, bool savePrefs = false)
    {
        UpdateVolume(masterVolume, bgm, sfx, muted, savePrefs);
    }

    private void ApplyVolume()
    {
        float effectiveMaster = isMuted ? 0f : masterVolume;
        float effectiveBgm = isMuted ? 0f : bgmVolume;
        float effectiveSfx = isMuted ? 0f : sfxVolume;

        if (mixerWorking && mainMixer != null)
        {
            SetMixerVolume(masterParam, effectiveMaster);
            SetMixerVolume(bgmParam, effectiveBgm);
            SetMixerVolume(sfxParam, effectiveSfx);

            if (bgmSource != null && fadeCts == null)
            {
                bgmSource.volume = 1f;
            }
        }
        else
        {
            AudioListener.volume = effectiveMaster;
            if (bgmSource != null && fadeCts == null)
            {
                bgmSource.volume = effectiveBgm;
            }
        }

        OnMasterVolumeChanged?.Invoke(effectiveMaster);
        OnBGMVolumeChanged?.Invoke(effectiveBgm);
        OnSFXVolumeChanged?.Invoke(effectiveSfx);
        OnMuteChanged?.Invoke(isMuted);
    }

    private void SetMixerVolume(string parameterName, float linearVolume)
    {
        if (mainMixer == null || string.IsNullOrEmpty(parameterName)) return;
        float db = (linearVolume <= MinLinearVolume) ? MinDecibelVolume : Mathf.Log10(Mathf.Clamp(linearVolume, MinLinearVolume, 1f)) * DecibelMultiplier;
        mainMixer.SetFloat(parameterName, db);
    }

    public void ApplyVolumeToVideoPlayerPublic(VideoPlayer vp)
    {
        if (vp == null) return;

        bool muted = isMuted || bgmVolume <= MinLinearVolume || masterVolume <= MinLinearVolume;
        float targetVolume = muted ? 0f : (bgmVolume * masterVolume);

        for (ushort i = 0; i < vp.audioTrackCount; i++)
        {
            try
            {
                if (muted)
                {
                    vp.SetDirectAudioMute(i, true);
                    vp.SetDirectAudioVolume(i, 0f);
                }
                else
                {
                    vp.SetDirectAudioMute(i, false);
                    vp.EnableAudioTrack(i, true);
                    vp.SetDirectAudioVolume(i, targetVolume);
                }

                AudioSource trackSrc = vp.GetTargetAudioSource(i);
                if (trackSrc != null)
                {
                    trackSrc.volume = 1f;
                    if (mixerWorking && bgmGroup != null && trackSrc.outputAudioMixerGroup != bgmGroup)
                    {
                        trackSrc.outputAudioMixerGroup = bgmGroup;
                    }
                }
            }
            catch { }
        }

        AudioSource attachedSrc = vp.GetComponent<AudioSource>();
        if (attachedSrc != null)
        {
            attachedSrc.volume = 1f;
            if (mixerWorking && bgmGroup != null && attachedSrc.outputAudioMixerGroup != bgmGroup)
            {
                attachedSrc.outputAudioMixerGroup = bgmGroup;
            }
        }
    }

    [ContextMenu("Reset Audio Settings To Default (100% Vol)")]
    public void ResetAudioSettingsToDefault()
    {
        masterVolume = 1f;
        bgmVolume = 1f;
        sfxVolume = 1f;
        isMuted = false;
        SaveSettings();
        ApplyVolume();
        Debug.Log("[AudioManager] Reset all volumes to 100% and unmuted.", this);
    }

    public static void PlaySFXIfAvailable(AudioClip clip, Vector3 position)
    {
        if (isApplicationQuitting || _instance == null || clip == null) return;
        _instance.PlaySFX(clip, position);
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float pitch = 1f)
    {
        if (clip == null) return;

        if (isMuted || sfxVolume <= MinLinearVolume)
        {
            return;
        }

        AudioSource src = null;
        while (sfxPool.Count > 0 && src == null)
        {
            src = sfxPool.Dequeue();
        }

        if (src == null)
        {
            src = CreateNewSFXSource(false);
            if (src == null) return;
        }

        src.transform.position = position;
        src.clip = clip;
        src.pitch = pitch;
        src.volume = mixerWorking ? 1f : sfxVolume;
        src.Play();

        float duration = (clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch))) + 0.1f;
        _ = ReturnToPoolAsync(src, duration);
    }

    private async Task ReturnToPoolAsync(AudioSource src, float delay)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
            if (src != null)
            {
                src.Stop();
                src.clip = null;
                sfxPool.Enqueue(src);
            }
        }
        catch { }
    }

    public void PreloadAudioClip(AudioClip clip)
    {
        if (clip == null) return;
        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    public void PreloadAudioClips(IEnumerable<AudioClip> clips)
    {
        if (clips == null) return;
        foreach (var clip in clips)
        {
            PreloadAudioClip(clip);
        }
    }

    public void SetDefaultSceneMusic(AudioClip clip, AudioClip bossBGM = null)
    {
        defaultSceneBGM = clip;
        if (bossBGM != null)
        {
            defaultBossBGM = bossBGM;
        }
        PreloadAudioClip(clip);
        PreloadAudioClip(bossBGM);

        if (isBgmPausedByTimeline)
        {
            bgmClipBeforeTimeline = clip;
            bgmTimeBeforeTimeline = 0f;
            bgmLoopBeforeTimeline = true;
        }
    }

    public AudioClip GetDefaultBossBGMForCurrentScene()
    {
        if (defaultBossBGM != null) return defaultBossBGM;

        string currentScene = SceneManager.GetActiveScene().name;
        SceneAudioProfile profile = FindAudioProfileForScene(currentScene);
        if (profile != null && profile.defaultBossBGM != null)
        {
            defaultBossBGM = profile.defaultBossBGM;
            PreloadAudioClip(defaultBossBGM);
            return defaultBossBGM;
        }
        return null;
    }

    public void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 0f)
    {
        EnsureBgmSource();

        // Prevent SceneBGMController or delayed loader from forcibly playing the default BGM while a cutscene is running.
        if (isBgmPausedByTimeline && clip != null && clip == defaultSceneBGM)
        {
            return;
        }

        isBgmPausedByTimeline = false; // Ngăn không cho restore BGM cũ nếu có lệnh thay đổi BGM tường minh

        if (clip == null)
        {
            StopMusic(fadeDuration);
            return;
        }

        PreloadAudioClip(clip);

        if (bgmSource.clip == clip && bgmSource.isPlaying && bgmSource.volume > AudibleThreshold)
        {
            return;
        }

        if (bgmSource.clip == clip && (!bgmSource.isPlaying || bgmSource.volume <= AudibleThreshold))
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
            currentFadingTargetClip = null;
            bgmSource.volume = mixerWorking ? 1f : GetEffectiveBGMVolume();
            bgmSource.Play();
            return;
        }

        if (fadeDuration > AudibleThreshold && bgmSource.isPlaying)
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
            }
            currentFadingTargetClip = clip;
            fadeCts = new CancellationTokenSource();
            _ = FadeMusicAsync(clip, fadeDuration, loop, 0f, fadeCts.Token);
        }
        else
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
            currentFadingTargetClip = null;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.time = 0f;
            bgmSource.volume = mixerWorking ? 1f : GetEffectiveBGMVolume();

            if (mixerWorking && bgmGroup != null && bgmSource.outputAudioMixerGroup != bgmGroup)
            {
                bgmSource.outputAudioMixerGroup = bgmGroup;
            }

            bgmSource.Play();
        }
    }

    public void PlayBossMusic(AudioClip bossClip, bool loop = true, float fadeDuration = DefaultFadeDuration)
    {
        AudioClip clipToPlay = bossClip != null ? bossClip : GetDefaultBossBGMForCurrentScene();
        if (clipToPlay == null) return;

        EnsureBgmSource();
        PreloadAudioClip(clipToPlay);

        if (bgmSource.clip == clipToPlay && bgmSource.isPlaying && bgmSource.volume > AudibleThreshold)
        {
            isBossMusicActive = true;
            return;
        }

        if (currentFadingTargetClip == clipToPlay && isBossMusicActive)
        {
            return;
        }

        if (!isBossMusicActive)
        {
            if (bgmSource.isPlaying && bgmSource.clip != null && bgmSource.clip != clipToPlay)
            {
                previousBgmClip = bgmSource.clip;
                previousBgmTime = bgmSource.time;
            }
            else if (defaultSceneBGM != null && defaultSceneBGM != clipToPlay)
            {
                previousBgmClip = defaultSceneBGM;
                previousBgmTime = 0f;
            }
        }

        isBossMusicActive = true;
        PlayMusic(clipToPlay, loop, fadeDuration);
    }

    public void StopBossMusic(bool restorePrevious = true, float fadeDuration = 1.5f)
    {
        if (!isBossMusicActive) return;
        isBossMusicActive = false;

        if (restorePrevious)
        {
            AudioClip targetRestoreClip = previousBgmClip != null ? previousBgmClip : defaultSceneBGM;
            if (targetRestoreClip != null)
            {
                float resumeTime = (previousBgmClip == targetRestoreClip) ? previousBgmTime : 0f;
                ResumeMusicWithFade(targetRestoreClip, fadeDuration, true, resumeTime);
                previousBgmClip = null;
                previousBgmTime = 0f;
                return;
            }
        }

        StopMusic(fadeDuration);
    }

    public void RestoreSceneMusic(float fadeDuration = 1.5f)
    {
        AudioClip clipToRestore = previousBgmClip != null ? previousBgmClip : defaultSceneBGM;
        if (clipToRestore != null)
        {
            isBossMusicActive = false;
            PlayMusic(clipToRestore, true, fadeDuration);
            previousBgmClip = null;
            previousBgmTime = 0f;
        }
    }

    private void ResumeMusicWithFade(AudioClip clip, float duration, bool loop, float startTime = 0f)
    {
        if (clip == null) return;
        EnsureBgmSource();
        PreloadAudioClip(clip);

        if (duration > AudibleThreshold)
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
            }
            currentFadingTargetClip = clip;
            fadeCts = new CancellationTokenSource();
            _ = FadeMusicAsync(clip, duration, loop, startTime, fadeCts.Token);
        }
        else
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
            currentFadingTargetClip = null;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.1f));
            bgmSource.volume = mixerWorking ? 1f : GetEffectiveBGMVolume();

            if (mixerWorking && bgmGroup != null && bgmSource.outputAudioMixerGroup != bgmGroup)
            {
                bgmSource.outputAudioMixerGroup = bgmGroup;
            }

            bgmSource.Play();
        }
    }

    private async Task FadeMusicAsync(AudioClip newClip, float duration, bool loop, float startTime, CancellationToken ct)
    {
        EnsureBgmSource();
        float targetMaxVolume = mixerWorking ? 1f : GetEffectiveBGMVolume();
        float halfDuration = Mathf.Max(AudibleThreshold, duration * 0.5f);

        // 1. Fade out current music
        if (bgmSource.isPlaying && bgmSource.volume > 0f)
        {
            float startVol = bgmSource.volume;
            float timer = 0f;
            while (timer < halfDuration)
            {
                if (ct.IsCancellationRequested || bgmSource == null) return;
                timer += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / halfDuration);
                await Task.Yield();
            }
            if (bgmSource != null)
            {
                bgmSource.volume = 0f;
                bgmSource.Stop();
            }
        }

        if (newClip == null)
        {
            if (bgmSource != null)
            {
                bgmSource.clip = null;
                bgmSource.volume = targetMaxVolume;
            }
            fadeCts = null;
            currentFadingTargetClip = null;
            return;
        }

        // 2. Switch to new clip
        if (bgmSource != null)
        {
            bgmSource.clip = newClip;
            bgmSource.loop = loop;
            bgmSource.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, newClip.length - 0.1f));

            if (mixerWorking && bgmGroup != null && bgmSource.outputAudioMixerGroup != bgmGroup)
            {
                bgmSource.outputAudioMixerGroup = bgmGroup;
            }

            bgmSource.volume = 0f;
            bgmSource.Play();

            // 3. Fade in new music
            float inTimer = 0f;
            while (inTimer < halfDuration)
            {
                if (ct.IsCancellationRequested || bgmSource == null) return;
                inTimer += Time.unscaledDeltaTime;
                float currentMax = mixerWorking ? 1f : GetEffectiveBGMVolume();
                bgmSource.volume = Mathf.Lerp(0f, currentMax, inTimer / halfDuration);
                await Task.Yield();
            }

            if (bgmSource != null)
            {
                bgmSource.volume = mixerWorking ? 1f : GetEffectiveBGMVolume();
            }
        }

        fadeCts = null;
        currentFadingTargetClip = null;
    }

    public void PauseMusic(float fadeDuration = 0f)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        bgmClipBeforeTimeline = bgmSource.clip;
        bgmTimeBeforeTimeline = bgmSource.time;
        bgmLoopBeforeTimeline = bgmSource.loop;
        isBgmPausedByTimeline = true;
        StopMusic(fadeDuration);
    }

    public void ResumeMusic(float fadeDuration = 1.2f)
    {
        if (isBgmPausedByTimeline)
        {
            isBgmPausedByTimeline = false;
            RestoreBgmAfterTimeline();
        }
    }

    public void StopMusic(float fadeDuration = 0f)
    {
        if (bgmSource == null) return;

        if (fadeDuration > AudibleThreshold && bgmSource.isPlaying)
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
            }
            fadeCts = new CancellationTokenSource();
            _ = FadeMusicAsync(null, fadeDuration, false, 0f, fadeCts.Token);
        }
        else
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    #region PlayableDirector / Timeline Audio Handling

    public static void RegisterTimelineDirector(PlayableDirector director)
    {
        if (isApplicationQuitting || _instance == null || director == null) return;
        _instance.RegisterDirector(director);
    }

    public static void UnregisterTimelineDirector(PlayableDirector director)
    {
        if (isApplicationQuitting || _instance == null || director == null) return;
        _instance.UnregisterDirector(director);
    }

    public void RegisterDirector(PlayableDirector director)
    {
        if (director == null) return;

        if (trackedDirectors.Add(director))
        {
            director.played -= HandleDirectorPlayed;
            director.played += HandleDirectorPlayed;

            director.stopped -= HandleDirectorStopped;
            director.stopped += HandleDirectorStopped;

            director.paused -= HandleDirectorPaused;
            director.paused += HandleDirectorPaused;

            if (director.state == PlayState.Playing)
            {
                HandleDirectorPlayed(director);
            }
        }
    }

    public void UnregisterDirector(PlayableDirector director)
    {
        if (director == null) return;

        if (trackedDirectors.Remove(director))
        {
            director.played -= HandleDirectorPlayed;
            director.stopped -= HandleDirectorStopped;
            director.paused -= HandleDirectorPaused;
        }

        activePlayingDirectors.Remove(director);
        CheckActiveDirectorsAndRestoreBgmIfNeeded();
    }

    public void ScanAndRegisterSceneDirectors()
    {
        PruneNullDirectors();

#if UNITY_2023_1_OR_NEWER
        var directors = UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var directors = UnityEngine.Object.FindObjectsOfType<PlayableDirector>(true);
#endif
        if (directors == null || directors.Length == 0) return;

        foreach (var dir in directors)
        {
            if (dir != null)
            {
                RegisterDirector(dir);
            }
        }
    }

    private void UnregisterAllDirectors()
    {
        foreach (var dir in trackedDirectors)
        {
            if (dir != null)
            {
                dir.played -= HandleDirectorPlayed;
                dir.stopped -= HandleDirectorStopped;
                dir.paused -= HandleDirectorPaused;
            }
        }

        trackedDirectors.Clear();
        activePlayingDirectors.Clear();
    }

    private void HandleDirectorPlayed(PlayableDirector director)
    {
        if (director == null) return;

        activePlayingDirectors.Add(director);

        if (!isBgmPausedByTimeline)
        {
            PauseBgmForTimeline();
        }
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (director != null)
        {
            activePlayingDirectors.Remove(director);
        }

        CheckActiveDirectorsAndRestoreBgmIfNeeded();
    }

    private void HandleDirectorPaused(PlayableDirector director)
    {
        if (director != null)
        {
            activePlayingDirectors.Remove(director);
        }

        CheckActiveDirectorsAndRestoreBgmIfNeeded();
    }

    private void PauseBgmForTimeline()
    {
        isBgmPausedByTimeline = true;

        if (bgmSource != null && bgmSource.clip != null)
        {
            bgmClipBeforeTimeline = bgmSource.clip;
            bgmTimeBeforeTimeline = bgmSource.time;
            bgmLoopBeforeTimeline = bgmSource.loop;
        }
        else
        {
            bgmClipBeforeTimeline = defaultSceneBGM;
            bgmTimeBeforeTimeline = 0f;
            bgmLoopBeforeTimeline = true;
        }

        StopMusic(0.5f);
    }

    private void CheckActiveDirectorsAndRestoreBgmIfNeeded()
    {
        PruneNullDirectors();

        if (activePlayingDirectors.Count == 0 && isBgmPausedByTimeline)
        {
            isBgmPausedByTimeline = false;
            RestoreBgmAfterTimeline();
        }
    }

    private void RestoreBgmAfterTimeline()
    {
        AudioClip targetClip = bgmClipBeforeTimeline != null ? bgmClipBeforeTimeline : defaultSceneBGM;
        if (targetClip == null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            SceneAudioProfile profile = FindAudioProfileForScene(currentScene);
            if (profile != null)
            {
                targetClip = profile.sceneBGM;
            }
        }

        if (targetClip != null)
        {
            float resumeTime = (targetClip == bgmClipBeforeTimeline) ? bgmTimeBeforeTimeline : 0f;
            bool loop = (targetClip == bgmClipBeforeTimeline) ? bgmLoopBeforeTimeline : true;
            ResumeMusicWithFade(targetClip, 1.2f, loop, resumeTime);
        }

        bgmClipBeforeTimeline = null;
        bgmTimeBeforeTimeline = 0f;
    }

    private void PruneNullDirectors()
    {
        trackedDirectors.RemoveWhere(d => d == null);
        activePlayingDirectors.RemoveWhere(d => d == null || d.state != PlayState.Playing);
    }

    public bool IsTimelinePlaying()
    {
        PruneNullDirectors();
        return activePlayingDirectors.Count > 0;
    }

    #endregion

    public AudioClip GetCurrentMusic() => bgmSource != null ? bgmSource.clip : null;
    public AudioClip GetPreviousMusic() => previousBgmClip;
    public bool IsBossMusicActive() => isBossMusicActive;
    public bool IsMusicPlaying() => bgmSource != null && bgmSource.isPlaying;

    public float GetMasterVolume() => masterVolume;
    public float GetBgmVolume() => bgmVolume;
    public float GetSfxVolume() => sfxVolume;
    public bool IsMuted() => isMuted;
    public float GetEffectiveMasterVolume() => isMuted ? 0f : masterVolume;
    public float GetEffectiveBGMVolume() => isMuted ? 0f : bgmVolume;
    public float GetEffectiveSFXVolume() => isMuted ? 0f : sfxVolume;
}
