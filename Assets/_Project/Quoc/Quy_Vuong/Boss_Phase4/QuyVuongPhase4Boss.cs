using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Quoc.QuyVuong;
using Bao.Bosses.QuyVuongPhase4;

namespace Quoc.QuyVuong.Boss_Phase4
{
    /// <summary>
    /// Controller chính cho Quỷ Vương Phase 4 (Hỏa Nham Ma / Magma Golem).
    /// Triển khai interface IQuyVuongPhaseFlow để đồng bộ 100% với QuyVuongPhaseSwitcher và các Phase 1, 2, 3.
    /// Quản lý 2 kỹ năng cốt lõi:
    /// - Skill 1: Tectonic Rending (Liệt Địa Ma Trảo / Đầu Lâu Húc & Móc Lửa)
    /// - Skill 2: Obsidian Guillotine (Triệu Hồi Bom Nham Thạch & Bổ Nện Đất)
    /// </summary>
    public class QuyVuongPhase4Boss : MonoBehaviour, IQuyVuongPhaseFlow
    {
        public event Action OnFlowCompleted;
        public bool IsExecutingFlow => isExecutingSkill;

        [Header("=== References ===")]
        [SerializeField] private QuyVuongBossPhase bossPhase;
        [SerializeField] private QuyVuongBossHealth bossHealth;
        [SerializeField] private Phase4TectonicRendingSkill tectonicRendingSkill;
        [SerializeField] private Phase4ObsidianGuillotineSkill obsidianGuillotineSkill;

        [Header("=== Arena Relocation ===")]
        [SerializeField] private Transform[] arenaRelocationPoints;
        [SerializeField] private float dashRelocationSpeed = 18f;

        [Header("=== Combat Settings ===")]
        [SerializeField] private float meleeRangeThreshold = 6f;
        [SerializeField] private float detectionRadius = 18f;
        [SerializeField] private float attackCooldown = 2.0f;
        [SerializeField] private float attackCooldownAggressive = 1.0f;
        [SerializeField] private float lowHealthThreshold = 0.35f;

        [Header("=== Animation Names ===")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string moveAnim = "Run";
        [SerializeField] private string attackAnim = "Skill";

        private Transform playerTransform;
        private bool isExecutingSkill = false;
        private bool isFrozen = false;
        private bool isAggressive = false;
        private int lastSkillIndex = -1;
        private int manualSkillOverride = -1;
        private Coroutine activeSkillCoroutine;

        private void Awake()
        {
            FindReferences();
        }

        private void Start()
        {
            FindPlayer();
        }

        private void OnEnable()
        {
            isExecutingSkill = false;
            isFrozen = false;
            isAggressive = false;
            lastSkillIndex = -1;
            manualSkillOverride = -1;

            FindReferences();
            FindPlayer();

            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
                bossHealth.OnDied += HandleDied;
            }

            // Đảm bảo body hiển thị và phát Idle khi Phase 4 active
            if (tectonicRendingSkill != null) tectonicRendingSkill.SetBossBodyActive(true);
            if (obsidianGuillotineSkill != null) obsidianGuillotineSkill.SetBossBodyActive(true);
            bossPhase?.PlayIdle();
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
            }
            ClearAllSpawnedSkills();
            if (tectonicRendingSkill != null) tectonicRendingSkill.SetBossBodyActive(true);
            if (obsidianGuillotineSkill != null) obsidianGuillotineSkill.SetBossBodyActive(true);
        }

        private void Update()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;

            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            // Luôn quay mặt về phía Player trong mọi frame (kể cả khi đang cast skill)
            FacePlayer();

            CheckAggressiveState();

            if (isExecutingSkill) return;

            _ = ExecutePhase4BehaviorAsync();
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
            StopActiveSkill();
            StopAllCoroutines();

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

            isExecutingSkill = false;
            bossPhase?.PlayIdle();
        }

        public void FindReferences()
        {
            if (bossPhase == null)
            {
                bossPhase = GetComponent<QuyVuongBossPhase>() ?? GetComponentInChildren<QuyVuongBossPhase>();
            }

            if (bossHealth == null)
            {
                bossHealth = GetComponentInParent<QuyVuongBossHealth>() ?? GetComponent<QuyVuongBossHealth>();
            }

            if (tectonicRendingSkill == null)
            {
                tectonicRendingSkill = GetComponent<Phase4TectonicRendingSkill>() ?? GetComponentInChildren<Phase4TectonicRendingSkill>();
            }

            if (obsidianGuillotineSkill == null)
            {
                obsidianGuillotineSkill = GetComponent<Phase4ObsidianGuillotineSkill>() ?? GetComponentInChildren<Phase4ObsidianGuillotineSkill>();
            }
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                playerTransform = PlayerRegistry.Current.transform;
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                return;
            }

            var health = FindAnyObjectByType<PlayerHealth>();
            if (health != null)
            {
                playerTransform = health.transform;
                return;
            }

            var playerComp = FindAnyObjectByType<Player>();
            if (playerComp != null)
            {
                playerTransform = playerComp.transform;
            }
        }

        private void CheckAggressiveState()
        {
            if (bossHealth != null && bossHealth.MaxHealth > 0)
            {
                float currentHpPercent = (float)bossHealth.CurrentHealth / bossHealth.MaxHealth;
                isAggressive = currentHpPercent <= lowHealthThreshold;
            }
        }

        public void FaceDirection(float directionX)
        {
            if (Mathf.Abs(directionX) > 0.05f)
            {
                float scaleX = Mathf.Abs(transform.localScale.x);
                transform.localScale = new Vector3(
                    directionX > 0f ? -scaleX : scaleX,
                    transform.localScale.y,
                    transform.localScale.z
                );

                if (bossPhase != null && bossPhase.transform != transform)
                {
                    bossPhase.SetFacingDirection(directionX);
                }
            }
        }

        public void FacePlayer()
        {
            if (playerTransform == null) FindPlayer();
            if (playerTransform == null) return;
            FaceDirection(playerTransform.position.x - transform.position.x);
        }

        private async Task ExecutePhase4BehaviorAsync()
        {
            if (isExecutingSkill || isFrozen) return;
            isExecutingSkill = true;

            var ct = destroyCancellationToken;

            try
            {
                FacePlayer();

                int chosenSkillIndex = SelectNextSkillIndex();
                lastSkillIndex = chosenSkillIndex;

                QuyVuongPhase4SkillBase selectedSkill = chosenSkillIndex == 0 ? (QuyVuongPhase4SkillBase)tectonicRendingSkill : obsidianGuillotineSkill;

                if (selectedSkill != null && playerTransform != null)
                {
                    // Thực thi skill coroutine
                    var tcs = new TaskCompletionSource<bool>();
                    activeSkillCoroutine = StartCoroutine(RunSkillCoroutine(selectedSkill, tcs));
                    await tcs.Task;
                }

                if (this == null || ct.IsCancellationRequested || isFrozen) return;

                // Tùy chọn di chuyển đổi vị trí sau skill nếu có cấu hình arenaRelocationPoints
                if (arenaRelocationPoints != null && arenaRelocationPoints.Length > 0)
                {
                    Transform targetPoint = GetRandomRelocationPoint();
                    if (targetPoint != null)
                    {
                        await DashToPositionAsync(targetPoint.position, dashRelocationSpeed, ct);
                        if (this == null || ct.IsCancellationRequested || isFrozen) return;
                    }
                }

                FacePlayer();
                bossPhase?.PlayIdle();

                // Thời gian hồi chiêu
                float currentCooldown = isAggressive ? attackCooldownAggressive : attackCooldown;
                int cooldownMs = Mathf.Max(0, Mathf.RoundToInt(currentCooldown * 1000f));
                await Task.Delay(cooldownMs, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[QuyVuongPhase4Boss] Lỗi khi thực thi chuỗi hành động Phase 4: {ex.Message}");
            }
            finally
            {
                if (this != null)
                {
                    isExecutingSkill = false;
                    activeSkillCoroutine = null;

                    // Thông báo tới QuyVuongPhaseSwitcher rằng Phase 4 đã hoàn thành xong 1 lượt flow hành động!
                    if (!ct.IsCancellationRequested && !isFrozen && (bossHealth == null || !bossHealth.IsDead))
                    {
                        OnFlowCompleted?.Invoke();
                    }
                }
            }
        }

        private IEnumerator RunSkillCoroutine(QuyVuongPhase4SkillBase skill, TaskCompletionSource<bool> tcs)
        {
            if (skill != null && playerTransform != null)
            {
                yield return skill.ExecuteSkill(playerTransform);
            }
            tcs.TrySetResult(true);
        }

        private void StopActiveSkill()
        {
            if (activeSkillCoroutine != null)
            {
                StopCoroutine(activeSkillCoroutine);
                activeSkillCoroutine = null;
            }
        }

        private int SelectNextSkillIndex()
        {
            if (manualSkillOverride >= 0)
            {
                int overrideVal = manualSkillOverride;
                manualSkillOverride = -1;
                return overrideVal;
            }

            if (playerTransform == null) return 0;

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= meleeRangeThreshold)
            {
                // Tầm gần: 70% Skill 1 (Melee Skull Dash & Uppercut), 30% Skill 2 (Ranged Bombs)
                return UnityEngine.Random.value < 0.70f ? 0 : 1;
            }
            else
            {
                // Tầm xa: 70% Skill 2 (Ranged Bombs & Meteor Slam), 30% Skill 1 (Skull Dash áp sát)
                return UnityEngine.Random.value < 0.70f ? 1 : 0;
            }
        }

        private Transform GetRandomRelocationPoint()
        {
            if (arenaRelocationPoints == null || arenaRelocationPoints.Length == 0) return null;
            int idx = UnityEngine.Random.Range(0, arenaRelocationPoints.Length);
            return arenaRelocationPoints[idx];
        }

        private async Task DashToPositionAsync(Vector3 targetPos, float speed, CancellationToken ct)
        {
            bossPhase?.PlayMove();
            FacePlayer();

            while (Vector3.Distance(transform.position, targetPos) > 0.2f)
            {
                if (ct.IsCancellationRequested || isFrozen) return;

                FacePlayer();
                transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
                await Task.Yield();
            }

            transform.position = targetPos;
        }

        #region ContextMenu & Manual Triggers
        [ContextMenu("Trigger Skill 1: Liệt Địa Ma Trảo & Đầu Lâu Húc")]
        public void TriggerSkill1()
        {
            manualSkillOverride = 0;
            if (!isExecutingSkill) _ = ExecutePhase4BehaviorAsync();
        }

        [ContextMenu("Trigger Skill 2: Bom Dung Nham & Bổ Nện Đất")]
        public void TriggerSkill2()
        {
            manualSkillOverride = 1;
            if (!isExecutingSkill) _ = ExecutePhase4BehaviorAsync();
        }
        #endregion
    }
}
