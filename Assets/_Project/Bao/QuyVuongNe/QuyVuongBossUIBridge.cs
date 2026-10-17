using Bao.Bosses;
using Quoc.QuyVuong;
using UnityEngine;

namespace Bao.QuyVuongNe
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(QuyVuongBossHealth), typeof(BossUIEventDispatcher))]
    public sealed class QuyVuongBossUIBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuyVuongBossHealth health;
        [SerializeField] private BossUIEventDispatcher dispatcher;
        [SerializeField] private QuyVuongPhaseSwitcher phaseSwitcher;

        [Header("Presentation")]
        [SerializeField] private string bossDisplayName = "Quỷ Vương";
        [SerializeField] private bool engageOnStart = true;

        private bool initialized;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            InitializeUI();

            if (engageOnStart)
            {
                Engage();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Engage()
        {
            InitializeUI();
            dispatcher?.Engage();
        }

        private void ResolveReferences()
        {
            if (health == null)
            {
                health = GetComponent<QuyVuongBossHealth>();
            }

            if (dispatcher == null)
            {
                dispatcher = GetComponent<BossUIEventDispatcher>();
            }

            if (phaseSwitcher == null)
            {
                phaseSwitcher = GetComponentInChildren<QuyVuongPhaseSwitcher>(true);
            }
        }

        private void InitializeUI()
        {
            if (initialized || health == null || dispatcher == null)
            {
                return;
            }

            dispatcher.Initialize(bossDisplayName, health.MaxHealth);
            dispatcher.UpdateHealth(health.CurrentHealth, health.MaxHealth);
            dispatcher.ChangePhase(GetDisplayedPhaseIndex());
            initialized = true;
        }

        private void Subscribe()
        {
            if (health == null)
            {
                return;
            }

            Unsubscribe();
            health.OnHealthChanged += HandleHealthChanged;
            health.OnPhaseChanged += HandleHealthPhaseChanged;
            health.OnDied += HandleDied;

            if (phaseSwitcher != null)
            {
                phaseSwitcher.OnPhaseChanged += HandleSubPhaseChanged;
            }
        }

        private void Unsubscribe()
        {
            if (health == null)
            {
                return;
            }

            health.OnHealthChanged -= HandleHealthChanged;
            health.OnPhaseChanged -= HandleHealthPhaseChanged;
            health.OnDied -= HandleDied;

            if (phaseSwitcher != null)
            {
                phaseSwitcher.OnPhaseChanged -= HandleSubPhaseChanged;
            }
        }

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            dispatcher?.UpdateHealth(currentHealth, maxHealth);
        }

        private void HandleHealthPhaseChanged(int phaseIndex)
        {
            dispatcher?.ChangePhase(phaseIndex <= 0 ? 0 : GetDisplayedPhaseIndex());
        }

        private void HandleSubPhaseChanged(int subPhaseIndex)
        {
            if (health != null && health.CurrentPhaseIndex > 0)
            {
                dispatcher?.ChangePhase(1 + subPhaseIndex);
            }
        }

        private void HandleDied()
        {
            dispatcher?.Die();
        }

        private int GetDisplayedPhaseIndex()
        {
            if (health == null || health.CurrentPhaseIndex <= 0)
            {
                return 0;
            }

            return 1 + (phaseSwitcher != null ? phaseSwitcher.CurrentPhaseIndex : 0);
        }
    }
}
