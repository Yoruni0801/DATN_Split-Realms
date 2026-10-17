using System;
using System.Threading.Tasks;
using UnityEngine;
using Quoc.QuyVuong;
using Quoc.QuyVuong.Boss_Phase1;

namespace Quoc.QuyVuong.Boss_Phase2
{
    public enum BossRelocationMode
    {
        Random,      // Di chuyển ngẫu nhiên giữa các vị trí
        Sequential   // Di chuyển theo đúng thứ tự mảng (1 -> 2 -> 3 -> ...)
    }

    public class QuyVuongPhase2Boss : MonoBehaviour, IQuyVuongPhaseFlow
    {
        public event Action OnFlowCompleted;
        public bool IsExecutingFlow => isExecutingCombo;
        [Header("References")]
        [SerializeField] private QuyVuongBossPhase bossPhase;
        [SerializeField] private QuyVuongBossHealth bossHealth;

        [Header("Positions Setup (Kéo thả GameObject)")]
        [Tooltip("Thời gian đứng yên tại vị trí xuất hiện (vị trí phase trước) trước khi bắt đầu hành động / di chuyển (giây)")]
        [SerializeField] private float initialAppearDelay = 0.5f;
        [SerializeField] private BossRelocationMode relocationMode = BossRelocationMode.Random; // Chế độ chọn vị trí di chuyển
        [SerializeField] private Transform[] arenaRelocationPoints; // Các vị trí di chuyển/dash quanh arena
        [SerializeField] private Transform scytheComboDashPoint; // Vị trí cố định Boss sẽ dash tới khi dùng Scythe Slash (Để trống nếu muốn tự áp sát Player)

        [Header("Particle Effects (Kéo thả Prefab / Particle)")]
        [SerializeField] private GameObject scytheSlashParticlePrefab; // Particle Slash đánh vùng phía trước
        [SerializeField] private Transform scytheSlashSpawnPoint; // Vị trí xuất chiêu liềm
        [SerializeField] private Vector3 scytheSlashRotationOffset = Vector3.zero; // Độ lệch góc xoay cho Particle VFX (Chỉnh Inspector)
        [SerializeField] private ParticleSystem scytheSlashVfx; // Particle System cho đòn chém liềm (Expose Inspector)
        [SerializeField] private GameObject rangedProjectilePrefab; // Prefab đạn/triệu hồi tầm xa (Gắn QuyVuongHomingProjectile hoặc MamaTickArrow)
        [SerializeField] private Transform[] rangedSpawnPoints; // Danh sách các vị trí xuất chiêu đạn (Kéo thả GameObject)
        [SerializeField] private ParticleSystem chargeVfxParticle; // Eff sạc năng lượng khi chuẩn bị tung chiêu

        [Header("Normal Phase Settings")]
        [SerializeField] private float attackCooldown = 2.0f; // Thời gian recovery sau chuỗi combo
        [SerializeField] private float dashSpeed = 16f;
        [SerializeField] private float scytheSlashDelay = 0.3f; // Thời gian chờ (delay) trước khi xuất chiêu Scythe Slash (chỉnh Inspector)
        [SerializeField] private int scytheDamage = 25;
        [SerializeField] private float scytheAttackLength = 60f; // Đòn chém dài đến hết map
        [SerializeField] private float scytheAttackHeight = 3.5f; // Chiều cao đòn chém (để Player có thể nhảy né)
        [SerializeField] private int projectileCount = 6; // Số lượng đạn/triệu hồi (Tùy chỉnh số lượng trong Inspector)
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private int projectileDamage = 15;
        [SerializeField] private float projectileInterval = 0.25f;

        [Header("Aggressive Phase Settings (Tăng tốc & Giảm Recovery khi HP thấp)")]
        [SerializeField] private float aggressiveHpThreshold = 0.5f; // Ngưỡng HP (ví dụ 50%) để chuyển sang Aggressive
        [SerializeField] private float attackCooldownAggressive = 1.0f; // Recovery ngắn hơn ở phase aggressive
        [SerializeField] private float dashSpeedAggressive = 22f;
        [SerializeField] private int aggressiveBonusProjectile = 2; // Bắn thêm đạn khi aggressive

        [Header("Spine Animation Names")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string moveAnim = "Run";
        [SerializeField] private string scytheAttackAnim = "Attack";
        [SerializeField] private string rangedAttackAnim = "Skill";

        private Transform playerTransform;
        private bool isExecutingCombo = false;
        private bool isAggressive = false;
        private bool isFrozen = false;
        private bool isFirstActionAfterAppear = true;
        private int rangedComboCount = 0; // Đếm số lần đã thi triển chiêu Ranged
        private int currentRelocationIndex = 0; // Chỉ số vị trí di chuyển hiện tại khi ở chế độ Sequential
        private readonly System.Collections.Generic.List<GameObject> activeSpawnedObjects = new System.Collections.Generic.List<GameObject>();
        private System.Threading.CancellationTokenSource comboCts;

        private void Awake()
        {
            if (bossPhase == null)
            {
                bossPhase = GetComponent<QuyVuongBossPhase>() ?? GetComponentInChildren<QuyVuongBossPhase>();
            }

            if (bossHealth == null)
            {
                bossHealth = GetComponentInParent<QuyVuongBossHealth>() ?? GetComponent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void OnEnable()
        {
            isExecutingCombo = false;
            isAggressive = false;
            isFrozen = false;
            isFirstActionAfterAppear = true;
            currentRelocationIndex = 0;
            rangedComboCount = 0;

            if (bossHealth == null)
            {
                bossHealth = GetComponentInParent<QuyVuongBossHealth>() ?? GetComponent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }

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

        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                ClearAllSpawnedSkills();
                isExecutingCombo = false;
                bossPhase?.PlayIdle();
            }
        }

        private void HandleDied()
        {
            SetFrozen(true);
            ClearAllSpawnedSkills();
        }

        public void ClearAllSpawnedSkills()
        {
            try
            {
                comboCts?.Cancel();
                comboCts?.Dispose();
                comboCts = null;
            }
            catch { }

            if (chargeVfxParticle != null) chargeVfxParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (scytheSlashVfx != null) scytheSlashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
            {
                if (activeSpawnedObjects[i] != null)
                {
                    Destroy(activeSpawnedObjects[i]);
                }
            }
            activeSpawnedObjects.Clear();

            var homingProjs = FindObjectsByType<QuyVuongHomingProjectile>(FindObjectsSortMode.None);
            foreach (var p in homingProjs) if (p != null) Destroy(p.gameObject);

            var mamaArrows = FindObjectsByType<MamaTickArrow>(FindObjectsSortMode.None);
            foreach (var ma in mamaArrows) if (ma != null) Destroy(ma.gameObject);
        }

        private GameObject TrackSpawned(GameObject obj)
        {
            if (obj != null)
            {
                activeSpawnedObjects.Add(obj);
            }
            return obj;
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
            }
            else
            {
                var health = FindAnyObjectByType<PlayerHealth>();
                if (health != null)
                {
                    playerTransform = health.transform;
                }
                else
                {
                    var playerComp = FindAnyObjectByType<Player>();
                    if (playerComp != null)
                    {
                        playerTransform = playerComp.transform;
                    }
                }
            }
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

            if (isExecutingCombo) return;

            _ = ExecutePhase2BehaviorAsync();
        }

        private void CheckAggressiveState()
        {
            if (bossHealth != null)
            {
                float currentHpPercent = (float)bossHealth.CurrentHealth / bossHealth.MaxHealth;
                isAggressive = currentHpPercent <= aggressiveHpThreshold;
            }
        }

        /// <summary>
        /// Vòng lặp Combo của Boss Phase 2:
        /// 1. Dash đổi vị trí
        /// 2. Bắn 2 lần chiêu Ranged, sau đó sẽ dùng chiêu cận chiến Scythe Slash áp sát Player
        /// 3. Recovery (Khoảng nghỉ để Player phản công)
        /// </summary>
        private async Task ExecutePhase2BehaviorAsync()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;
            isExecutingCombo = true;

            try
            {
                comboCts?.Cancel();
                comboCts?.Dispose();
            }
            catch { }
            comboCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            var ct = comboCts.Token;
            bool completedFullCombo = false;

            try
            {
                // Khi vừa xuất hiện (tại vị trí phase trước): Đứng yên trong initialAppearDelay trước khi di chuyển / ra đòn
                if (isFirstActionAfterAppear)
                {
                    isFirstActionAfterAppear = false;
                    FacePlayer();
                    if (bossPhase != null)
                    {
                        bossPhase.PlaySpineAnimation(idleAnim, true);
                    }
                    if (initialAppearDelay > 0f)
                    {
                        int delayMs = Mathf.RoundToInt(initialAppearDelay * 1000f);
                        await Task.Delay(delayMs, ct);
                        if (this == null || isFrozen) return;
                    }
                }

                // Xoay mặt về hướng Player
                FacePlayer();

                // Quy trình combo: Dùng đòn Ranged đủ 2 lần -> tung chiêu cận chiến Scythe Slash áp sát Player
                if (rangedComboCount >= 2)
                {
                    rangedComboCount = 0; // Reset đếm
                    await ExecuteScytheComboAsync(ct);
                    completedFullCombo = true; // Đã thi triển xong trọn vẹn chuỗi combo (2 Ranged + 1 Scythe Slash)
                }
                else
                {
                    rangedComboCount++;
                    await ExecuteRangedComboAsync(ct);
                }

                if (this == null) return;

                // Sau combo: Dash đổi vị trí quanh arena (theo mode chọn)
                if (arenaRelocationPoints != null && arenaRelocationPoints.Length > 0)
                {
                    Transform targetPoint = GetNextRelocationPoint();
                    if (targetPoint != null)
                    {
                        float speed = isAggressive ? dashSpeedAggressive : dashSpeed;
                        await DashToPositionAsync(targetPoint.position, speed, ct);
                        if (this == null) return;
                    }
                }

                // Xoay mặt lại về phía Player sau khi Dash
                FacePlayer();

                // Chuyển về trạng thái Recovery (Idle)
                if (bossPhase != null)
                {
                    bossPhase.PlaySpineAnimation(idleAnim, true);
                }

                // Thời gian recovery (ngắn hơn khi Aggressive)
                float currentCooldown = isAggressive ? attackCooldownAggressive : attackCooldown;
                int recoveryMs = Mathf.RoundToInt(currentCooldown * 1000f);
                await Task.Delay(recoveryMs, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[QuyVuongPhase2Boss] Lỗi ở Phase 2 Combo: {ex.Message}");
            }
            finally
            {
                if (this != null)
                {
                    isExecutingCombo = false;
                    if (!ct.IsCancellationRequested && !isFrozen && (bossHealth == null || !bossHealth.IsDead) && completedFullCombo)
                    {
                        OnFlowCompleted?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Đòn chém liềm (Scythe Slash): Dash tới vị trí chỉ định (hoặc áp sát Player) và chém vùng rộng phía trước
        /// </summary>
        private async Task ExecuteScytheComboAsync(System.Threading.CancellationToken ct)
        {
            Debug.Log($"[QuyVuongPhase2Boss] Executing Scythe Slash Combo! (Aggressive: {isAggressive})");

            // Nếu người dùng chỉ định điểm Scythe Combo Dash Point cố định thì dùng, không thì tự động áp sát Player
            Vector3 dashTargetPos = scytheComboDashPoint != null ? scytheComboDashPoint.position : GetPositionNearPlayer();
            float speed = isAggressive ? dashSpeedAggressive : dashSpeed;
            await DashToPositionAsync(dashTargetPos, speed, ct);
            if (this == null) return;

            FacePlayer();

            // Play Effect Charge nếu có
            if (chargeVfxParticle != null) chargeVfxParticle.Play();

            // Play Attack Animation
            if (bossPhase != null)
            {
                bossPhase.PlaySpineAnimation(scytheAttackAnim, false);
            }

            // Chờ thời gian delay được cấu hình trong Inspector trước khi xuất chiêu (VFX & Damage)
            if (scytheSlashDelay > 0f)
            {
                int delayMs = Mathf.RoundToInt(scytheSlashDelay * 1000f);
                await Task.Delay(delayMs, ct);
                if (this == null) return;
            }

            // Instanitate Particle Slash (Đòn đánh vùng hướng thẳng về vị trí Player)
            Vector3 slashSpawnPos = scytheSlashSpawnPoint != null ? scytheSlashSpawnPoint.position : transform.position;

            // Tính góc quay hướng thẳng về phía Player
            Quaternion targetRotation;
            if (playerTransform != null)
            {
                Vector2 dirToPlayer = (playerTransform.position - slashSpawnPos).normalized;
                float angle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
                targetRotation = Quaternion.Euler(0, 0, angle) * Quaternion.Euler(scytheSlashRotationOffset);
            }
            else
            {
                float dirX = transform.localScale.x > 0 ? 0f : 180f;
                targetRotation = Quaternion.Euler(0, 0, dirX) * Quaternion.Euler(scytheSlashRotationOffset);
            }

            if (scytheSlashVfx != null)
            {
                scytheSlashVfx.transform.rotation = targetRotation;
                scytheSlashVfx.Play();
            }

            if (scytheSlashParticlePrefab != null)
            {
                GameObject slashFx = TrackSpawned(Instantiate(scytheSlashParticlePrefab, slashSpawnPos, targetRotation));
                slashFx.transform.SetParent(transform);
            }

            // Gây sát thương nếu player ở trong tầm chém hình chữ nhật (dài vô tận về phía trước, giới hạn chiều cao)
            if (playerTransform != null)
            {
                float directionX = transform.localScale.x > 0 ? 1f : -1f;
                Vector3 playerDir = playerTransform.position - transform.position;

                // Kiểm tra xem Player có đang đứng ở phía trước mặt Boss không
                bool isInFront = Mathf.Sign(playerDir.x) == Mathf.Sign(directionX);

                // Kiểm tra khoảng cách: Nằm trong khoảng chiều dài (X) và chiều cao (Y)
                bool isWithinX = Mathf.Abs(playerDir.x) <= scytheAttackLength;
                bool isWithinY = Mathf.Abs(playerDir.y) <= scytheAttackHeight / 2f; // Tính từ tâm Boss lên/xuống

                if (isInFront && isWithinX && isWithinY)
                {
                    ApplyDamageToPlayer(scytheDamage);
                }
            }

            await Task.Delay(400, ct);

            if (chargeVfxParticle != null) chargeVfxParticle.Stop();
        }

        /// <summary>
        /// Đòn tầm xa / triệu hồi (Projectile / Summon): Triệu hồi 1 đợt đạn tại tất cả các vị trí trong mảng Array rangedSpawnPoints
        /// </summary>
        private async Task ExecuteRangedComboAsync(System.Threading.CancellationToken ct)
        {
            Debug.Log($"[QuyVuongPhase2Boss] Executing Ranged / Summon Combo Burst across Array points! (Aggressive: {isAggressive})");

            FacePlayer();

            if (chargeVfxParticle != null) chargeVfxParticle.Play();

            if (bossPhase != null)
            {
                bossPhase.PlaySpineAnimation(rangedAttackAnim, false);
            }

            // Nếu mảng vị trí rangedSpawnPoints được thiết lập trong Inspector
            if (rangedSpawnPoints != null && rangedSpawnPoints.Length > 0)
            {
                // Duyệt qua tất cả các vị trí trong Array rangedSpawnPoints
                foreach (Transform spawnPoint in rangedSpawnPoints)
                {
                    if (this == null) return;
                    if (spawnPoint == null) continue;

                    SpawnSingleRangedProjectile(spawnPoint.position);

                    // Nếu muốn xuất hiện nối tiếp nhanh thay vì cùng 1 khung hình
                    if (projectileInterval > 0f)
                    {
                        int intervalMs = Mathf.RoundToInt(projectileInterval * 1000f);
                        await Task.Delay(intervalMs, ct);
                    }
                }

                // Khi Aggressive mode: có thể bắn thêm 1 lượt nữa từ các vị trí ngẫu nhiên
                if (isAggressive && aggressiveBonusProjectile > 0)
                {
                    for (int i = 0; i < aggressiveBonusProjectile; i++)
                    {
                        if (this == null) return;

                        Transform randomPoint = rangedSpawnPoints[UnityEngine.Random.Range(0, rangedSpawnPoints.Length)];
                        if (randomPoint != null)
                        {
                            SpawnSingleRangedProjectile(randomPoint.position);
                        }

                        if (projectileInterval > 0f)
                        {
                            int intervalMs = Mathf.RoundToInt(projectileInterval * 1000f);
                            await Task.Delay(intervalMs, ct);
                        }
                    }
                }
            }
            else
            {
                // Fallback nếu chưa kéo mảng array -> Bắn tại vị trí Boss
                int totalCount = isAggressive ? projectileCount + aggressiveBonusProjectile : projectileCount;
                for (int i = 0; i < totalCount; i++)
                {
                    if (this == null) return;
                    SpawnSingleRangedProjectile(transform.position);

                    if (projectileInterval > 0f)
                    {
                        int intervalMs = Mathf.RoundToInt(projectileInterval * 1000f);
                        await Task.Delay(intervalMs, ct);
                    }
                }
            }

            if (chargeVfxParticle != null) chargeVfxParticle.Stop();
        }

        private void SpawnSingleRangedProjectile(Vector3 spawnPos)
        {
            if (rangedProjectilePrefab == null) return;

            GameObject projObj = TrackSpawned(Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity));

            if (playerTransform != null)
            {
                Vector2 direction = (playerTransform.position - spawnPos).normalized;

                // Khởi tạo cho Homing Projectile đuổi theo player và có thể Parry
                if (projObj.TryGetComponent<QuyVuongHomingProjectile>(out var homingProj))
                {
                    homingProj.Initialize(direction, projectileSpeed, projectileDamage, transform, playerTransform);
                }
                // Hoặc đạn thường (MamaTickArrow)
                else if (projObj.TryGetComponent<MamaTickArrow>(out var arrow))
                {
                    arrow.Initialize(direction, projectileSpeed, projectileDamage, transform);
                }
                else if (projObj.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity = direction * projectileSpeed;
                }
            }
        }

        private void ApplyDamageToPlayer(int damage)
        {
            if (playerTransform == null) return;

            // Tìm Collider2D trên Player hoặc các con của Player (ví dụ Hitbox/Main Body trong PlayerBase.prefab)
            if (!playerTransform.TryGetComponent<Collider2D>(out var playerCollider))
            {
                playerCollider = playerTransform.GetComponentInChildren<Collider2D>();
            }

            if (playerCollider != null)
            {
                Vector3 attackDir = (playerTransform.position - transform.position).normalized;
                PlayerHitResolver.TryApplyHitToPlayer(
                    playerCollider,
                    damage,
                    ParryableAttackType.Melee,
                    transform,
                    attackDir
                );
            }
            else
            {
                // Fallback nếu không thấy Collider2D
                if (!playerTransform.TryGetComponent<PlayerHealth>(out var playerHealth))
                {
                    playerHealth = playerTransform.GetComponentInChildren<PlayerHealth>();
                }

                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, transform);
                }
            }
        }

        public void FaceDirection(float directionX)
        {
            if (Mathf.Abs(directionX) > 0.05f)
            {
                Vector3 scale = transform.localScale;
                scale.x = directionX > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;

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

        private Vector3 GetPositionNearPlayer()
        {
            if (playerTransform == null) return transform.position;

            float offsetDirection = (playerTransform.position.x >= transform.position.x) ? -2.0f : 2.0f;
            return new Vector3(playerTransform.position.x + offsetDirection, playerTransform.position.y, 0f);
        }

        private Transform GetNextRelocationPoint()
        {
            if (arenaRelocationPoints == null || arenaRelocationPoints.Length == 0) return null;

            if (relocationMode == BossRelocationMode.Sequential)
            {
                if (currentRelocationIndex >= arenaRelocationPoints.Length)
                {
                    currentRelocationIndex = 0;
                }
                Transform point = arenaRelocationPoints[currentRelocationIndex];
                currentRelocationIndex = (currentRelocationIndex + 1) % arenaRelocationPoints.Length;
                return point;
            }
            else
            {
                // Chọn điểm ngẫu nhiên nhưng tránh điểm trùng với vị trí hiện tại nếu mảng có nhiều hơn 1 điểm
                if (arenaRelocationPoints.Length > 1)
                {
                    int idx;
                    int attempts = 0;
                    do
                    {
                        idx = UnityEngine.Random.Range(0, arenaRelocationPoints.Length);
                        attempts++;
                    }
                    while (arenaRelocationPoints[idx] != null && 
                           Vector3.Distance(transform.position, arenaRelocationPoints[idx].position) < 1.5f && 
                           attempts < 10);

                    return arenaRelocationPoints[idx];
                }
                else
                {
                    return arenaRelocationPoints[0];
                }
            }
        }

        /// <summary>
        /// Reset lại thứ tự di chuyển arena points về điểm đầu tiên (index 0).
        /// </summary>
        public void ResetRelocationSequence()
        {
            currentRelocationIndex = 0;
            isFirstActionAfterAppear = true;
        }

        private async Task DashToPositionAsync(Vector3 targetPos, float speed, System.Threading.CancellationToken ct)
        {
            if (bossPhase != null)
            {
                bossPhase.PlaySpineAnimation(moveAnim, true);
            }

            FacePlayer();

            while (Vector3.Distance(transform.position, targetPos) > 0.1f)
            {
                FacePlayer();

                transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
                await Task.Yield();
                if (ct.IsCancellationRequested || this == null) return;
            }

            transform.position = targetPos;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            // Vẽ hộp chữ nhật đại diện cho đòn chém
            Vector3 centerOffset = new Vector3((scytheAttackLength / 2f) * (transform.localScale.x > 0 ? 1 : -1), 0, 0);
            Vector3 hitboxSize = new Vector3(scytheAttackLength, scytheAttackHeight, 1f);
            Gizmos.DrawWireCube(transform.position + centerOffset, hitboxSize);

            if (arenaRelocationPoints != null)
            {
                Gizmos.color = Color.magenta;
                foreach (var p in arenaRelocationPoints)
                {
                    if (p != null)
                    {
                        Gizmos.DrawSphere(p.position, 0.4f);
                    }
                }
            }

            if (scytheSlashSpawnPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(scytheSlashSpawnPoint.position, 0.2f);
            }

            if (rangedSpawnPoints != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var p in rangedSpawnPoints)
                {
                    if (p != null)
                    {
                        Gizmos.DrawSphere(p.position, 0.2f);
                    }
                }
            }
        }
    }
}
