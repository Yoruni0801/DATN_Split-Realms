using UnityEngine;
using System;

namespace Bao.Bosses
{
    /// <summary>
    /// Component này được gắn vào con Boss. Nó đóng vai trò là cầu nối phát ra các sự kiện 
    /// (mất máu, bắt đầu đánh, chết...) để các hệ thống khác (như UI) lắng nghe mà không cần 
    /// biết cụ thể đây là con boss nào.
    /// </summary>
    public class BossUIEventDispatcher : MonoBehaviour
    {
        [Header("Dynamic UI Spawner")]
        [Tooltip("Kéo Prefab UI của con Boss này vào đây. Khi Boss xuất hiện, UI này sẽ tự động sinh ra.")]
        [SerializeField] private GameObject uiPrefab;

        [Header("Boss Background Music (BGM)")]
        [Tooltip("Clip nhạc nền riêng của Boss. Khi Boss xuất hiện / hiện thanh máu, bài nhạc này sẽ được phát thay cho nhạc Map ban đầu.")]
        [SerializeField] private AudioClip bossBGM;

        [Tooltip("Tự động chuyển sang nhạc Boss khi Boss phát hiện người chơi / hiện thanh máu.")]
        [SerializeField] private bool changeBgmOnEngage = true;

        [Tooltip("Tự động khôi phục lại nhạc nền Scene ban đầu sau khi Boss chết.")]
        [SerializeField] private bool restoreSceneBgmOnDeath = true;

        [Tooltip("Thời gian fade chuyển đổi âm thanh (giây).")]
        [SerializeField] [Range(0f, 5f)] private float bgmFadeDuration = 1.2f;

        [Header("Multi-Layer Health Bar Settings")]
        [Tooltip("Bật/Tắt chế độ hiển thị máu nhiều tầng (x4, x3, x2, x1). Tích chọn cho con Boss bạn muốn có thanh máu x, bỏ tích nếu muốn thanh máu 1 thanh thông thường.")]
        [SerializeField] private bool useMultiLayerHealthBar = true;

        [Tooltip("Số lượng tầng máu khi bật chế độ Multi-Layer (Mặc định: 4).")]
        [SerializeField, Range(1, 10)] private int multiLayerCount = 4;

        public bool UseMultiLayerHealthBar => useMultiLayerHealthBar;
        public int MultiLayerCount => multiLayerCount;

        public string BossName { get; private set; }
        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public bool IsEngaged { get; private set; }

        public AudioClip BossBGM => bossBGM;
        public float BgmFadeDuration => bgmFadeDuration;
        public bool RestoreSceneBgmOnDeath => restoreSceneBgmOnDeath;

        public event Action<string, int> OnInitialized;
        public event Action<int, int> OnHealthChanged;
        public event Action OnEngaged;
        public event Action OnDied;
        public event Action<int> OnPhaseChanged;

        private void Awake()
        {
            TryResolveBossBGM();
        }

        private void Start()
        {
            TryResolveBossBGM();
        }

        /// <summary>
        /// Tự động tìm kiếm AudioClip nhạc Boss nếu trường bossBGM đang bị trống và Preload trước vào RAM.
        /// </summary>
        public void TryResolveBossBGM()
        {
            if (bossBGM == null)
            {
                // 1. Thử tìm trên NidusBossAudioController
                NidusBossAudioController nidusAudio = GetComponent<NidusBossAudioController>();
                if (nidusAudio != null && nidusAudio.BossFightBGM != null)
                {
                    bossBGM = nidusAudio.BossFightBGM;
                }

                // 2. Thử tìm trên DreadwormBossAudioController
                if (bossBGM == null)
                {
                    DreadwormBossAudioController dreadwormAudio = GetComponent<DreadwormBossAudioController>();
                    if (dreadwormAudio != null && dreadwormAudio.BossFightBGM != null)
                    {
                        bossBGM = dreadwormAudio.BossFightBGM;
                    }
                }

                // 3. Thử tìm trên RedSlimeBossAudioController
                if (bossBGM == null)
                {
                    RedSlimeBossAudioController redSlimeAudio = GetComponent<RedSlimeBossAudioController>();
                    if (redSlimeAudio != null && redSlimeAudio.BossFightBGM != null)
                    {
                        bossBGM = redSlimeAudio.BossFightBGM;
                    }
                }

                // 4. Thử tìm từ SceneBGMController của Map hiện tại
                if (bossBGM == null && Duy.Audio.SceneBGMController.Instance != null && Duy.Audio.SceneBGMController.Instance.GetDefaultBossBGM() != null)
                {
                    bossBGM = Duy.Audio.SceneBGMController.Instance.GetDefaultBossBGM();
                }

                // 5. Thử tìm từ cấu hình SceneAudioProfile trong AudioManager
                if (bossBGM == null && AudioManager.Instance != null)
                {
                    bossBGM = AudioManager.Instance.GetDefaultBossBGMForCurrentScene();
                }
            }

            // Nạp trước dữ liệu âm thanh vào RAM để loại bỏ hoàn toàn giật lag khi vào trận
            if (bossBGM != null && bossBGM.loadState == AudioDataLoadState.Unloaded)
            {
                bossBGM.LoadAudioData();
            }
        }

        /// <summary>
        /// Cho phép thiết lập hoặc thay đổi Boss BGM từ code.
        /// </summary>
        public void SetBossBGM(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip != null)
            {
                bossBGM = clip;
                if (bossBGM.loadState == AudioDataLoadState.Unloaded)
                {
                    bossBGM.LoadAudioData();
                }
            }
            if (fadeDuration >= 0f) bgmFadeDuration = fadeDuration;

            // Nếu đang trong trận đánh, cập nhật đổi nhạc ngay
            if (IsEngaged && bossBGM != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossMusic(bossBGM, true, bgmFadeDuration);
            }
        }

        /// <summary>
        /// Gán Prefab UI thủ công qua code nếu cần.
        /// </summary>
        public void SetUIPrefab(GameObject prefab)
        {
            uiPrefab = prefab;
        }

        /// <summary>
        /// Cấu hình chế độ thanh máu đa tầng x4 từ code hoặc Boss script.
        /// </summary>
        public void ConfigureMultiLayer(bool useMultiLayer, int layerCount = 4)
        {
            useMultiLayerHealthBar = useMultiLayer;
            multiLayerCount = Mathf.Max(1, layerCount);
        }

        /// <summary>
        /// Gọi hàm này khi Boss khởi tạo (thường ở Start hoặc Awake).
        /// </summary>
        public void Initialize(string bossName, int maxHealth)
        {
            BossName = bossName;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;

            TryResolveBossBGM();

            // Tự động sinh ra UI Prefab nếu có cài đặt
            if (uiPrefab != null)
            {
                Canvas canvas = uiPrefab.GetComponent<Canvas>();
                GameObject spawnedUI = (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                    ? Instantiate(uiPrefab)
                    : Instantiate(uiPrefab, transform);

                GenericBossUI uiScript = spawnedUI.GetComponentInChildren<GenericBossUI>();
                if (uiScript != null)
                {
                    uiScript.SetBossEvents(this);
                }
            }

            OnInitialized?.Invoke(bossName, maxHealth);
        }

        /// <summary>
        /// Gọi hàm này khi Boss bắt đầu vào trận (phát hiện người chơi).
        /// </summary>
        public void Engage()
        {
            if (IsEngaged) return;
            IsEngaged = true;

            TryResolveBossBGM();

            // Tự động chuyển sang BGM trận đấu của Boss
            if (changeBgmOnEngage && bossBGM != null && AudioManager.Instance != null)
            {
                Debug.Log($"[BossUIEventDispatcher] Boss '{BossName}' Engage -> Phát Boss BGM: '{bossBGM.name}'");
                AudioManager.Instance.PlayBossMusic(bossBGM, true, bgmFadeDuration);
            }
            else if (bossBGM == null)
            {
                Debug.LogWarning($"[BossUIEventDispatcher] Boss '{BossName}' Engage nhưng chưa được gán Boss BGM! Vui lòng gán AudioClip vào ô Boss BGM trên BossUIEventDispatcher hoặc BossAudioController.");
            }

            OnEngaged?.Invoke();
        }

        /// <summary>
        /// Gọi hàm này mỗi khi Boss bị nhận sát thương hoặc hồi máu.
        /// </summary>
        public void UpdateHealth(int currentHealth)
        {
            CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Gọi hàm này khi cả CurrentHealth và MaxHealth thay đổi (ví dụ khi chia/gộp máu Swarm).
        /// </summary>
        public void UpdateHealth(int currentHealth, int maxHealth)
        {
            MaxHealth = Mathf.Max(1, maxHealth);
            CurrentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Gọi hàm này khi Boss chết.
        /// </summary>
        public void Die()
        {
            if (CurrentHealth > 0)
            {
                UpdateHealth(0);
            }
            IsEngaged = false;

            // Dừng nhạc Boss và khôi phục lại nhạc Scene ban đầu
            if (bossBGM != null && AudioManager.Instance != null)
            {
                Debug.Log($"[BossUIEventDispatcher] Boss '{BossName}' Died -> Khôi phục nhạc Scene.");
                AudioManager.Instance.StopBossMusic(restoreSceneBgmOnDeath, bgmFadeDuration);
            }

            OnDied?.Invoke();
        }

        /// <summary>
        /// Gọi hàm này khi Boss chuyển phase.
        /// </summary>
        public void ChangePhase(int phaseIndex)
        {
            OnPhaseChanged?.Invoke(phaseIndex);
        }
    }
}
