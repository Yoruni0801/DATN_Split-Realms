using UnityEngine;
using System.Collections;

namespace Bao.Bosses
{
    /// <summary>
    /// Component quản lý toàn bộ âm thanh SFX và nhạc nền BGM cho Boss Red Slime:
    /// - Form A (Đỏ - Lớn): Melee, Nhảy nện đất (Slam Jump / Dive Impact), Sóng gai trồi lên (Spike Wave).
    /// - Form B (Xanh - Vừa): Lướt săn mồi (Hunter Dash), Bắn đạn thạch 3 hướng (Tri-Burst Spit), Gọng Kìm Sấm Sét Song Sát (Dual Cross-Dash & Clash & Parry).
    /// - Form C (Vàng - Nhỏ): Bật nảy đa điểm siêu tốc (Pinball Popcorn Bounce), Trận đồ Laser Lõi Cube (Prismatic Laser Matrix).
    /// - Cơ chế chung: Phân tách (Split Pop), Hợp thể (Merge Gather & Boom), Nhận đòn (Hurt), Tử trận (Death), Nhạc nền Boss BGM.
    /// </summary>
    [RequireComponent(typeof(RedSlimeBoss))]
    public class RedSlimeBossAudioController : MonoBehaviour
    {
        [Header("Boss Fight Music (BGM)")]
        [Tooltip("Clip nhạc nền riêng của Red Slime Boss. Tự động phát khi Boss Engage (hiện thanh máu) và dừng/khôi phục khi Boss chết.")]
        [SerializeField] private AudioClip bossFightBGM;
        [SerializeField] private bool autoSyncWithDispatcher = true;

        [Header("Universal & Life Cycle SFX (Âm Thanh Chung & Vòng Đời)")]
        [Tooltip("Âm thanh khi Slime bị dính sát thương / nhũn đàn hồi.")]
        [SerializeField] private AudioClip hurtClip;
        [Tooltip("Âm thanh khi Slime chết / nổ tan biến.")]
        [SerializeField] private AudioClip deathClip;
        [Tooltip("Âm thanh nổ bung tách cơ thể (Split Pop).")]
        [SerializeField] private AudioClip splitPopClip;
        [Tooltip("Âm thanh các Slime con di chuyển lao vào nhau chuẩn bị gộp (Merge Gather).")]
        [SerializeField] private AudioClip mergeGatherClip;
        [Tooltip("Âm thanh nổ gộp hợp thể thành công thành Slime cấp cao hơn (Merge Impact).")]
        [SerializeField] private AudioClip mergeImpactClip;

        [Header("Form A - Red Slime Skills (Kỹ Năng Slime Đỏ - Lớn)")]
        [Tooltip("Âm thanh vung tay cào/tát cận chiến.")]
        [SerializeField] private AudioClip meleeClip;
        [Tooltip("Âm thanh va chạm khi đòn cào đánh trúng Player.")]
        [SerializeField] private AudioClip meleeImpactClip;
        [Tooltip("Âm thanh gồng tụ lực & bật nhảy vút lên cao (Slam Windup & Jump).")]
        [SerializeField] private AudioClip slamJumpClip;
        [Tooltip("Âm thanh lao nện thẳng xuống đất cực mạnh gây rung chấn (Dive Slam Impact).")]
        [SerializeField] private AudioClip slamImpactClip;
        [Tooltip("Âm thanh gai nhọn đâm xuyên từ dưới lòng đất trồi lên (Spike Eruption).")]
        [SerializeField] private AudioClip spikeEruptClip;

        [Header("Form B - Blue Slime Skills (Kỹ Năng Slime Xanh - Vừa)")]
        [Tooltip("Âm thanh co nén tụ lực chuẩn bị phi lướt (Hunter Dash Windup).")]
        [SerializeField] private AudioClip dashBWindupClip;
        [Tooltip("Âm thanh phi lướt xé gió tốc độ cao như viên đạn (Slime Bullet Dash Launch).")]
        [SerializeField] private AudioClip dashBLaunchClip;
        [Tooltip("Âm thanh chém trúng Player khi lướt.")]
        [SerializeField] private AudioClip dashBImpactClip;
        [Tooltip("Âm thanh gồng tụ năng lượng ở miệng trước khi bắn (Tri-Burst Windup).")]
        [SerializeField] private AudioClip shootBWindupClip;
        [Tooltip("Âm thanh nhả phun 3 viên đạn thạch hổ phách (Tri-Burst Spit Fire).")]
        [SerializeField] private AudioClip shootBFireClip;
        [Tooltip("Âm thanh đạn thạch hổ phách va chạm nổ tung khi trúng đích (Element Impact).")]
        [SerializeField] private AudioClip shootBImpactClip;
        [Tooltip("Âm thanh chiếu tia laser cảnh báo giao cắt chữ X (Dual Cross-Dash Telegraph).")]
        [SerializeField] private AudioClip dualCrossDashWarningClip;
        [Tooltip("Âm thanh 2 Slime cùng phóng siêu thanh cắt xéo (Dual Cross-Dash Launch).")]
        [SerializeField] private AudioClip dualCrossDashLaunchClip;
        [Tooltip("Âm thanh phát nổ sóng xung kích Guillotine cực đại tại điểm giao cắt (Center Clash).")]
        [SerializeField] private AudioClip dualCrossDashClashClip;
        [Tooltip("Âm thanh khi bị Player thực hiện cú Parry hoàn hảo làm choáng (Parried Stun).")]
        [SerializeField] private AudioClip dualCrossDashParriedClip;

        [Header("Form C - Yellow Slime Skills (Kỹ Năng Slime Vàng - Nhỏ)")]
        [Tooltip("Âm thanh co nén lò xo năng lượng (Pinball Windup Charge).")]
        [SerializeField] private AudioClip pinballChargeClip;
        [Tooltip("Âm thanh bật nảy tưng tưng liên hoàn parabol tốc độ cao (Pinball Bounce).")]
        [SerializeField] private AudioClip pinballBounceClip;
        [Tooltip("Âm thanh tiếp đất nảy đàn hồi sau chuỗi bật nảy (Pinball Land).")]
        [SerializeField] private AudioClip pinballLandClip;
        [Tooltip("Âm thanh khóa vị trí & quá tải năng lượng lõi Cube (Laser Matrix Charge).")]
        [SerializeField] private AudioClip laserChargeClip;
        [Tooltip("Âm thanh tia laser ma trận năng lượng quét liên tục nối giữa 2 Slime (Laser Loop).")]
        [SerializeField] private AudioClip laserLoopClip;
        [Tooltip("Âm thanh tia laser bộc phát nổ kết thúc (Laser Burst End).")]
        [SerializeField] private AudioClip laserBurstClip;

        [Header("Audio Settings")]
        [Tooltip("Độ biến thiên cao độ ngẫu nhiên giúp âm thanh sống động, tự nhiên.")]
        [SerializeField] private float pitchVariance = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float volumeScale = 1f;

        public AudioClip BossFightBGM => bossFightBGM;

        private RedSlimeBoss redSlimeBoss;
        private BossUIEventDispatcher uiEventDispatcher;
        private RedSlimeBossState previousState;
        private bool hasPlayedDeathSound;

        private void Awake()
        {
            redSlimeBoss = GetComponent<RedSlimeBoss>();
            uiEventDispatcher = GetComponent<BossUIEventDispatcher>();

            SyncBGM();
        }

        private void Start()
        {
            SyncBGM();
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
            if (redSlimeBoss == null) return;

            // Xử lý âm thanh khi Boss chết
            if (redSlimeBoss.IsDead && !hasPlayedDeathSound)
            {
                hasPlayedDeathSound = true;
                PlayDeathSound();
                return;
            }

            // Theo dõi chuyển đổi State để hỗ trợ fallback trigger nếu cần
            var currentState = redSlimeBoss.CurrentState;
            if (currentState != previousState)
            {
                HandleStateChangeFallback(currentState);
                previousState = currentState;
            }
        }

        private void HandleStateChangeFallback(RedSlimeBossState newState)
        {
            if (newState == null) return;
            // Các State cụ thể đã chủ động gọi các hàm Play chi tiết theo từng timeline / event
        }

        // ══════════════════════════════════════════════════
        //  PUBLIC PLAY METHODS FOR SKILLS & MECHANICS
        // ══════════════════════════════════════════════════

        // --- Common & Life Cycle ---
        public void PlayHurtSound() => PlaySound(hurtClip);
        public void PlayDeathSound() => PlaySound(deathClip);
        public void PlaySplitSound() => PlaySound(splitPopClip);
        public void PlayMergeGatherSound() => PlaySound(mergeGatherClip);
        public void PlayMergeImpactSound() => PlaySound(mergeImpactClip != null ? mergeImpactClip : splitPopClip);

        // --- Form A ---
        public void PlayMeleeSound() => PlaySound(meleeClip);
        public void PlayMeleeImpactSound() => PlaySound(meleeImpactClip);
        public void PlaySlamJumpSound() => PlaySound(slamJumpClip);
        public void PlaySlamImpactSound() => PlaySound(slamImpactClip);
        public void PlaySpikeEruptSound(Vector3? position = null) => PlaySound(spikeEruptClip, position);

        // --- Form B ---
        public void PlayDashBWindupSound() => PlaySound(dashBWindupClip);
        public void PlayDashBLaunchSound() => PlaySound(dashBLaunchClip);
        public void PlayDashBImpactSound(Vector3? position = null) => PlaySound(dashBImpactClip, position);
        public void PlayShootBWindupSound() => PlaySound(shootBWindupClip);
        public void PlayShootBFireSound() => PlaySound(shootBFireClip);
        public void PlayShootBImpactSound(Vector3? position = null) => PlaySound(shootBImpactClip, position);
        public void PlayDualWarningSound() => PlaySound(dualCrossDashWarningClip);
        public void PlayDualLaunchSound() => PlaySound(dualCrossDashLaunchClip);
        public void PlayDualClashSound(Vector3? position = null) => PlaySound(dualCrossDashClashClip, position);
        public void PlayDualParriedSound() => PlaySound(dualCrossDashParriedClip);

        // --- Form C ---
        public void PlayPinballChargeSound() => PlaySound(pinballChargeClip);
        public void PlayPinballBounceSound(Vector3? position = null) => PlaySound(pinballBounceClip, position);
        public void PlayPinballLandSound(Vector3? position = null) => PlaySound(pinballLandClip, position);
        public void PlayLaserChargeSound() => PlaySound(laserChargeClip);
        public void PlayLaserLoopSound() => PlaySound(laserLoopClip);
        public void PlayLaserBurstSound(Vector3? position = null) => PlaySound(laserBurstClip, position);

        // ══════════════════════════════════════════════════
        //  CORE SOUND PLAYBACK
        // ══════════════════════════════════════════════════

        public void PlaySound(AudioClip clip, Vector3? customPos = null)
        {
            if (clip == null) return;

            Vector3 playPos = customPos.HasValue ? customPos.Value : transform.position;

            if (AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                // Hệ thống AudioManager có Object Pooling sfxPool tối ưu hiệu năng
                AudioManager.Instance.PlaySFX(clip, playPos, pitch);
            }
        }
    }
}
