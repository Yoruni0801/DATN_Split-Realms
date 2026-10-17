using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;
using Quoc.QuyVuong;

namespace Quoc.QuyVuong.Audio
{
    /// <summary>
    /// Component Quan sát & Kích hoạt Âm thanh Tấn công Tự động Cho Mọi Dạng (Phase 0, 1, 2, 3, 4):
    /// - Tự động hook vào toàn bộ Spine SkeletonAnimation của Boss Quỷ Vương trên tất cả các Phase.
    /// - Nhận diện chính xác từng Phase theo tên GameObject và cấu trúc phân cấp để kích hoạt SFX chuẩn xác.
    /// - 100% không can thiệp hay sửa đổi code cũ.
    /// </summary>
    [RequireComponent(typeof(QuyVuongAudioController))]
    public class QuyVuongAudioObserver : MonoBehaviour
    {
        private QuyVuongAudioController audioController;
        private QuyVuongBossHealth bossHealth;
        private QuyVuongBossController bossController;
        private QuyVuongPhaseSwitcher phaseSwitcher;

        private readonly List<SkeletonAnimation> observedSkeletons = new List<SkeletonAnimation>();
        private readonly Dictionary<SkeletonAnimation, string> lastPlayedAnimation = new Dictionary<SkeletonAnimation, string>();

        private void Awake()
        {
            audioController = GetComponent<QuyVuongAudioController>();
            bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            bossController = GetComponent<QuyVuongBossController>() ?? GetComponentInParent<QuyVuongBossController>() ?? GetComponentInChildren<QuyVuongBossController>();
            phaseSwitcher = GetComponent<QuyVuongPhaseSwitcher>() ?? GetComponentInParent<QuyVuongPhaseSwitcher>() ?? GetComponentInChildren<QuyVuongPhaseSwitcher>();
        }

        private void OnEnable()
        {
            HookAllSkeletonAnimations();
        }

        private void OnDisable()
        {
            UnhookAllSkeletonAnimations();
        }

        private void Start()
        {
            HookAllSkeletonAnimations();
            StartCoroutine(ContinuousObservationRoutine());
        }

        public void HookAllSkeletonAnimations()
        {
            UnhookAllSkeletonAnimations();

            SkeletonAnimation[] skeletons = GetComponentsInChildren<SkeletonAnimation>(true);
            foreach (var skel in skeletons)
            {
                if (skel != null)
                {
                    observedSkeletons.Add(skel);
                    if (skel.AnimationState != null)
                    {
                        skel.AnimationState.Start -= HandleSpineAnimationStart;
                        skel.AnimationState.Start += HandleSpineAnimationStart;

                        skel.AnimationState.Event -= HandleSpineCustomEvent;
                        skel.AnimationState.Event += HandleSpineCustomEvent;
                    }
                }
            }
        }

        private void UnhookAllSkeletonAnimations()
        {
            foreach (var skel in observedSkeletons)
            {
                if (skel != null && skel.AnimationState != null)
                {
                    skel.AnimationState.Start -= HandleSpineAnimationStart;
                    skel.AnimationState.Event -= HandleSpineCustomEvent;
                }
            }
            observedSkeletons.Clear();
        }

        /// <summary>
        /// Xử lý phát âm thanh chính xác khi bất kỳ Animation Spine nào của Boss bắt đầu chạy.
        /// </summary>
        private void HandleSpineAnimationStart(TrackEntry trackEntry)
        {
            if (trackEntry == null || trackEntry.Animation == null || audioController == null) return;

            string animName = trackEntry.Animation.Name;
            SkeletonAnimation activeSkel = GetActiveSkeletonAnimation();
            int identifiedPhase = IdentifyPhaseFromSkeleton(activeSkel);

            DispatchAttackAudioByAnimation(animName, identifiedPhase);
        }

        private void HandleSpineCustomEvent(TrackEntry trackEntry, Spine.Event e)
        {
            if (e == null || e.Data == null || audioController == null) return;
            string eventName = e.Data.Name.ToLower();

            if (eventName.Contains("footstep") || eventName.Contains("walk") || eventName.Contains("step"))
            {
                audioController.PlayFootstep();
            }
            else if (eventName.Contains("slash") || eventName.Contains("swing") || eventName.Contains("attack"))
            {
                audioController.PlayScytheHeavySwing();
            }
            else if (eventName.Contains("shoot") || eventName.Contains("fire"))
            {
                audioController.PlayRainArrowShoot();
            }
            else if (eventName.Contains("slam") || eventName.Contains("impact") || eventName.Contains("land"))
            {
                audioController.PlaySkyLeapHeavyMeteorImpact();
            }
            else if (eventName.Contains("roar") || eventName.Contains("grunt"))
            {
                audioController.PlayRageRoar();
            }
        }

        private SkeletonAnimation GetActiveSkeletonAnimation()
        {
            for (int i = 0; i < observedSkeletons.Count; i++)
            {
                if (observedSkeletons[i] != null && observedSkeletons[i].gameObject.activeInHierarchy)
                {
                    return observedSkeletons[i];
                }
            }
            return observedSkeletons.Count > 0 ? observedSkeletons[0] : null;
        }

        /// <summary>
        /// Nhận diện chính xác dạng / phase của Boss dựa theo tên GameObject hoặc Hierarchy.
        /// </summary>
        private int IdentifyPhaseFromSkeleton(SkeletonAnimation skel)
        {
            if (skel != null)
            {
                string objName = skel.gameObject.name.ToLower();
                Transform parent = skel.transform.parent;
                string parentName = parent != null ? parent.name.ToLower() : "";

                if (objName.Contains("phase0") || parentName.Contains("phase0") || objName.Contains("mamatick") || parentName.Contains("mamatick"))
                {
                    return 0; // Phase 0 (Cổ Đại)
                }
                if (objName.Contains("phase1") || parentName.Contains("phase1"))
                {
                    return 1; // Phase 1 (Cung Thủ)
                }
                if (objName.Contains("phase2") || parentName.Contains("phase2"))
                {
                    return 2; // Phase 2 (Liềm Tử Thần)
                }
                if (objName.Contains("phase3") || parentName.Contains("phase3"))
                {
                    return 3; // Phase 3 (Địa Chấn Thiên Thạch)
                }
                if (objName.Contains("phase4") || parentName.Contains("phase4") || objName.Contains("lua") || parentName.Contains("lua") || objName.Contains("magma") || parentName.Contains("magma"))
                {
                    return 4; // Phase 4 (Hỏa Nham Ma)
                }
            }

            // Fallback sang BossHealth Phase Index
            if (bossHealth != null)
            {
                return bossHealth.CurrentPhaseIndex;
            }
            if (phaseSwitcher != null)
            {
                return phaseSwitcher.CurrentPhaseIndex;
            }
            return 1;
        }

        private void DispatchAttackAudioByAnimation(string animName, int phaseIndex)
        {
            if (string.IsNullOrEmpty(animName) || audioController == null) return;

            string lowerAnim = animName.ToLower();

            // 1. Phân loại theo từng Dạng / Phase cụ thể
            switch (phaseIndex)
            {
                case 0: // Dạng 0: Quỷ Vương Cổ Đại (MamaTick / Water Beam)
                    if (lowerAnim.Contains("skill") || lowerAnim.Contains("attack"))
                    {
                        audioController.PlayPhase0WaterBeamBurst();
                        audioController.PlayPhase0WaterRainShoot();
                    }
                    else if (lowerAnim.Contains("run") || lowerAnim.Contains("fly") || lowerAnim.Contains("move"))
                    {
                        audioController.PlayPhase0SkyTeleport();
                    }
                    break;

                case 1: // Dạng 1: Quỷ Vương Cung Thủ Nước
                    if (lowerAnim.Contains("skill") || lowerAnim.Contains("attack"))
                    {
                        audioController.PlayRainArrowShoot();
                        audioController.PlayHorizontalArrowFire();
                    }
                    else if (lowerAnim.Contains("run") || lowerAnim.Contains("fly") || lowerAnim.Contains("move"))
                    {
                        audioController.PlayWingFlap();
                    }
                    break;

                case 2: // Dạng 2: Quỷ Vương Tử Thần Liềm
                    if (lowerAnim.Contains("attack") || lowerAnim.Contains("slash"))
                    {
                        audioController.PlayScytheHeavySwing();
                    }
                    else if (lowerAnim.Contains("skill") || lowerAnim.Contains("shoot") || lowerAnim.Contains("cast"))
                    {
                        audioController.PlayHomingMultiFire();
                    }
                    else if (lowerAnim.Contains("run") || lowerAnim.Contains("move") || lowerAnim.Contains("dash"))
                    {
                        audioController.PlayRelocateTeleport();
                    }
                    break;

                case 3: // Dạng 3: Quỷ Vương Địa Chấn & Thiên Thạch
                    if (lowerAnim.Contains("attack") || lowerAnim.Contains("ground"))
                    {
                        audioController.PlayEarthStomp();
                        audioController.PlayGroundLineCrackRumble();
                    }
                    else if (lowerAnim.Contains("gap closer") || lowerAnim.Contains("jump") || lowerAnim.Contains("leap") || lowerAnim.Contains("skill"))
                    {
                        audioController.PlaySkyLeapTakeoff();
                        audioController.PlaySkyLeapDescentSwoosh();
                    }
                    else if (lowerAnim.Contains("run") || lowerAnim.Contains("move"))
                    {
                        audioController.PlayFootstep();
                    }
                    break;

                case 4: // Dạng 4: Quỷ Vương Hỏa Nham Ma (Phase 4 Magma Golem)
                    if (lowerAnim.Contains("attack") || lowerAnim.Contains("slash") || lowerAnim.Contains("dash"))
                    {
                        audioController.PlayPhase4SkullDashRush();
                        audioController.PlayPhase4EruptionUppercut();
                    }
                    else if (lowerAnim.Contains("skill") || lowerAnim.Contains("shoot") || lowerAnim.Contains("bomb") || lowerAnim.Contains("cast"))
                    {
                        audioController.PlayPhase4BombExplosion();
                        audioController.PlayPhase4VolcanicArtilleryFire();
                    }
                    else if (lowerAnim.Contains("run") || lowerAnim.Contains("move"))
                    {
                        audioController.PlayFootstep();
                    }
                    break;

                default: // Mặc định chung
                    if (lowerAnim.Contains("attack") || lowerAnim.Contains("skill"))
                    {
                        audioController.PlayScytheHeavySwing();
                    }
                    break;
            }

            // 2. Các animation tử trận chung
            if (lowerAnim.Contains("death") || lowerAnim.Contains("die"))
            {
                audioController.PlayDeathSound();
            }
        }

        /// <summary>
        /// Vòng lặp Coroutine liên tục kiểm tra thay đổi Animation của Phase Model đang hoạt động
        /// phòng trường hợp Spine AnimationState event bị miss do script khác can thiệp.
        /// </summary>
        private IEnumerator ContinuousObservationRoutine()
        {
            var wait = new WaitForSeconds(0.04f);

            while (true)
            {
                if (audioController == null) yield break;

                for (int i = 0; i < observedSkeletons.Count; i++)
                {
                    SkeletonAnimation skel = observedSkeletons[i];
                    if (skel != null && skel.gameObject.activeInHierarchy && skel.AnimationState != null)
                    {
                        TrackEntry currentTrack = skel.AnimationState.GetCurrent(0);
                        if (currentTrack != null && currentTrack.Animation != null)
                        {
                            string currentAnimName = currentTrack.Animation.Name;
                            lastPlayedAnimation.TryGetValue(skel, out string prevAnim);

                            if (currentAnimName != prevAnim)
                            {
                                lastPlayedAnimation[skel] = currentAnimName;
                                int identifiedPhase = IdentifyPhaseFromSkeleton(skel);
                                DispatchAttackAudioByAnimation(currentAnimName, identifiedPhase);
                            }
                        }
                    }
                }

                yield return wait;
            }
        }
    }
}
