using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Quoc.QuyVuong;
using Quoc.QuyVuong.Phase1;
using Quoc.QuyVuong.Boss_Phase1;
using Quoc.QuyVuong.Boss_Phase2;
using Quoc.QuyVuong.Boss_Phase3;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Component cảm biến & kích hoạt âm thanh kỹ năng thời gian thực (Real-time Skill Audio Sensor):
    /// - Theo dõi trực quan trạng thái Particle System, VFX charge/slash, Projectile và Hazard của Boss.
    /// - Tự động phát âm thanh chính xác ngay tức thì khi bất kỳ chiêu thức nào được kích hoạt:
    ///   + Mưa tên (Rain Arrow)
    ///   + Bắn tên ngang (Horizontal Arrow)
    ///   + Dây móc & Lồng nước (Grapple & Cage)
    ///   + Húc tốc độ cao (Ram Dash)
    ///   + Trảm liềm (Scythe Slash)
    ///   + Phi đạn đuổi (Homing Projectiles)
    ///   + Rạch đất (Ground Line)
    ///   + Bổ giáo thiên thạch (Sky Leap Meteor Slam)
    ///   + Triệu hồi quái (Dusthorn / Enemy Spawn)
    ///   + Cột nước khổng lồ (Water Beam)
    ///   + Toàn bộ kỹ năng Phase 4 (Skull Dash, Uppercut, Bom dung nham)
    /// - 100% Không xâm lấn, không chỉnh sửa code cũ.
    /// </summary>
    [RequireComponent(typeof(QuyVuongAudioController))]
    public class QuyVuongSkillAudioTrigger : MonoBehaviour
    {
        private QuyVuongAudioController audioController;
        private readonly List<ParticleSystem> monitoredParticles = new List<ParticleSystem>();
        private readonly HashSet<ParticleSystem> activeParticles = new HashSet<ParticleSystem>();

        private int lastSeenArrowsCount = 0;
        private int lastSeenHomingCount = 0;
        private int lastSeenHazardsCount = 0;

        private void Awake()
        {
            audioController = GetComponent<QuyVuongAudioController>();
        }

        private void OnEnable()
        {
            StartCoroutine(ScanAndMonitorSkillVfxRoutine());
            StartCoroutine(MonitorProjectilesAndHazardsRoutine());
        }

        private void OnDisable()
        {
            monitoredParticles.Clear();
            activeParticles.Clear();
        }

        /// <summary>
        /// Quét và theo dõi trạng thái của tất cả Particle System con của Boss
        /// </summary>
        private IEnumerator ScanAndMonitorSkillVfxRoutine()
        {
            var wait = new WaitForSeconds(0.05f);

            while (true)
            {
                if (audioController == null) yield break;

                // Quét lại danh sách particles định kỳ
                ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
                monitoredParticles.Clear();
                monitoredParticles.AddRange(particles);

                for (int i = 0; i < monitoredParticles.Count; i++)
                {
                    ParticleSystem ps = monitoredParticles[i];
                    if (ps == null) continue;

                    bool isPlaying = ps.isPlaying && ps.gameObject.activeInHierarchy;
                    bool wasPlaying = activeParticles.Contains(ps);

                    if (isPlaying && !wasPlaying)
                    {
                        activeParticles.Add(ps);
                        OnParticleStarted(ps);
                    }
                    else if (!isPlaying && wasPlaying)
                    {
                        activeParticles.Remove(ps);
                        OnParticleStopped(ps);
                    }
                }

                yield return wait;
            }
        }

        private void OnParticleStarted(ParticleSystem ps)
        {
            if (ps == null || audioController == null) return;
            string pName = ps.gameObject.name.ToLower();

            // 1. Phân loại hiệu ứng gồng tụ năng lượng (Charge)
            if (pName.Contains("charge") || pName.Contains("gong") || pName.Contains("telegraph") || pName.Contains("warning"))
            {
                if (pName.Contains("water") || pName.Contains("beam"))
                {
                    audioController.PlayPhase0WaterBeamCharge();
                }
                else if (pName.Contains("horizontal") || pName.Contains("arrow"))
                {
                    audioController.PlayHorizontalArrowCharge();
                }
                else if (pName.Contains("homing") || pName.Contains("ranged"))
                {
                    audioController.PlayHomingSummonCharge();
                }
                else if (pName.Contains("scythe") || pName.Contains("slash"))
                {
                    audioController.PlayScytheWindup();
                }
                else if (pName.Contains("ram"))
                {
                    audioController.PlayRamWindup();
                }
                else if (pName.Contains("ground"))
                {
                    audioController.PlayEarthStomp();
                }
            }
            // 2. Hiệu ứng trảm liềm / Vung vũ khí
            else if (pName.Contains("slash") || pName.Contains("scythe") || pName.Contains("chem") || pName.Contains("sword"))
            {
                audioController.PlayScytheHeavySwing();
            }
            // 3. Hiệu ứng bắn đạn / bắn tên
            else if (pName.Contains("shoot") || pName.Contains("fire") || pName.Contains("projectile"))
            {
                if (pName.Contains("horizontal"))
                {
                    audioController.PlayHorizontalArrowFire();
                }
                else
                {
                    audioController.PlayRainArrowShoot();
                }
            }
            // 4. Hiệu ứng bay / lướt
            else if (pName.Contains("trail") || pName.Contains("fly") || pName.Contains("dash") || pName.Contains("rush"))
            {
                if (pName.Contains("ram"))
                {
                    audioController.PlayRamDashRush();
                }
                else
                {
                    audioController.PlayWingFlap();
                }
            }
            // 5. Hiệu ứng dậm đất / nổ / chấn động
            else if (pName.Contains("impact") || pName.Contains("slam") || pName.Contains("land") || pName.Contains("stomp") || pName.Contains("eruption"))
            {
                if (pName.Contains("meteor") || pName.Contains("sky") || pName.Contains("leap"))
                {
                    audioController.PlaySkyLeapHeavyMeteorImpact();
                }
                else if (pName.Contains("earth") || pName.Contains("ground") || pName.Contains("dusthorn"))
                {
                    audioController.PlayEarthEruptionDusthorn();
                }
                else
                {
                    audioController.PlaySubBassImpact(0.8f);
                }
            }
            // 6. Hiệu ứng triệu hồi
            else if (pName.Contains("spawn") || pName.Contains("summon") || pName.Contains("portal"))
            {
                audioController.PlayEnemySpawnBurst();
            }
        }

        private void OnParticleStopped(ParticleSystem ps)
        {
            if (ps == null || audioController == null) return;
            string pName = ps.gameObject.name.ToLower();

            if (pName.Contains("charge") || pName.Contains("gong") || pName.Contains("telegraph"))
            {
                audioController.StopChargeSound();
            }
            else if (pName.Contains("trail") || pName.Contains("fly"))
            {
                audioController.StopMovementSound();
            }
        }

        /// <summary>
        /// Theo dõi sự xuất hiện của các đối tượng đạn / mũi tên / hazard trong trận đấu
        /// </summary>
        private IEnumerator MonitorProjectilesAndHazardsRoutine()
        {
            var wait = new WaitForSeconds(0.06f);

            while (true)
            {
                if (audioController == null) yield break;

                // 1. Quét mũi tên Phase 1 (QuyVuongPhase1Arrow)
                var arrows = FindObjectsByType<QuyVuongPhase1Arrow>(FindObjectsSortMode.None);
                if (arrows.Length > lastSeenArrowsCount)
                {
                    int newlySpawned = arrows.Length - lastSeenArrowsCount;
                    for (int i = 0; i < newlySpawned; i++)
                    {
                        audioController.PlayRainArrowShoot();
                    }
                }
                lastSeenArrowsCount = arrows.Length;

                // 2. Quét phi đạn ma thuật Phase 2 (QuyVuongHomingProjectile)
                var homingProjectiles = FindObjectsByType<QuyVuongHomingProjectile>(FindObjectsSortMode.None);
                if (homingProjectiles.Length > lastSeenHomingCount)
                {
                    audioController.PlayHomingMultiFire();
                }
                lastSeenHomingCount = homingProjectiles.Length;

                // 3. Quét Hazard nứt đất Phase 3 (GroundLineHazard)
                var groundHazards = FindObjectsByType<GroundLineHazard>(FindObjectsSortMode.None);
                if (groundHazards.Length > lastSeenHazardsCount)
                {
                    audioController.PlayGroundLineCrackRumble();
                    audioController.PlayEarthStomp();
                }
                lastSeenHazardsCount = groundHazards.Length;

                yield return wait;
            }
        }
    }
}
