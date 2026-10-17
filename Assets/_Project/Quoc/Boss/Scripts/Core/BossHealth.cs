using System;
using System.Collections;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    public class BossHealth : MonoBehaviour, IDamageable, Bao.Enemies.IDamageable, EnemySystem.IDamageable, IEnemyTarget
    {
        [Header("Health Settings")]
        [SerializeField, Tooltip("Máu tối đa của Boss")]
        private int maxHealth = 1000;

        [Header("Phase Settings")]
        [SerializeField, Tooltip("Nếu tích chọn: Khi hết HP ở Phase 1, Boss sẽ chuyển sang Phase 2 (hồi lại máu full) thay vì chết ngay!")]
        private bool transitionOnDeath = true;

        [SerializeField, Tooltip("Máu tối đa của Phase 2 (nếu để 0 sẽ lấy bằng MaxHealth ban đầu)")]
        private int phase2MaxHealth = 0;

        [Header("Phase Thresholds (% HP)")]
        [SerializeField, Range(0.1f, 1f), Tooltip("Tỷ lệ HP để chuyển sang Phase 2 (mặc định 0.8 = 80%)")]
        private float phase2Threshold = 0.8f;

        [SerializeField, Range(0.05f, 0.9f), Tooltip("Tỷ lệ HP để chuyển sang Phase 3 (mặc định 0.4 = 40%)")]
        private float phase3Threshold = 0.4f;

        [Header("Hurt Flash Settings (Spine)")]
        [SerializeField, Tooltip("Màu sắc nhấp nháy của Boss khi bị dính sát thương (Chỉnh trên Inspector)")]
        private Color hurtFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField, Tooltip("Thời gian nhấp nháy đổi màu khi dính sát thương (giây)")]
        private float hurtFlashDuration = 0.15f;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;
        public bool IsInvulnerable { get; private set; } = false;
        public bool IsDead => CurrentHealth <= 0;
        public bool HasPendingPhaseTransition { get; private set; } = false;

        private Coroutine flashCoroutine;

        // IEnemyTarget Interface Implementation
        Transform IEnemyTarget.Transform => transform;
        bool IEnemyTarget.IsAlive => !IsDead;
        float IEnemyTarget.CurrentHealth => (float)CurrentHealth;

        void IEnemyTarget.TakeDamage(float damage)
        {
            TakeDamage(Mathf.RoundToInt(damage));
        }

        void IEnemyTarget.ApplyStun(float duration) { }
        void IEnemyTarget.FlyToTarget(Vector3 targetPosition, float holdTime) { }
        void IEnemyTarget.ApplyVelocity(Vector2 velocity, bool forceDynamicBody) { }

        // EnemySystem.IDamageable Implementation
        void EnemySystem.IDamageable.Die()
        {
            if (!IsDead)
            {
                TakeDamage(CurrentHealth);
            }
        }

        // Events
        public event Action<int, int> OnHealthChanged;
        public event Action<BossPhase> OnPhaseChanged;
        public event Action OnDied;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(int damage)
        {
            if (IsDead || IsInvulnerable || damage <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            Debug.Log($"[BossHealth] Boss nhận {damage} damage! HP còn lại: {CurrentHealth}/{maxHealth} (Phase {CurrentPhase})");

            // Kích hoạt nhấp nháy đổi màu Spine khi dính damage
            TriggerHurtFlash();

            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            CheckPhaseThresholds();

            if (IsDead)
            {
                if (transitionOnDeath && CurrentPhase == BossPhase.Phase1)
                {
                    Debug.Log("[BossHealth] Boss hết máu ở Phase 1! Kích hoạt chuyển Phase 2!");
                    HasPendingPhaseTransition = true;
                    // Hồi lại máu cho Phase 2
                    maxHealth = phase2MaxHealth > 0 ? phase2MaxHealth : maxHealth;
                    CurrentHealth = maxHealth;
                    OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
                }
                else
                {
                    OnDied?.Invoke();
                }
            }
        }

        private void TriggerHurtFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(HurtFlashRoutine());
        }

        private IEnumerator HurtFlashRoutine()
        {
            BossController controller = GetComponent<BossController>();
            SkeletonAnimation spine = controller != null ? controller.SkeletonAnimation : GetComponent<SkeletonAnimation>();
            if (spine == null) spine = GetComponentInChildren<SkeletonAnimation>();

            if (spine != null && spine.Skeleton != null)
            {
                spine.Skeleton.SetColor(hurtFlashColor);
                yield return new WaitForSeconds(hurtFlashDuration);
                spine.Skeleton.SetColor(Color.white);
            }
            else
            {
                SpriteRenderer sr = controller != null ? controller.SpriteRenderer : GetComponent<SpriteRenderer>();
                if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

                if (sr != null)
                {
                    Color orig = sr.color;
                    sr.color = hurtFlashColor;
                    yield return new WaitForSeconds(hurtFlashDuration);
                    sr.color = orig;
                }
            }

            flashCoroutine = null;
        }

        private void OnDisable()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            BossController controller = GetComponent<BossController>();
            SkeletonAnimation spine = controller != null ? controller.SkeletonAnimation : GetComponent<SkeletonAnimation>();
            if (spine == null) spine = GetComponentInChildren<SkeletonAnimation>();
            if (spine != null && spine.Skeleton != null)
            {
                spine.Skeleton.SetColor(Color.white);
            }
        }

        private void CheckPhaseThresholds()
        {
            float hpPercent = (float)CurrentHealth / maxHealth;

            if (CurrentPhase == BossPhase.Phase1 && hpPercent <= phase2Threshold)
            {
                HasPendingPhaseTransition = true;
            }
            else if (CurrentPhase == BossPhase.Phase2 && hpPercent <= phase3Threshold)
            {
                HasPendingPhaseTransition = true;
            }
        }

        public void ConsumePhaseTransition()
        {
            HasPendingPhaseTransition = false;
            if (CurrentPhase == BossPhase.Phase1)
            {
                CurrentPhase = BossPhase.Phase2;
                OnPhaseChanged?.Invoke(CurrentPhase);
            }
            else if (CurrentPhase == BossPhase.Phase2)
            {
                CurrentPhase = BossPhase.Phase3;
                OnPhaseChanged?.Invoke(CurrentPhase);
            }
        }

        public void SetInvulnerable(bool value)
        {
            IsInvulnerable = value;
        }
    }
}
