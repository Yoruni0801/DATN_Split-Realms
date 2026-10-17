using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    [CreateAssetMenu(fileName = "NewBossAttackData", menuName = "Quoc/Boss/Attack Data")]
    public class BossAttackData : ScriptableObject
    {
        [Header("General Settings")]
        [Tooltip("Tên định danh của đòn đánh")]
        public string attackName = "New Attack";

        [Tooltip("Các Phase được phép dùng chiêu này")]
        public List<BossPhase> allowedPhases = new List<BossPhase> { BossPhase.Phase1, BossPhase.Phase2, BossPhase.Phase3 };

        [Tooltip("Khoảng cách tối thiểu với Player để tung chiêu")]
        public float minDistance = 0f;

        [Tooltip("Khoảng cách tối đa với Player để tung chiêu")]
        public float maxDistance = 10f;

        [Tooltip("Thời gian cooldown của chiêu (giây)")]
        public float cooldown = 3f;

        [Tooltip("Trọng số xác suất khi chọn chiêu")]
        public float weight = 1f;

        [Header("Timings & Damage")]
        [Tooltip("Thời gian báo hiệu / chuẩn bị trước khi đánh (giây)")]
        public float telegraphDuration = 0.5f;

        [Tooltip("Thời gian hồi chiêu / nghỉ sau khi tung chiêu (giây)")]
        public float recoveryDuration = 1f;

        [Tooltip("Sát thương gây ra")]
        public int damage = 20;

        [Header("Spine Animation Settings")]
        [SpineAnimation, Tooltip("Tên Spine Animation dành riêng cho đòn đánh này (chọn từ danh sách Spine)")]
        public string spineAnimationName = "Attack";

        [Tooltip("Có lặp lại animation Spine này không?")]
        public bool loopSpineAnimation = false;

        [Header("Animator Trigger (Unity Animator Fallback)")]
        [Tooltip("Tên Trigger parameter nếu dùng Unity Animator")]
        public string animatorTrigger = "Slash";

        [Header("Attack Specific Variables")]
        [Tooltip("Số lần chém (dành cho Triple Slash / Combo)")]
        public int comboCount = 1;

        [Tooltip("Tốc độ lướt / lao (dành cho Charge / Double Charge)")]
        public float chargeSpeed = 15f;

        [Tooltip("Thời gian lướt tối đa (dành cho Charge)")]
        public float chargeDuration = 0.8f;

        [Tooltip("Tốc độ shockwave tạo ra khi giậm đất")]
        public float shockwaveSpeed = 8f;

        [Tooltip("Tốc độ đạn (dành cho Ranged Attack)")]
        public float projectileSpeed = 12f;

        [System.NonSerialized]
        public float lastTimeUsed = -999f;

        public bool IsOnCooldown(float currentTime)
        {
            return currentTime < lastTimeUsed + cooldown;
        }

        public void Use(float currentTime)
        {
            lastTimeUsed = currentTime;
        }
    }
}
