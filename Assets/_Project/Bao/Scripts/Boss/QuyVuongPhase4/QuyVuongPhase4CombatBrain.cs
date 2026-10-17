using System.Collections;
using UnityEngine;
using Quoc.QuyVuong;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// AI Combat Brain cho Phase 4 của Quỷ Vương (Magma Golem)
    /// Tự động kích hoạt khi Phase 4 active, quản lý quyết định lựa chọn linh hoạt giữa 2 Skills:
    /// 1. Tectonic Rending (Liệt Địa Ma Trảo & Lốc Xoáy Lửa)
    /// 2. Obsidian Guillotine (Triệu Hồi Bom Dung Nham & Bổ Nện Đất)
    /// </summary>
    public class QuyVuongPhase4CombatBrain : MonoBehaviour, IQuyVuongPhaseFlow
    {
        public event System.Action OnFlowCompleted;
        public bool IsExecutingFlow => IsExecutingSkill;

        [Header("References")]
        [SerializeField] private QuyVuongBossController bossController;
        [SerializeField] private QuyVuongBossHealth bossHealth;
        [SerializeField] private QuyVuongBossPhase phase4Model;

        [Header("Phase 4 Skills")]
        [SerializeField] private Phase4TectonicRendingSkill tectonicRendingSkill;
        [SerializeField] private Phase4ObsidianGuillotineSkill obsidianGuillotineSkill;

        [Header("Combat Settings")]
        [SerializeField] private float meleeRangeThreshold = 6f;
        [SerializeField] private float globalSkillCooldown = 2.5f;
        [SerializeField] private float detectionRadius = 18f;

        [Header("Aggression Settings")]
        [SerializeField] private float aggressionMultiplierLowHealth = 1.5f;
        [SerializeField] private float lowHealthThreshold = 0.35f;

        public Transform PlayerTransform { get; private set; }
        public bool IsExecutingSkill { get; private set; }
        public CombatState CurrentState { get; private set; } = CombatState.Idle;

        private float lastSkillExecutionTime;
        private float currentCooldown;
        private bool isFrozen = false;

        public enum CombatState
        {
            Idle,
            Telegraph,
            ExecutingSkill,
            Recovery
        }

        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                ClearAllSpawnedSkills();
            }
        }

        private void HandleDied()
        {
            SetFrozen(true);
            ClearAllSpawnedSkills();
        }

        public void ClearAllSpawnedSkills()
        {
            StopAllCoroutines();
            IsExecutingSkill = false;
            CurrentState = CombatState.Idle;

            if (tectonicRendingSkill != null)
            {
                tectonicRendingSkill.ClearActiveSkillObjects();
            }
            if (obsidianGuillotineSkill != null)
            {
                obsidianGuillotineSkill.ClearActiveProjectiles();
            }

            var spires = FindObjectsByType<Phase4MagmaSpire>(FindObjectsSortMode.None);
            foreach (var s in spires) if (s != null) Destroy(s.gameObject);

            var trenches = FindObjectsByType<Phase4MagmaTrenchSegment>(FindObjectsSortMode.None);
            foreach (var t in trenches) if (t != null) Destroy(t.gameObject);

            var mortars = FindObjectsByType<Phase4SpineMortarProjectile>(FindObjectsSortMode.None);
            foreach (var m in mortars) if (m != null) Destroy(m.gameObject);
        }

        private void Awake()
        {
            if (bossController == null)
                bossController = GetComponent<QuyVuongBossController>() ?? GetComponentInParent<QuyVuongBossController>();
            if (bossHealth == null)
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>();

            if (tectonicRendingSkill == null)
                tectonicRendingSkill = GetComponent<Phase4TectonicRendingSkill>() ?? gameObject.AddComponent<Phase4TectonicRendingSkill>();
            if (obsidianGuillotineSkill == null)
                obsidianGuillotineSkill = GetComponent<Phase4ObsidianGuillotineSkill>() ?? gameObject.AddComponent<Phase4ObsidianGuillotineSkill>();

            if (phase4Model == null)
            {
                var phases = GetComponentsInChildren<QuyVuongBossPhase>(true);
                foreach (var p in phases)
                {
                    if (p.gameObject.name.Contains("Phase4") || p.gameObject.name.Contains("Lua"))
                    {
                        phase4Model = p;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (bossHealth == null)
                bossHealth = GetComponent<QuyVuongBossHealth>() ?? GetComponentInParent<QuyVuongBossHealth>();

            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
                bossHealth.OnDied += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
            }
            ClearAllSpawnedSkills();
        }

        private void Start()
        {
            FindPlayer();
            lastSkillExecutionTime = -globalSkillCooldown;
        }

        private void Update()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;

            if (bossHealth != null && bossHealth.IsTransitioning)
                return;

            // Nếu model Phase 4 chưa active thì không thực hiện skill Phase 4
            if (phase4Model != null && !phase4Model.gameObject.activeInHierarchy)
            {
                return;
            }

            if (PlayerTransform == null)
            {
                FindPlayer();
                if (PlayerTransform == null) return;
            }

            // Luôn quay mặt về phía Player trong mọi frame (kể cả khi đang cast skill)
            FacePlayer();

            if (!IsExecutingSkill && CanExecuteSkill())
            {
                DecideAndExecuteSkill();
            }
        }

        public void FacePlayer()
        {
            if (PlayerTransform == null) FindPlayer();
            if (PlayerTransform != null)
            {
                float dirX = PlayerTransform.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.05f)
                {
                    float scaleX = Mathf.Abs(transform.localScale.x);
                    transform.localScale = new Vector3(
                        dirX > 0 ? -scaleX : scaleX,
                        transform.localScale.y,
                        transform.localScale.z
                    );

                    if (phase4Model != null && phase4Model.transform != transform)
                    {
                        phase4Model.SetFacingDirection(dirX);
                    }
                }
            }
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                PlayerTransform = PlayerRegistry.Current.transform;
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                PlayerTransform = playerObj.transform;
            }
            else
            {
                var health = FindAnyObjectByType<PlayerHealth>();
                if (health != null)
                {
                    PlayerTransform = health.transform;
                }
            }
        }

        private bool CanExecuteSkill()
        {
            if (IsExecutingSkill || isFrozen) return false;

            float timeSinceLastSkill = Time.time - lastSkillExecutionTime;
            currentCooldown = globalSkillCooldown;

            if (bossHealth != null)
            {
                float healthPercent = (float)bossHealth.CurrentHealth / Mathf.Max(1, bossHealth.MaxHealth);
                if (healthPercent <= lowHealthThreshold)
                {
                    currentCooldown /= aggressionMultiplierLowHealth;
                }
            }

            return timeSinceLastSkill >= currentCooldown;
        }

        private void DecideAndExecuteSkill()
        {
            if (PlayerTransform == null || isFrozen) return;

            float distanceToPlayer = Vector2.Distance(transform.position, PlayerTransform.position);

            if (distanceToPlayer > detectionRadius)
            {
                CurrentState = CombatState.Idle;
                return;
            }

            // Chọn ngẫu nhiên có trọng số giữa 2 Skills dựa theo khoảng cách
            if (distanceToPlayer <= meleeRangeThreshold)
            {
                // Tầm gần: 65% Tectonic Rending (Skull Dash & Uppercut), 35% Obsidian Guillotine (Bom & Bổ Nện)
                float roll = Random.value;
                if (roll < 0.65f && tectonicRendingSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(tectonicRendingSkill));
                }
                else if (obsidianGuillotineSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(obsidianGuillotineSkill));
                }
                else if (tectonicRendingSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(tectonicRendingSkill));
                }
            }
            else
            {
                // Tầm xa: 70% Obsidian Guillotine (Air-Lock & Meteor Dive áp sát), 30% Tectonic Rending
                float roll = Random.value;
                if (roll < 0.70f && obsidianGuillotineSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(obsidianGuillotineSkill));
                }
                else if (tectonicRendingSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(tectonicRendingSkill));
                }
                else if (obsidianGuillotineSkill != null)
                {
                    StartCoroutine(ExecuteSkillRoutine(obsidianGuillotineSkill));
                }
            }
        }

        private IEnumerator ExecuteSkillRoutine(QuyVuongPhase4SkillBase skill)
        {
            IsExecutingSkill = true;
            CurrentState = CombatState.Telegraph;

            yield return StartCoroutine(skill.ExecuteSkill(PlayerTransform));

            lastSkillExecutionTime = Time.time;
            CurrentState = CombatState.Recovery;

            yield return new WaitForSeconds(0.5f);

            IsExecutingSkill = false;
            CurrentState = CombatState.Idle;

            if (!isFrozen && (bossHealth == null || !bossHealth.IsDead))
            {
                OnFlowCompleted?.Invoke();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, meleeRangeThreshold);
        }
    }
}
