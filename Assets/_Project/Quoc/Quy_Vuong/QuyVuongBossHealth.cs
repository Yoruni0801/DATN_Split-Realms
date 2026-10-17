using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Quoc.QuyVuong
{
    public class QuyVuongBossHealth : MonoBehaviour,
        Quoc.Boss.IDamageable,
        Bao.Enemies.IDamageable,
        EnemySystem.IDamageable,
        IEnemyTarget
    {
        [Header("Phase Max Health Settings (4 Phases)")]
        [SerializeField]
        private int[] phaseMaxHealths = new int[] { 1000, 1500, 2000, 2500 };

        [Header("Hurt Flash Settings")]
        [SerializeField]
        private Color hurtFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField]
        private float hurtFlashDuration = 0.15f;

        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int CurrentPhaseIndex { get; private set; } = 0;
        public bool IsInvulnerable { get; private set; } = false;
        public bool IsTransitioning { get; private set; } = false;
        public bool IsDead { get; private set; } = false;

        public int TotalPhases => phaseMaxHealths != null ? phaseMaxHealths.Length : 0;
        public bool HasNextPhase => CurrentPhaseIndex + 1 < TotalPhases;

        // Events
        public event Action<int, int> OnHealthChanged;
        public event Action<int> OnPhaseChanged;
        public event Action OnPhaseTransitioning;
        public event Action OnDied;

        // IEnemyTarget Implementation
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

        private void Awake()
        {
            InitializeHealthForPhase(0);
        }

        public void InitializeHealthForPhase(int phaseIndex)
        {
            CurrentPhaseIndex = phaseIndex;
            if (phaseMaxHealths != null && phaseIndex >= 0 && phaseIndex < phaseMaxHealths.Length && phaseMaxHealths[phaseIndex] > 0)
            {
                MaxHealth = phaseMaxHealths[phaseIndex];
            }
            else
            {
                MaxHealth = 1000;
            }
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(int damage)
        {
            if (CurrentHealth <= 0 || IsInvulnerable || IsTransitioning || IsDead || damage <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            Debug.Log($"[QuyVuongBossHealth] Boss nhận {damage} damage! HP: {CurrentHealth}/{MaxHealth} (Phase Index: {CurrentPhaseIndex})");

            _ = TriggerHurtFlashAsync();

            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0)
            {
                if (HasNextPhase)
                {
                    Debug.Log($"[QuyVuongBossHealth] Hết máu Phase {CurrentPhaseIndex + 1}! Chuẩn bị chuyển Phase!");
                    NotifyPhaseTransitioning();
                }
                else
                {
                    IsDead = true;
                    Debug.Log("[QuyVuongBossHealth] Boss đã cạn máu ở Phase cuối cùng! Hạ gục!");
                    OnDied?.Invoke();
                }
            }
        }

        public void NotifyPhaseTransitioning()
        {
            IsTransitioning = true;
            IsInvulnerable = true;
            OnPhaseTransitioning?.Invoke();
        }

        public void AdvanceToNextPhase()
        {
            CurrentPhaseIndex++;
            InitializeHealthForPhase(CurrentPhaseIndex);
            IsTransitioning = false;
            IsInvulnerable = false;

            Debug.Log($"[QuyVuongBossHealth] Đã chuyển sang Phase Index {CurrentPhaseIndex}! HP hồi đầy: {CurrentHealth}/{MaxHealth}");

            OnPhaseChanged?.Invoke(CurrentPhaseIndex);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable;
        }

        private async Task TriggerHurtFlashAsync()
        {
            var switcher = GetComponent<QuyVuongPhaseSwitcher>() ?? GetComponentInParent<QuyVuongPhaseSwitcher>() ?? GetComponentInChildren<QuyVuongPhaseSwitcher>();
            if (switcher != null)
            {
                switcher.TriggerHurtFlash();
                return;
            }

            QuyVuongBossPhase currentPhase = GetComponent<QuyVuongBossController>()?.CurrentPhaseModel;
            if (currentPhase == null)
            {
                currentPhase = GetComponentInChildren<QuyVuongBossPhase>();
            }

            if (currentPhase != null)
            {
                currentPhase.SetColor(hurtFlashColor);
                int durationMs = Mathf.RoundToInt(hurtFlashDuration * 1000f);
                await Task.Delay(durationMs, destroyCancellationToken);

                if (this == null) return;
                currentPhase.SetColor(Color.white);
            }
        }
    }
}
