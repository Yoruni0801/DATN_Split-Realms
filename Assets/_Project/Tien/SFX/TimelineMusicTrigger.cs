using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Quản lý kích hoạt phát nhạc nền (BGM) khi Cutscene / Timeline sắp kết thúc hoặc đã kết thúc.
/// Đã được tối ưu hóa để KHÔNG bị phát tràn sang Scene khác.
/// </summary>
[AddComponentMenu("Audio/Timeline Music Trigger")]
public class TimelineMusicTrigger : MonoBehaviour
{
    [Header("Cutscene Settings")]
    [Tooltip("PlayableDirector của Cutscene/Timeline. Kéo thả Director vào đây.")]
    public PlayableDirector director;

    [Tooltip("Phát nhạc trước khi Timeline kết thúc bao nhiêu giây.")]
    [Range(0f, 30f)]
    public float triggerTimeBeforeEnd = 5f;

    [Tooltip("Tự động phát nhạc ngay khi Timeline kết thúc (nếu trước đó chưa kịp phát).")]
    public bool playIfCutsceneEnded = true;

    [Header("Audio Settings")]
    [Tooltip("Clip nhạc nền cần phát.")]
    public AudioClip musicToPlay;

    [Tooltip("Lặp lại bài nhạc nền này liên tục.")]
    public bool loop = true;

    [Tooltip("Thời gian chuyển đổi Fade mượt mà (giây).")]
    [Range(0f, 5f)]
    public float fadeDuration = 1.2f;

    [Header("Scene Transition & Cleanup Settings")]
    [Tooltip("Tự động dừng hoặc Fade out bài nhạc này khi Scene bị Unload (chuyển sang Scene khác) để không bị phát tràn sang Scene mới.")]
    public bool stopOnSceneChange = true;

    [Tooltip("Tự động dừng nhạc khi GameObject này bị hủy.")]
    public bool stopOnDestroy = true;

    [Tooltip("Tùy chọn: Sử dụng AudioSource cục bộ gắn trong Scene thay vì AudioManager toàn cầu (tự động giải phóng hoàn toàn khi đổi Scene).")]
    public bool useLocalAudioSource = false;

    private bool hasTriggered = false;
    private bool wasPlaying = false;
    private AudioSource localSource;

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.played += OnDirectorPlayed;
            director.stopped += OnDirectorStopped;
        }

        if (stopOnSceneChange)
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }

        if (stopOnSceneChange)
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene == gameObject.scene)
        {
            StopTriggeredMusic();
        }
    }

    private void OnDestroy()
    {
        if (stopOnDestroy)
        {
            StopTriggeredMusic();
        }
    }

    private void OnDirectorPlayed(PlayableDirector d)
    {
        hasTriggered = false;
        wasPlaying = true;
    }

    private void OnDirectorStopped(PlayableDirector d)
    {
        if (playIfCutsceneEnded && !hasTriggered)
        {
            TriggerMusic();
        }
        wasPlaying = false;
    }

    private void Update()
    {
        if (director == null || hasTriggered) return;

        bool isPlaying = director.state == PlayState.Playing;

        if (isPlaying)
        {
            wasPlaying = true;
            double timeRemaining = director.duration - director.time;

            if (timeRemaining <= triggerTimeBeforeEnd)
            {
                TriggerMusic();
            }
        }
        else if (wasPlaying)
        {
            wasPlaying = false;
            if (playIfCutsceneEnded)
            {
                TriggerMusic();
            }
        }
    }

    /// <summary>
    /// Kích hoạt phát bài nhạc nền.
    /// </summary>
    public void TriggerMusic()
    {
        if (hasTriggered || musicToPlay == null) return;
        hasTriggered = true;

        if (useLocalAudioSource)
        {
            if (localSource == null)
            {
                localSource = gameObject.AddComponent<AudioSource>();
                localSource.spatialBlend = 0f;
                localSource.playOnAwake = false;
            }
            localSource.clip = musicToPlay;
            localSource.loop = loop;
            localSource.volume = 1f;
            localSource.Play();
        }
        else if (AudioManager.Instance != null)
        {
            // Không gọi SetDefaultSceneMusic vì đây chỉ là BGM tạm thời của Cutscene/Timeline, tránh bị lan sang Scene khác
            AudioManager.Instance.PlayMusic(musicToPlay, loop, fadeDuration);
        }
    }

    /// <summary>
    /// Dừng bài nhạc nền này nếu đang phát.
    /// </summary>
    public void StopTriggeredMusic()
    {
        if (!hasTriggered || musicToPlay == null) return;

        if (localSource != null && localSource.isPlaying)
        {
            localSource.Stop();
        }

        if (AudioManager.Instance != null && AudioManager.Instance.GetCurrentMusic() == musicToPlay)
        {
            AudioManager.Instance.StopMusic(fadeDuration > 0f ? fadeDuration : 0.5f);
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        wasPlaying = false;
    }
}
