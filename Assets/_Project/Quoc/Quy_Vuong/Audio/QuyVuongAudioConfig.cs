using System;
using UnityEngine;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Chứa thông tin cấu hình và thư viện âm thanh (Audio Clips) cho Toàn Bộ 5 Dạng của Boss Quỷ Vương:
    /// - Phase 0: Quỷ Vương Cổ Đại / Thủy Tinh (MamaTick & Water Beam)
    /// - Phase 1: Quỷ Vương Cung Thủ & Bão Tên Nước (Rain Arrow & Grapple Cage)
    /// - Phase 2: Quỷ Vương Tử Thần Liềm Đoạt Mệnh & Phi Đạn Đuổi (Scythe Slash & Homing Projectiles)
    /// - Phase 3: Quỷ Vương Địa Chấn & Bổ Giáo Thiên Thạch (Ground Line & Meteor Slam)
    /// - Phase 4: Quỷ Vương Hỏa Nham Ma / Trùm Cuối Tối Thượng (Skull Dash, Uppercut & Obsidian Bomb)
    /// </summary>
    [Serializable]
    public class BossLifecycleAudioData
    {
        [Header("Engage & Roar (Khai Chiến Hùng Tráng)")]
        [Tooltip("Tiếng gầm uy lực khai chiến khi Boss bắt đầu xuất hiện")]
        public AudioClip engageRoarClip;

        [Tooltip("Âm thanh tiếng chuông / trống trận bùng nổ (Stinger Impact) mở màn trận chiến")]
        public AudioClip engageStingerClip;

        [Header("Hurt & Stagger")]
        [Tooltip("Âm thanh trúng đòn va chạm thịt da & giáp")]
        public AudioClip hurtClip;

        [Tooltip("Âm thanh khi bị phản đòn / choáng")]
        public AudioClip parriedStaggerClip;

        [Header("Phase Transition & Rage Awakening (Bộc Phát Nộ Khí)")]
        [Tooltip("Tiếng bộc phát năng lượng / gầm cuồng nộ khi chuyển Phase")]
        public AudioClip phaseTransitionClip;

        [Tooltip("Tiếng sóng xung kích ma thuật rền vang toàn đấu trường khi chuyển Phase")]
        public AudioClip phaseTransitionShockwaveClip;

        [Header("Death (Tử Trận Bi Tráng)")]
        [Tooltip("Tiếng thét tử trận và sụp đổ tiêu tan của Boss")]
        public AudioClip deathClip;

        [Tooltip("Tiếng nổ giải phóng ma lực cuối cùng khi Boss sụp đổ")]
        public AudioClip deathExplosionClip;

        [Header("Movement")]
        [Tooltip("Âm thanh bước chân nặng nề rung chuyển mặt đất")]
        public AudioClip footstepClip;

        [Tooltip("Âm thanh vỗ cánh / lướt gió tốc độ cao")]
        public AudioClip wingFlapClip;
    }

    [Serializable]
    public class EpicFinalBossAudioData
    {
        [Header("Sub-Bass & Heavy Impact (Rung Chấn Trầm Uy Lực)")]
        [Tooltip("Âm thanh Sub-Bass siêu trầm tạo độ nặng rung chuyển màn hình khi tung tuyệt chiêu")]
        public AudioClip subBassImpactClip;

        [Tooltip("Âm thanh tiếng rít ma thuật siêu nhiên dâng cao (Epic Magic Riser)")]
        public AudioClip magicRiserClip;

        [Header("Low-HP Tension & Heartbeat (Căng Thẳng Nguy Kịch)")]
        [Tooltip("Tiếng nhịp tim đập hồi hộp dồn dập khi HP Boss ở mức nguy kịch (< 30%)")]
        public AudioClip heartbeatTensionClip;

        [Tooltip("Ngưỡng máu kích hoạt nhịp tim căng thẳng (0.3 = 30% HP)")]
        [Range(0.1f, 0.5f)] public float lowHpThreshold = 0.3f;

        [Header("Arena Reverb & Spatial Acoustics")]
        [Tooltip("Kích hoạt hiệu ứng vang đấu trường trùm cuối (Reverb Filter)")]
        public bool enableArenaReverb = true;

        [Tooltip("Độ vang của đấu trường")]
        [Range(0f, 1f)] public float reverbIntensity = 0.4f;
    }

    [Serializable]
    public class Phase0AudioData
    {
        [Header("Water Rain Attack (Mưa Thủy Tiễn Cổ Đại)")]
        [Tooltip("Âm thanh cảnh báo tụ nước")]
        public AudioClip waterRainTelegraphClip;

        [Tooltip("Âm thanh bắn loạt tên nước")]
        public AudioClip waterRainShootClip;

        [Tooltip("Âm thanh mũi tên nước cắm xuống phát nổ bọt nước")]
        public AudioClip waterRainImpactClip;

        [Header("Water Beam Burst (Cột Nước Khổng Lồ Phun Trào)")]
        [Tooltip("Âm thanh tụ năng lượng cột nước dưới đất")]
        public AudioClip waterBeamChargeClip;

        [Tooltip("Âm thanh cột nước khổng lồ bắn xối xả")]
        public AudioClip waterBeamBurstClip;

        [Tooltip("Âm thanh tiếng rít dòng nước xoáy liên tục")]
        public AudioClip waterBeamLoopClip;

        [Header("Horizontal Water Attack (Bắn Tên Nước Ngang)")]
        [Tooltip("Âm thanh bắn tên nước bay ngang")]
        public AudioClip horizontalWaterArrowFireClip;

        [Tooltip("Âm thanh tên nước ngang va chạm phát nổ")]
        public AudioClip horizontalWaterArrowImpactClip;

        [Header("Teleport & Relocate")]
        [Tooltip("Âm thanh tốc biến bay lên không trung")]
        public AudioClip skyTeleportClip;

        [Tooltip("Âm thanh đáp xuống đấu trường")]
        public AudioClip arenaLandingClip;
    }

    [Serializable]
    public class Phase1AudioData
    {
        [Header("Rain Arrow (Mưa Tên Hủy Diệt)")]
        [Tooltip("Âm thanh cảnh báo / tụ vòng xoáy nước mưa tên")]
        public AudioClip rainArrowTelegraphClip;

        [Tooltip("Âm thanh bắn mũi tên mưa lên trời")]
        public AudioClip rainArrowShootClip;

        [Tooltip("Âm thanh mưa tên rít xé gió rơi xuống")]
        public AudioClip rainArrowFallingLoopClip;

        [Tooltip("Âm thanh mũi tên mưa nổ cắm xuống mặt đất")]
        public AudioClip rainArrowImpactClip;

        [Header("Horizontal Arrow (Tên Ngang Xuyên Thấu)")]
        [Tooltip("Âm thanh gồng tụ năng lượng bắn tên ngang")]
        public AudioClip horizontalArrowChargeClip;

        [Tooltip("Âm thanh phóng tên ngang xuyên không")]
        public AudioClip horizontalArrowFireClip;

        [Tooltip("Âm thanh mũi tên ngang va chạm phát nổ")]
        public AudioClip horizontalArrowImpactClip;

        [Header("Grapple Arrow (Tên Dây Kéo & Lồng Nước)")]
        [Tooltip("Âm thanh xuất hiện cung nước và bắn dây móc")]
        public AudioClip grappleBowCastClip;

        [Tooltip("Âm thanh phóng mũi tên móc")]
        public AudioClip grappleArrowShootClip;

        [Tooltip("Âm thanh dây nước siết kéo giật Boss hoặc Player")]
        public AudioClip grapplePullClip;

        [Tooltip("Âm thanh lồng nước bung ra giam giữ Player")]
        public AudioClip cageTrapSpawnClip;

        [Header("Ram / Charge Attack (Lao Húc Cực Tốc)")]
        [Tooltip("Âm thanh gầm dồn lực cào đất chuẩn bị lao húc")]
        public AudioClip ramWindupClip;

        [Tooltip("Âm thanh xé gió tốc độ cực cao khi lao húc")]
        public AudioClip ramDashRushClip;

        [Tooltip("Âm thanh nện va chạm đối thủ hoặc va vào tường")]
        public AudioClip ramImpactClip;

        [Header("Enemy Spawn (Triệu Hồi Quái Cấm Địa)")]
        [Tooltip("Âm thanh niệm chú mở cổng ma thuật")]
        public AudioClip enemySpawnChantClip;

        [Tooltip("Âm thanh quái xuất hiện từ vòng xoáy")]
        public AudioClip enemySpawnBurstClip;

        [Header("Fly Movement (Bay Lượn)")]
        [Tooltip("Âm thanh đạp chân cất cánh bay")]
        public AudioClip flyTakeOffClip;

        [Tooltip("Âm thanh vệt khí rít theo khi bay")]
        public AudioClip flyTrailClip;

        [Tooltip("Âm thanh đáp đất chấn động")]
        public AudioClip flyLandingClip;
    }

    [Serializable]
    public class Phase2AudioData
    {
        [Header("Scythe Slash (Đòn Chém Liềm Không Gian)")]
        [Tooltip("Âm thanh gồng vung lưỡi liềm tụ ma khí")]
        public AudioClip scytheWindupClip;

        [Tooltip("Âm thanh vung lưỡi liềm xé rách không gian")]
        public AudioClip scytheHeavySwingClip;

        [Tooltip("Âm thanh sóng kiếm liềm càn quét toàn map")]
        public AudioClip scytheWaveSweepClip;

        [Tooltip("Âm thanh đòn chém liềm chém trúng mục tiêu")]
        public AudioClip scytheHitClip;

        [Header("Homing Projectiles (Phi Đạn Dẫn Đường Ma Thuật)")]
        [Tooltip("Âm thanh triệu hồi vòng đạn ma pháp quanh thân")]
        public AudioClip homingSummonChargeClip;

        [Tooltip("Âm thanh bắn phóng loạt đạn ma pháp")]
        public AudioClip homingMultiFireClip;

        [Tooltip("Âm thanh đạn rít đuổi theo mục tiêu")]
        public AudioClip homingFlightLoopClip;

        [Tooltip("Âm thanh phi đạn phát nổ xung kích")]
        public AudioClip homingExplosionClip;

        [Header("Aggressive Mode (Chuyển Dạng Cuồng Nộ)")]
        [Tooltip("Tiếng gầm bộc phát cuồng nộ khi HP Boss dưới 50%")]
        public AudioClip rageRoarClip;

        [Header("Relocate / Teleport Dash")]
        [Tooltip("Âm thanh tốc biến di chuyển quanh arena")]
        public AudioClip relocateTeleportClip;
    }

    [Serializable]
    public class Phase3AudioData
    {
        [Header("Ground Line & Earth Eruption (Rạch Đất & Trồi Gai Địa Ngục)")]
        [Tooltip("Âm thanh nện chân dậm đất kích hoạt chấn địa")]
        public AudioClip earthStompClip;

        [Tooltip("Âm thanh đường nứt đất rạch dài kêu rền vang liên hồi")]
        public AudioClip groundLineCrackRumbleClip;

        [Tooltip("Âm thanh gai đá phun trào / triệu hồi Dusthorn trồi lên")]
        public AudioClip earthEruptionDusthornClip;

        [Header("Sky Leap & Spear Meteor Slam (Nhảy Không Trung & Bổ Giáo Thiên Thạch)")]
        [Tooltip("Âm thanh dậm nhảy siêu tốc vút lên bầu trời")]
        public AudioClip skyLeapTakeoffClip;

        [Tooltip("Âm thanh lao bổ từ trên không xuống như thiên thạch xé rách mây")]
        public AudioClip skyLeapDescentSwooshClip;

        [Tooltip("Âm thanh cắm ngọn giáo nện đất hủy diệt cực đại")]
        public AudioClip skyLeapHeavyMeteorImpactClip;

        [Tooltip("Âm thanh sóng chấn động lan tỏa sau cú nện giáo")]
        public AudioClip shockwaveRumbleClip;
    }

    [Serializable]
    public class Phase4AudioData
    {
        [Header("Tectonic Rending - Molten Skull Dash (Hóa Đầu Lâu Lửa Húc Cực Mạnh)")]
        [Tooltip("Âm thanh biến thân Đầu Lâu Hỏa Ma gầm thét")]
        public AudioClip skullTransformClip;

        [Tooltip("Âm thanh lướt húc ma lửa tốc độ cao xé gió")]
        public AudioClip skullDashRushClip;

        [Tooltip("Âm thanh Đầu Lâu Lửa nện va chạm phát nổ nung chảy")]
        public AudioClip skullDashImpactClip;

        [Header("Tectonic Rending - Eruption Uppercut (Vuốt Móc Lửa & Lốc Xoáy Dung Nham)")]
        [Tooltip("Âm thanh gồng vuốt móc lửa đánh bật lên")]
        public AudioClip eruptionUppercutClip;

        [Tooltip("Âm thanh lốc xoáy dung nham gầm rú bốc lên")]
        public AudioClip fireTornadoVortexClip;

        [Header("Obsidian Guillotine (Triệu Hồi Bom Dung Nham & Bổ Nện Đất)")]
        [Tooltip("Âm thanh triệu hồi 3 tảng bom dung nham lơ lửng")]
        public AudioClip obsidianSummonBombsClip;

        [Tooltip("Âm thanh từng quả bom dung nham rít bay phóng tới")]
        public AudioClip bombHomingLaunchClip;

        [Tooltip("Âm thanh bom dung nham nổ tung mặt đất")]
        public AudioClip bombExplosionClip;

        [Tooltip("Âm thanh loạt mảnh đạn shrapnel bắn tung tóe")]
        public AudioClip shrapnelBurstClip;

        [Header("Volcanic Artillery (Pháo Hỏa Nham Đại Bác)")]
        [Tooltip("Âm thanh bắn pháo dung nham đại bác")]
        public AudioClip volcanicArtilleryFireClip;

        [Tooltip("Âm thanh đạn pháo dung nham nổ cày nát đấu trường")]
        public AudioClip volcanicArtilleryImpactClip;
    }

    [Serializable]
    public class BossBGMAudioData
    {
        [Header("Epic Orchestral BGM Tracks (Cho Từng Dạng)")]
        [Tooltip("Nhạc nền giao tranh Phase 0 (Quỷ Vương Cổ Đại)")]
        public AudioClip phase0BGM;

        [Tooltip("Nhạc nền giao tranh hùng tráng Phase 1 (Cung Thủ Nước)")]
        public AudioClip phase1BGM;

        [Tooltip("Nhạc nền giao tranh kịch tính Phase 2 (Tử Thần Liềm)")]
        public AudioClip phase2BGM;

        [Tooltip("Nhạc nền trận chiến định mệnh đỉnh cao Phase 3 (Địa Chấn Thiên Thạch)")]
        public AudioClip phase3BGM;

        [Tooltip("Nhạc nền Trùm Cuối Tối Thượng Phase 4 (Hỏa Nham Ma)")]
        public AudioClip phase4BGM;

        [Tooltip("Thời gian chuyển mượt giữa các bản nhạc (giây)")]
        public float bgmCrossfadeDuration = 2.0f;

        [Tooltip("Tự động lặp lại nhạc nền")]
        public bool loopBGM = true;
    }
}
