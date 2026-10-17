using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Component quản lý toàn bộ âm thanh SFX và nhạc nền BGM cho Dreadworm Boss:
    /// - Nhạc nền: BGM Boss Battle hùng tráng.
    /// - Vòng đời & Trạng thái: Tiếng gầm khai chiến (Engage Roar), Nhận đòn (Hurt), Bộc phát Nộ Phase 2 (Phase Transition), Tử trận (Death), Phản đòn/Choáng (Parried Stagger).
    /// - Kỹ năng Cận chiến & Lao húc: Slam Windup/Impact, Heavy Quake Slam Impact, Charge Windup/Dash/Drift.
    /// - Kỹ năng Tấn công Đặc biệt: Projectile Charge/Fire/Hit, Energy Sphere Spawn/Sweep/Impact, Spine Charge Pulse/Erupt, Sanctuary Barrier Windup/Shield Up/Hit/Shatter, Aerial Drop Telegraph/Impact.
    /// </summary>
    [RequireComponent(typeof(DreadwormBoss))]
    public class DreadwormBossAudioController : MonoBehaviour
    {
        [Header("Boss Fight Music (BGM)")]
        [Tooltip("Clip nhạc nền riêng của Boss. Tự động phát khi Boss Engage và dừng/khôi phục khi Boss chết.")]
        [SerializeField] private AudioClip bossFightBGM;
        [SerializeField] private bool autoSyncWithDispatcher = true;

        [Header("Universal & Lifecycle Audio (Vòng Đời & Trạng Thái Chung)")]
        [Tooltip("Tiếng gầm mở màn dũng mãnh khi phát hiện Player.")]
        [SerializeField] private AudioClip engageRoarClip;
        [Tooltip("Âm thanh trúng đòn va chạm thịt da & giáp.")]
        [SerializeField] private AudioClip hurtClip;
        [Tooltip("Tiếng bộc phát sóng năng lượng xung kích cực đại khi chuyển sang Phase 2.")]
        [SerializeField] private AudioClip phaseTransitionClip;
        [Tooltip("Tiếng gầm hấp hối cùng tiếng đổ sụp rung chuyển đấu trường khi Boss chết.")]
        [SerializeField] private AudioClip deathClip;
        [Tooltip("Âm thanh kim loại va đập phản đòn (Parry Deflect) trong trẻo và làm Boss choáng dazed.")]
        [SerializeField] private AudioClip parriedStaggerClip;

        [Header("Melee & Charge Skills (Cận Chiến & Cơ Động)")]
        [Tooltip("Âm thanh gồng cuộn mình nén cơ thể trước khi đập đầu nện đất.")]
        [SerializeField] private AudioClip slamWindupClip;
        [Tooltip("Âm thanh đập đầu nện đất chấn động uy lực.")]
        [SerializeField] private AudioClip slamImpactClip;
        [Tooltip("Âm thanh đập nện địa chấn siêu nặng rung chuyển đấu trường (Heavy Slam Finisher).")]
        [SerializeField] private AudioClip heavySlamImpactClip;
        [Tooltip("Âm thanh gầm gừ cào đất dồn sức trước khi lao húc.")]
        [SerializeField] private AudioClip chargeWindupClip;
        [Tooltip("Âm thanh xé gió tốc độ cao khi lao húc.")]
        [SerializeField] private AudioClip chargeDashClip;
        [Tooltip("Âm thanh phanh rê ma sát bẻ lái đổi hướng (Drift Turn).")]
        [SerializeField] private AudioClip chargeDriftClip;

        [Header("Special Combat - Projectile Barrage (Mưa Phi Đạn)")]
        [Tooltip("Âm thanh tụ năng lượng chuẩn bị phóng phi đạn.")]
        [SerializeField] private AudioClip iceArrowChargeClip;
        [Tooltip("Âm thanh bắn vút phi đạn xuyên không khí.")]
        [SerializeField] private AudioClip iceArrowFireClip;
        [Tooltip("Âm thanh phi đạn va chạm nổ tung mục tiêu.")]
        [SerializeField] private AudioClip iceArrowHitClip;

        [Header("Special Combat - Energy Sphere (Cầu Năng Lượng Càn Quét)")]
        [Tooltip("Âm thanh kích hoạt sinh quả cầu năng lượng xoay tròn.")]
        [SerializeField] private AudioClip sphereSpawnClip;
        [Tooltip("Âm thanh sóng năng lượng rít liên tục khi cầu năng lượng quét qua chiến trường.")]
        [SerializeField] private AudioClip sphereSweepClip;
        [Tooltip("Âm thanh quả cầu năng lượng phát nổ xung kích.")]
        [SerializeField] private AudioClip sphereImpactClip;

        [Header("Special Combat - Spine Eruption (Sóng Gai Bộc Phát)")]
        [Tooltip("Âm thanh tụ xung năng lượng chạy dọc sống lưng.")]
        [SerializeField] private AudioClip spineChargePulseClip;
        [Tooltip("Âm thanh loạt gai nhọn bung nở bắn tỏa xung quanh.")]
        [SerializeField] private AudioClip spineEruptClip;

        [Header("Special Combat - Sanctuary Barrier (Khiên Hộ Thể)")]
        [Tooltip("Âm thanh tụ khí tạo trận địa bảo vệ.")]
        [SerializeField] private AudioClip sanctuaryWindupClip;
        [Tooltip("Âm thanh kích hoạt bừng sáng khiên chắn hộ thể.")]
        [SerializeField] private AudioClip sanctuaryShieldUpClip;
        [Tooltip("Âm thanh đòn đánh va vào khiên bảo vệ.")]
        [SerializeField] private AudioClip sanctuaryShieldHitClip;
        [Tooltip("Âm thanh khiên bảo vệ vỡ tan nát giải phóng năng lượng.")]
        [SerializeField] private AudioClip sanctuaryShieldBreakClip;

        [Header("Special Combat - Aerial Bombardment (Mưa Rơi Tinh Thể/Thiên Thạch)")]
        [Tooltip("Âm thanh cảnh báo vật thể rít rơi từ trên trời.")]
        [SerializeField] private AudioClip icicleTelegraphClip;
        [Tooltip("Âm thanh vật thể nện đất phát nổ rền vang.")]
        [SerializeField] private AudioClip icicleImpactClip;

        [Header("Audio Settings")]
        [Tooltip("Độ biến thiên cao độ nhẹ nhàng tự nhiên (tránh méo tiếng).")]
        [SerializeField] private float pitchVariance = 0.04f;
        [SerializeField] [Range(0f, 1f)] private float volumeScale = 1f;

        public AudioClip BossFightBGM => bossFightBGM;

        private DreadwormBoss dreadwormBoss;
        private BossUIEventDispatcher uiEventDispatcher;
        private DreadwormBossState previousState;
        private bool hasPlayedDeathSound;

        // AudioSource chuyên biệt cho các âm thanh tụ/sạc (Charge/Windup) để có thể ngắt ngay lập tức khi hết chiêu
        private AudioSource chargeAudioSource;

        public static DreadwormBossAudioController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            dreadwormBoss = GetComponent<DreadwormBoss>();
            uiEventDispatcher = GetComponent<BossUIEventDispatcher>();

            EnsureChargeAudioSource();
            SyncBGM();
        }

        private void Start()
        {
            SyncBGM();
        }

        private void EnsureChargeAudioSource()
        {
            if (chargeAudioSource == null)
            {
                chargeAudioSource = gameObject.AddComponent<AudioSource>();
                chargeAudioSource.playOnAwake = false;
                chargeAudioSource.loop = false;
                chargeAudioSource.spatialBlend = 0.5f;
            }
        }

        private void SyncBGM()
        {
            if (uiEventDispatcher == null) uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
            if (uiEventDispatcher == null) return;

            if (bossFightBGM != null)
            {
                uiEventDispatcher.SetBossBGM(bossFightBGM);
            }
            else if (uiEventDispatcher.BossBGM != null)
            {
                bossFightBGM = uiEventDispatcher.BossBGM;
            }
        }

        private void Update()
        {
            if (dreadwormBoss == null) return;

            // Xử lý âm thanh khi Boss chết
            if (dreadwormBoss.IsDead && !hasPlayedDeathSound)
            {
                hasPlayedDeathSound = true;
                StopChargeSound();
                PlayDeath();
                return;
            }

            // Fallback theo dõi chuyển state
            var currentState = dreadwormBoss.CurrentState;
            if (currentState != previousState)
            {
                HandleStateChange(currentState);
                previousState = currentState;
            }
        }

        private void HandleStateChange(DreadwormBossState newState)
        {
            if (newState == null) return;
            // Tự động ngắt âm thanh tụ lực cũ khi đổi State
            StopChargeSound();
        }

        public void StopChargeSound()
        {
            if (chargeAudioSource != null && chargeAudioSource.isPlaying)
            {
                chargeAudioSource.Stop();
                chargeAudioSource.clip = null;
            }
        }

        private void PlayChargeSoundInternal(AudioClip clip, Vector3? pos = null)
        {
            if (clip == null) return;
            EnsureChargeAudioSource();

            chargeAudioSource.Stop();
            chargeAudioSource.clip = clip;
            chargeAudioSource.transform.position = pos ?? transform.position;
            chargeAudioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            
            float sfxVol = (AudioManager.Instance != null) ? AudioManager.Instance.GetEffectiveSFXVolume() : 1f;
            chargeAudioSource.volume = sfxVol * volumeScale;
            chargeAudioSource.Play();
        }

        // ══════════════════════════════════════════════════
        //  PUBLIC PLAY METHODS (SUPPORT OPTIONAL POSITIONS)
        // ══════════════════════════════════════════════════

        // Lifecycle
        public void PlayEngageRoar(Vector3? pos = null) => PlaySound(engageRoarClip, pos ?? transform.position);
        public void PlayHurt(Vector3? pos = null) => PlaySound(hurtClip, pos ?? transform.position);
        public void PlayPhaseTransition(Vector3? pos = null) => PlaySound(phaseTransitionClip, pos ?? transform.position);
        public void PlayDeath(Vector3? pos = null) => PlaySound(deathClip, pos ?? transform.position);
        public void PlayParriedStagger(Vector3? pos = null) => PlaySound(parriedStaggerClip, pos ?? transform.position);

        // Slams
        public void PlaySlamWindup(Vector3? pos = null) => PlayChargeSoundInternal(slamWindupClip, pos);
        public void PlaySlamImpact(Vector3? pos = null) { StopChargeSound(); PlaySound(slamImpactClip, pos ?? transform.position); }
        public void PlayHeavySlamImpact(Vector3? pos = null) { StopChargeSound(); PlaySound(heavySlamImpactClip, pos ?? transform.position); }

        // Charges
        public void PlayChargeWindup(Vector3? pos = null) => PlayChargeSoundInternal(chargeWindupClip, pos);
        public void PlayChargeDash(Vector3? pos = null) => PlaySound(chargeDashClip, pos ?? transform.position);
        public void PlayChargeDrift(Vector3? pos = null) => PlaySound(chargeDriftClip, pos ?? transform.position);

        // Projectiles (Mưa Phi Đạn)
        public void PlayIceArrowCharge(Vector3? pos = null) => PlayChargeSoundInternal(iceArrowChargeClip, pos);
        public void PlayIceArrowFire(Vector3? pos = null) => PlaySound(iceArrowFireClip, pos ?? transform.position);
        public void PlayIceArrowHit(Vector3? pos = null) => PlaySound(iceArrowHitClip, pos ?? transform.position);

        // Spheres
        public void PlaySphereSpawn(Vector3? pos = null) => PlaySound(sphereSpawnClip, pos ?? transform.position);
        public void PlaySphereSweep(Vector3? pos = null) => PlaySound(sphereSweepClip, pos ?? transform.position);
        public void PlaySphereImpact(Vector3? pos = null) => PlaySound(sphereImpactClip, pos ?? transform.position);

        // Spines
        public void PlaySpineChargePulse(Vector3? pos = null) => PlaySound(spineChargePulseClip, pos ?? transform.position);
        public void PlaySpineErupt(Vector3? pos = null) => PlaySound(spineEruptClip, pos ?? transform.position);

        // Sanctuary & Aerial Drops
        public void PlaySanctuaryWindup(Vector3? pos = null) => PlayChargeSoundInternal(sanctuaryWindupClip, pos);
        public void PlaySanctuaryShieldUp(Vector3? pos = null) { StopChargeSound(); PlaySound(sanctuaryShieldUpClip, pos ?? transform.position); }
        public void PlaySanctuaryShieldHit(Vector3? pos = null) => PlaySound(sanctuaryShieldHitClip, pos ?? transform.position);
        public void PlaySanctuaryShieldBreak(Vector3? pos = null) => PlaySound(sanctuaryShieldBreakClip, pos ?? transform.position, 0.95f);
        public void PlayIcicleTelegraph(Vector3? pos = null) => PlaySound(icicleTelegraphClip, pos ?? transform.position);
        public void PlayIcicleImpact(Vector3? pos = null) => PlaySound(icicleImpactClip, pos ?? transform.position);

        private void PlaySound(AudioClip clip, Vector3 position, float basePitch = 1f)
        {
            if (clip == null) return;

            float pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
            if (AudioManager.Instance != null)
            {
                // Phát qua kênh SFX của AudioManager
                AudioManager.Instance.PlaySFX(clip, position, pitch);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, position, volumeScale);
            }
        }
    }
}
