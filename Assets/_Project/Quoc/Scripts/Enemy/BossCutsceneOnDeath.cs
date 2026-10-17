using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Quoc.Boss;
using Quoc.QuyVuong;
using Bao.Bosses;

namespace Quoc.Enemy
{
    /// <summary>
    /// Script đặt tại: Assets/_Project/Quoc/Scripts/Enemy/BossCutsceneOnDeath.cs
    /// Dùng để phát Cutscene Timeline khi tiêu diệt Boss (hỗ trợ QuyVuongBossHealth, BossHealth, RedSlimeBoss, LithosMonolithBoss, BossUIEventDispatcher).
    /// Khi Boss chết: bật Particle System → scale dần lên trong khoảng thời gian → phát Cutscene.
    ///
    /// LƯU Ý: Script này KHÔNG được gắn trên Boss GameObject (vì boss có thể bị Destroy/SetActive(false) khi chết
    /// → Coroutine bị kill → particle + cutscene không chạy). Hãy gắn lên một GameObject độc lập trong Scene.
    /// </summary>
    public class BossCutsceneOnDeath : MonoBehaviour
    {
        [Header("Death Effect Config")]
        [SerializeField, Tooltip("Prefab Particle System sẽ spawn ra khi Boss chết")]
        private ParticleSystem deathParticlePrefab;

        [SerializeField, Tooltip("Vị trí spawn Particle (kéo Transform vào). Nếu để trống sẽ spawn tại vị trí Boss")]
        private Transform particleSpawnPoint;

        [SerializeField, Tooltip("Thời gian scale dần lên trước khi chạy cutscene (giây)")]
        private float scaleDuration = 2f;

        [SerializeField, Tooltip("Tốc độ scale lên mỗi giây (ví dụ: 0.5 = mỗi giây tăng 0.5 đơn vị scale)")]
        private float scaleSpeed = 0.5f;

        [SerializeField, Tooltip("Thời gian (giây) sau khi spawn xong sẽ tắt và hủy Particle. 0 = không tự hủy")]
        private float particleLifetime = 5f;

        [Header("Cutscene Timeline Config")]
        [SerializeField, Tooltip("Kéo Cutscene Timeline (PlayableDirector) từ Hierarchy/Project vào đây")]
        private PlayableDirector cutsceneTimeline;

        [SerializeField, Tooltip("Dừng Timeline trước đó (ví dụ: Timeline Boss Battle) nếu đang chạy")]
        private PlayableDirector previousTimelineToStop;

        [SerializeField, Tooltip("Tự động kích hoạt GameObject của Timeline nếu đang bị Disable")]
        private bool autoEnableTimelineObject = true;

        [SerializeField, Tooltip("Chỉ phát Cutscene 1 lần duy nhất")]
        private bool oneShot = true;

        [Header("Boss Component References (Tự động tìm nếu để trống)")]
        [SerializeField, Tooltip("Tham chiếu tới QuyVuongBossHealth (Quỷ Vương Boss)")]
        private QuyVuongBossHealth quyVuongBossHealth;

        [SerializeField, Tooltip("Tham chiếu tới BossHealth (nếu dùng Boss hệ thống Quoc)")]
        private BossHealth bossHealth;

        [SerializeField, Tooltip("Tham chiếu tới RedSlimeBoss (nếu dùng Boss Red Slime Bao)")]
        private RedSlimeBoss redSlimeBoss;

        [SerializeField, Tooltip("Tham chiếu tới LithosMonolithBoss (nếu dùng Boss Lithos Bao)")]
        private LithosMonolithBoss lithosBoss;

        [SerializeField, Tooltip("Tham chiếu tới BossUIEventDispatcher (nếu dùng Generic Boss UI Bao)")]
        private BossUIEventDispatcher bossUIEventDispatcher;

        private bool isCutscenePlayed;
        private ParticleSystem spawnedParticle;

        private void Awake()
        {
            FindBossReferencesIfNull();
        }

        private void FindBossReferencesIfNull()
        {
            if (quyVuongBossHealth == null)
            {
                quyVuongBossHealth = GetComponent<QuyVuongBossHealth>();
                if (quyVuongBossHealth == null) quyVuongBossHealth = GetComponentInParent<QuyVuongBossHealth>();
                if (quyVuongBossHealth == null) quyVuongBossHealth = GetComponentInChildren<QuyVuongBossHealth>();
            }

            if (bossHealth == null)
            {
                bossHealth = GetComponent<BossHealth>();
                if (bossHealth == null) bossHealth = GetComponentInParent<BossHealth>();
                if (bossHealth == null) bossHealth = GetComponentInChildren<BossHealth>();
            }

            if (redSlimeBoss == null)
            {
                redSlimeBoss = GetComponent<RedSlimeBoss>();
                if (redSlimeBoss == null) redSlimeBoss = GetComponentInParent<RedSlimeBoss>();
                if (redSlimeBoss == null) redSlimeBoss = GetComponentInChildren<RedSlimeBoss>();
            }

            if (lithosBoss == null)
            {
                lithosBoss = GetComponent<LithosMonolithBoss>();
                if (lithosBoss == null) lithosBoss = GetComponentInParent<LithosMonolithBoss>();
                if (lithosBoss == null) lithosBoss = GetComponentInChildren<LithosMonolithBoss>();
            }

            if (bossUIEventDispatcher == null)
            {
                bossUIEventDispatcher = GetComponent<BossUIEventDispatcher>();
                if (bossUIEventDispatcher == null) bossUIEventDispatcher = GetComponentInParent<BossUIEventDispatcher>();
                if (bossUIEventDispatcher == null) bossUIEventDispatcher = GetComponentInChildren<BossUIEventDispatcher>();
            }
        }

        private void OnEnable()
        {
            if (quyVuongBossHealth != null)
            {
                quyVuongBossHealth.OnDied += OnBossDiedHandler;
            }

            if (bossHealth != null)
            {
                bossHealth.OnDied += OnBossDiedHandler;
            }

            if (redSlimeBoss != null)
            {
                redSlimeBoss.OnBossDied += OnRedSlimeDiedHandler;
            }

            if (lithosBoss != null)
            {
                lithosBoss.OnBossDied += OnBossDiedHandler;
            }

            if (bossUIEventDispatcher != null)
            {
                bossUIEventDispatcher.OnDied += OnBossDiedHandler;
            }
        }

        private void OnDisable()
        {
            if (quyVuongBossHealth != null)
            {
                quyVuongBossHealth.OnDied -= OnBossDiedHandler;
            }

            if (bossHealth != null)
            {
                bossHealth.OnDied -= OnBossDiedHandler;
            }

            if (redSlimeBoss != null)
            {
                redSlimeBoss.OnBossDied -= OnRedSlimeDiedHandler;
            }

            if (lithosBoss != null)
            {
                lithosBoss.OnBossDied -= OnBossDiedHandler;
            }

            if (bossUIEventDispatcher != null)
            {
                bossUIEventDispatcher.OnDied -= OnBossDiedHandler;
            }
        }

        private void OnBossDiedHandler()
        {
            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] === OnBossDiedHandler FIRED === trên {gameObject.name}", this);
            TriggerCutscene();
        }

        private void OnRedSlimeDiedHandler()
        {
            int aliveCount = 0;
            if (RedSlimeBoss.ActiveSlimes != null)
            {
                foreach (var slime in RedSlimeBoss.ActiveSlimes)
                {
                    if (slime != null && !slime.IsDead)
                    {
                        aliveCount++;
                    }
                }
            }

            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] OnRedSlimeDiedHandler | Slime còn sống: {aliveCount}", this);

            if (aliveCount <= 0)
            {
                TriggerCutscene();
            }
        }

        /// <summary>
        /// Kích hoạt Cutscene Timeline (có thể gọi từ UnityEvent hoặc Animation Event).
        /// Flow: Bật Particle → Scale dần lên trong scaleDuration → Phát Cutscene.
        /// </summary>
        public void TriggerCutscene()
        {
            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] TriggerCutscene() called | oneShot={oneShot} | isCutscenePlayed={isCutscenePlayed} | gameObject.activeInHierarchy={gameObject.activeInHierarchy}", this);

            if (oneShot && isCutscenePlayed) return;
            isCutscenePlayed = true;

            if (cutsceneTimeline == null)
            {
                Debug.LogWarning($"[{nameof(BossCutsceneOnDeath)}] Chưa gán Cutscene Timeline (PlayableDirector) trên GameObject: {gameObject.name}", this);
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError($"[{nameof(BossCutsceneOnDeath)}] GameObject '{gameObject.name}' đang bị INACTIVE → Coroutine sẽ không chạy! Hãy gắn script này lên một GameObject độc lập, không nằm trong Boss.", this);
                return;
            }

            StopPreviousTimelineIfPlaying();
            StartCoroutine(DeathEffectThenCutscene());
        }

        private void StopPreviousTimelineIfPlaying()
        {
            if (previousTimelineToStop != null && previousTimelineToStop.state == PlayState.Playing)
            {
                previousTimelineToStop.Stop();
            }
        }

        private IEnumerator DeathEffectThenCutscene()
        {
            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] DeathEffectThenCutscene START", this);

            // Spawn Particle từ prefab
            if (deathParticlePrefab != null)
            {
                Transform spawnAt = particleSpawnPoint != null ? particleSpawnPoint : transform;
                spawnedParticle = Instantiate(deathParticlePrefab, spawnAt.position, Quaternion.identity);
                spawnedParticle.Play();
                Debug.Log($"[{nameof(BossCutsceneOnDeath)}] Spawn Death Particle tại {spawnAt.position} | Scale hiện tại: {spawnedParticle.transform.localScale}", this);
            }
            else
            {
                Debug.LogWarning($"[{nameof(BossCutsceneOnDeath)}] Chưa gán Death Particle Prefab trên {gameObject.name}", this);
            }

            // Scale dần lên trong khoảng thời gian scaleDuration
            if (spawnedParticle != null && scaleDuration > 0f)
            {
                Transform particleTransform = spawnedParticle.transform;
                float elapsed = 0f;

                while (elapsed < scaleDuration)
                {
                    elapsed += Time.deltaTime;
                    float scaleIncrease = scaleSpeed * Time.deltaTime;
                    particleTransform.localScale += Vector3.one * scaleIncrease;
                    yield return null;
                }

                Debug.Log($"[{nameof(BossCutsceneOnDeath)}] Scale hoàn tất | Scale cuối: {spawnedParticle.transform.localScale}", this);
            }

            // Hết thời gian → phát cutscene
            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] Bắt đầu phát Cutscene...", this);
            PlayCutsceneNow();

            // Hẹn giờ tắt và hủy particle
            if (spawnedParticle != null && particleLifetime > 0f)
            {
                yield return new WaitForSeconds(particleLifetime);
                spawnedParticle.Stop();
                Destroy(spawnedParticle.gameObject);
                Debug.Log($"[{nameof(BossCutsceneOnDeath)}] Đã hủy Death Particle sau {particleLifetime}s", this);
            }
        }

        private void PlayCutsceneNow()
        {
            if (cutsceneTimeline == null) return;

            Debug.Log($"[{nameof(BossCutsceneOnDeath)}] PlayCutsceneNow → Timeline '{cutsceneTimeline.gameObject.name}'", this);

            if (autoEnableTimelineObject && !cutsceneTimeline.gameObject.activeSelf)
            {
                cutsceneTimeline.gameObject.SetActive(true);
            }

            cutsceneTimeline.time = 0;
            cutsceneTimeline.Stop();
            cutsceneTimeline.Play();
        }
    }
}
