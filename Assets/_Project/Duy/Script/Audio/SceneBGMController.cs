using UnityEngine;
using System.Collections;

namespace Duy.Audio
{
    /// <summary>
    /// Component quản lý nhạc nền (BGM) và nhạc Boss riêng cho từng Scene / Map.
    /// Gắn component này vào một GameObject trong Scene và gán AudioClip mong muốn.
    /// Đã được tối ưu để hoạt động nhẹ nhàng, tự động Preload chống lag 100%.
    /// </summary>
    [AddComponentMenu("Audio/Scene BGM Controller")]
    public class SceneBGMController : MonoBehaviour
    {
        public static SceneBGMController Instance { get; private set; }

        [Header("Scene BGM Configuration")]
        [Tooltip("Clip nhạc nền mặc định cho Scene/Map này.")]
        [SerializeField] private AudioClip sceneBGM;

        [Tooltip("Clip nhạc Boss mặc định riêng cho Map này. Khi người chơi gặp Boss trong Map, nếu Boss chưa được gán nhạc riêng thì bài này sẽ tự động phát.")]
        [SerializeField] private AudioClip defaultBossBGM;

        [Tooltip("Tự động phát bài nhạc này khi Scene bắt đầu.")]
        [SerializeField] private bool playOnStart = true;

        [Tooltip("Lặp lại bài nhạc nền này liên tục.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Lặp lại bài nhạc Boss mặc định này liên tục.")]
        [SerializeField] private bool bossLoop = true;

        [Tooltip("Thời gian Fade In / Fade Out mượt mà khi chuyển sang bài này (giây).")]
        [SerializeField] [Range(0f, 5f)] private float fadeDuration = 1.2f;

        [Tooltip("Tự động khôi phục lại nhạc nền Scene sau khi Cutscene kết thúc.")]
        [SerializeField] private bool restoreOnCutsceneEnd = true;

        [Header("Preload Settings (Chống Lag Khi Gặp Boss)")]
        [Tooltip("Tự động nạp trước (Preload) dữ liệu âm thanh vào RAM ngay khi Scene tải xong để loại bỏ 100% hiện tượng khựng/lag khi gặp Boss.")]
        [SerializeField] private bool preloadClipsOnAwake = true;

        [Tooltip("Các clip âm thanh đặc biệt khác trong Map cần nạp trước (ví dụ: nhạc Phase 2, SFX lớn...).")]
        [SerializeField] private AudioClip[] additionalPreloadClips;

        [Header("Override Settings")]
        [Tooltip("Nếu true, luôn phát lại từ đầu kể cả khi bài nhạc trước đó đang phát bài tương tự.")]
        [SerializeField] private bool forceRestart = false;

        private void Awake()
        {
            Instance = this;

            if (preloadClipsOnAwake)
            {
                PreloadAllSceneClips();
            }
        }

        private void PreloadAllSceneClips()
        {
            if (sceneBGM != null && sceneBGM.loadState == AudioDataLoadState.Unloaded)
            {
                sceneBGM.LoadAudioData();
            }

            if (defaultBossBGM != null && defaultBossBGM.loadState == AudioDataLoadState.Unloaded)
            {
                defaultBossBGM.LoadAudioData();
            }

            if (additionalPreloadClips != null)
            {
                foreach (var clip in additionalPreloadClips)
                {
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    {
                        clip.LoadAudioData();
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (restoreOnCutsceneEnd)
            {
                CutsceneEngine.Cutscene.onCutsceneStopped += HandleCutsceneStopped;
            }
        }

        private void OnDisable()
        {
            if (restoreOnCutsceneEnd)
            {
                CutsceneEngine.Cutscene.onCutsceneStopped -= HandleCutsceneStopped;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleCutsceneStopped(CutsceneEngine.Cutscene cutscene)
        {
            if (restoreOnCutsceneEnd)
            {
                ApplySceneBGM();
            }
        }

        private IEnumerator Start()
        {
            if (!playOnStart || sceneBGM == null) yield break;

            // Chờ 1 frame duy nhất sau khi Scene nạp xong để AudioManager sẵn sàng
            yield return null;
            ApplySceneBGM();
        }

        /// <summary>
        /// Kích hoạt phát nhạc nền của Scene này qua AudioManager.
        /// </summary>
        public void ApplySceneBGM()
        {
            if (sceneBGM == null || AudioManager.Instance == null) return;

            // Đảm bảo AudioListener toàn cục luôn hoạt động bình thường
            AudioManager.Instance.EnsureAudioListener();

            // Đăng ký bài nhạc Scene mặc định và nhạc Boss mặc định của map
            AudioManager.Instance.SetDefaultSceneMusic(sceneBGM, defaultBossBGM);

            // Kiểm tra xem BGM hiện tại có đang phát đúng bài và có âm lượng hay không
            bool isCurrentPlayingWithVolume = AudioManager.Instance.GetCurrentMusic() == sceneBGM 
                && AudioManager.Instance.IsMusicPlaying()
                && AudioManager.Instance.bgmSource != null 
                && AudioManager.Instance.bgmSource.volume > 0.05f;

            if (forceRestart || !isCurrentPlayingWithVolume)
            {
                AudioManager.Instance.PlayMusic(sceneBGM, loop, fadeDuration);
            }
        }

        public void ChangeSceneBGM(AudioClip newClip, float customFadeDuration = -1f)
        {
            if (newClip == null) return;
            sceneBGM = newClip;
            float duration = customFadeDuration >= 0f ? customFadeDuration : fadeDuration;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetDefaultSceneMusic(sceneBGM, defaultBossBGM);
                AudioManager.Instance.PlayMusic(sceneBGM, loop, duration);
            }
        }

        public void SetDefaultBossBGM(AudioClip bossClip)
        {
            defaultBossBGM = bossClip;
            if (defaultBossBGM != null)
            {
                defaultBossBGM.LoadAudioData();
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetDefaultSceneMusic(sceneBGM, defaultBossBGM);
                }
            }
        }

        public AudioClip GetSceneBGM() => sceneBGM;
        public AudioClip GetDefaultBossBGM() => defaultBossBGM;
    }
}
