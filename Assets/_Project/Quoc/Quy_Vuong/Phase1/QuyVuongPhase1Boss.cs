using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Quoc.QuyVuong;
using Quoc.QuyVuong.Boss_Phase1;

namespace Quoc.QuyVuong.Phase1
{
    public class QuyVuongPhase1Boss : MonoBehaviour, IQuyVuongPhaseFlow
    {
        public event Action OnFlowCompleted;
        public bool IsExecutingFlow => isExecutingCombo;
        [Header("References")]
        [SerializeField] private QuyVuongBossPhase bossPhase;
        [SerializeField] private QuyVuongBossHealth bossHealth;

        [Header("=== RAIN ARROW VFX ===")]
        [SerializeField] private GameObject rainArrowWarningVFX;
        [SerializeField] private GameObject rainArrowProjectileVFX;
        [SerializeField] private GameObject rainArrowImpactVFX;
        [SerializeField] private Vector3 rainArrowRotationOffset = Vector3.zero;

        [Header("=== ENEMY SPAWN VFX ===")]
        [SerializeField] private GameObject enemySpawnWarningVFX;
        [SerializeField] private GameObject enemySpawnVFX;
        [SerializeField] private float enemySpawnWarningDuration = 0.5f;

        [Header("=== HORIZONTAL ARROW VFX ===")]
        [SerializeField] private ParticleSystem horizontalArrowChargeVFX;
        [SerializeField] private GameObject horizontalArrowProjectileVFX;
        [SerializeField] private GameObject horizontalArrowImpactVFX;
        [SerializeField] private Vector3 horizontalArrowRotationOffset = Vector3.zero;

        [Header("=== FLY VFX ===")]
        [SerializeField] private GameObject flyTakeOffVFX;
        [SerializeField] private ParticleSystem flyTrailVFX;
        [SerializeField] private GameObject flyLandingVFX;

        [Header("Arrow Settings (Exposed for Designer)")]
        [SerializeField] private GameObject arrowPrefab; // Prefab mũi tên
        [SerializeField] private ParticleSystem arrowParticleSystem; // Particle System hiệu ứng mũi tên
        [SerializeField] private GameObject arrowSpawnVfx;
        [SerializeField] private GameObject arrowWarningVfx;
        [SerializeField] private Transform arrowFirePoint;
        [SerializeField] private float rainArrowFallSpeed = 20f; // Tốc độ rơi của mũi tên mưa
        [SerializeField] private float arrowRainHeight = 8f;
        [SerializeField] private float arrowSpawnInterval = 0.35f;
        [SerializeField] private int rainArrowCount = 6;
        [SerializeField] private float rainArrowSpeed = 16f;
        [SerializeField] private int rainArrowDamage = 15;
        [SerializeField] private float warningDuration = 0.4f;

        [Header("Horizontal Arrow Settings")]
        [SerializeField] private int horizontalArrowCount = 4;
        [SerializeField] private float horizontalArrowInterval = 0.3f;
        [SerializeField] private float horizontalArrowSpeed = 16f;
        [SerializeField] private int horizontalArrowDamage = 18;
        [SerializeField] private float horizontalSpawnDistance = 10f;

        [Header("=== GRAPPLE ARROW SETTINGS ===")]
        [SerializeField] private GameObject grappleArrowPrefab;
        [SerializeField] private Transform bowStrapOriginPoint;
        [SerializeField] private LayerMask wallGrappleLayerMask;
        [SerializeField] private Vector3 grappleArrowRotationOffset = Vector3.zero;
        [SerializeField] private string grappleArrowAnim = "Skill";
        [SerializeField] private float grappleArrowSpeed = 20f;
        [SerializeField] private int grappleArrowDamage = 15;
        [SerializeField] private float grapplePullSpeed = 22f;
        [SerializeField] private GameObject grappleImpactVFX;
        [SerializeField] private GameObject bowCastVFXPrefab; // VFX hiệu ứng cung nước khi bắn (ví dụ: CungNuoc.prefab)
        [SerializeField] private Material grappleStrapMaterial; // Material hiệu ứng dòng nước cho sợi dây LineRenderer & TrailRenderer
        [SerializeField] private GameObject cagePrefab; // Prefab lồng giam Player
        [SerializeField] private float cageDuration = 2.5f; // Thời gian giam giữ Player trong lồng

        [Header("Enemy Spawn Settings")]
        [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>(); // Danh sách các loại enemy prefab khác nhau
        [SerializeField] private GameObject enemyPrefab; // Prefab đơn (fallback)
        [SerializeField] private List<GameObject> enemySpawnPoints = new List<GameObject>();
        [SerializeField] private int enemySpawnCount = 5;
        [SerializeField] private float enemySpawnDelay = 0.2f;

        [Header("Boss Movement & Positions")]
        [SerializeField] private float flySpeed = 12f;
        [SerializeField] private float flyHeight = 6f;
        [SerializeField] private List<GameObject> flyPositions = new List<GameObject>();
        [SerializeField] private float positionChangeDelay = 1.0f;

        [Header("=== RAM / CHARGE ATTACK SETTINGS ===")]
        [SerializeField] private float ramSpeed = 18f;
        [SerializeField] private int ramDamage = 20;
        [SerializeField] private float ramHitRadius = 1.5f;
        [SerializeField] private GameObject ramImpactVFX;

        [Header("Distance Thresholds")]
        [SerializeField] private float farDistance = 8f;
        [SerializeField] private float nearDistance = 4f;

        [Header("Animation Names")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string flyAnim = "Run";
        [SerializeField] private string rainArrowAnim = "Skill";
        [SerializeField] private string horizontalArrowAnim = "Skill";
        [SerializeField] private string flyToPositionAnim = "Run";
        [SerializeField] private string enemySpawnAnim = "Skill";

        [Header("Particle Effects / VFX")]
        [SerializeField] private ParticleSystem shootVfxParticle;
        [SerializeField] private ParticleSystem chargeVfxParticle;

        private Transform playerTransform;
        private bool isExecutingCombo = false;
        private bool isFlowCompleted = false;
        private bool isFrozen = false;
        private bool isMoving = false;
        private GameObject currentPositionObj = null;
        private readonly List<GameObject> activeSpawnedObjects = new List<GameObject>();
        private CancellationTokenSource comboCts;

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
            isFlowCompleted = false;
            isFrozen = false;
            currentPositionObj = null;

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
            QuyVuongWaterCage.ClearAll();
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
            if (shootVfxParticle != null) shootVfxParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (horizontalArrowChargeVFX != null) horizontalArrowChargeVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flyTrailVFX != null) flyTrailVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (arrowParticleSystem != null) arrowParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
            {
                if (activeSpawnedObjects[i] != null)
                {
                    Destroy(activeSpawnedObjects[i]);
                }
            }
            activeSpawnedObjects.Clear();

            var arrows = FindObjectsByType<QuyVuongPhase1Arrow>(FindObjectsSortMode.None);
            foreach (var a in arrows) if (a != null) Destroy(a.gameObject);

            var mamaArrows = FindObjectsByType<MamaTickArrow>(FindObjectsSortMode.None);
            foreach (var ma in mamaArrows) if (ma != null) Destroy(ma.gameObject);

            var grappleArrows = FindObjectsByType<QuyVuongGrappleArrow>(FindObjectsSortMode.None);
            foreach (var ga in grappleArrows) if (ga != null) Destroy(ga.gameObject);

            // Không tự động mở khóa player nếu player vẫn đang bị giam trong lồng nước QuyVuongWaterCage
            if (playerTransform != null && !QuyVuongWaterCage.HasActiveCageFor(playerTransform))
            {
                var inputLock = playerTransform.GetComponent<IPlayerInputLock>()
                    ?? playerTransform.GetComponentInChildren<IPlayerInputLock>()
                    ?? playerTransform.GetComponentInParent<IPlayerInputLock>();
                inputLock?.SetInputLocked(false);
            }
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

            // Luôn quay mặt về phía Player trong mọi frame
            FacePlayer();

            if (isExecutingCombo || isFlowCompleted) return;

            _ = ExecuteBossComboSequenceAsync();
        }

        #region Main Boss Combo Sequence
        /// <summary>
        /// Chuỗi hành động hoàn chỉnh của Boss Phase 1:
        /// 1. Nếu Player ở quá gần -> Chạy trốn ra vị trí xa Player.
        /// 2. Bắn Rain Arrow.
        /// 3. Bắn Horizontal Arrow.
        /// 4. Triệu hồi quái con.
        /// 5. Sau khi triệu hồi xong -> Di chuyển sang vị trí mới -> Nghỉ cooldown -> Lặp lại.
        /// </summary>
        private async Task ExecuteBossComboSequenceAsync()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;
            isExecutingCombo = true;

            try
            {
                comboCts?.Cancel();
                comboCts?.Dispose();
            }
            catch { }
            comboCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            var ct = comboCts.Token;

            try
            {
                // 1. Kiểm tra khoảng cách: Nếu Player ở gần -> Húc chạy sang vị trí xa Player
                float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
                if (distanceToPlayer < nearDistance)
                {
                    GameObject farPosObj = ChoosePositionFarFromPlayer();
                    if (farPosObj != null && Vector3.Distance(transform.position, farPosObj.transform.position) > 0.5f)
                    {
                        Debug.Log("[QuyVuongPhase1Boss] Player ở gần -> Húc sang vị trí xa Player!");
                        await MoveToPositionAsync(farPosObj.transform.position, flyToPositionAnim, ct);
                        if (this == null) return;
                    }
                }

                FacePlayer();

                // 2. Kỹ năng Rain Arrow
                Debug.Log("[QuyVuongPhase1Boss] Bắt đầu Rain Arrow!");
                if (chargeVfxParticle != null) chargeVfxParticle.Play();
                if (bossPhase != null)
                {
                    // Phát animation rainArrow với loop = true (nối tiếp từ đầu, không crossfade/revert lại pose cũ)
                    bossPhase.PlaySpineAnimation(rainArrowAnim, true, 1.0f, 0, false, 0f);
                }

                for (int i = 0; i < rainArrowCount; i++)
                {
                    if (this == null) return;
                    if (playerTransform == null) FindPlayer();

                    Vector3 targetPlayerPos = playerTransform != null ? playerTransform.position : transform.position;
                    Vector3 arrowSpawnPos = new Vector3(targetPlayerPos.x, targetPlayerPos.y + arrowRainHeight, 0f);

                    GameObject warningVfxToSpawn = rainArrowWarningVFX != null ? rainArrowWarningVFX : arrowWarningVfx;
                    if (warningVfxToSpawn != null)
                    {
                        GameObject warningFx = TrackSpawned(Instantiate(warningVfxToSpawn, targetPlayerPos, Quaternion.identity));
                        Destroy(warningFx, warningDuration + 0.2f);
                    }

                    if (warningDuration > 0f)
                    {
                        int warnMs = Mathf.RoundToInt(warningDuration * 1000f);
                        await Task.Delay(warnMs, ct);
                        if (this == null) return;
                    }

                    // Ưu tiên dùng arrowPrefab (prefab mũi tên thực tế) rơi từ trên cao xuống
                    GameObject arrowPrefabToUse = arrowPrefab != null ? arrowPrefab : rainArrowProjectileVFX;
                    float speedToUse = rainArrowFallSpeed > 0f ? rainArrowFallSpeed : rainArrowSpeed;
                    SpawnSingleArrow(Vector2.down, arrowSpawnPos, speedToUse, rainArrowDamage, arrowPrefabToUse, rainArrowImpactVFX, rainArrowRotationOffset);

                    if (arrowSpawnVfx != null)
                    {
                        TrackSpawned(Instantiate(arrowSpawnVfx, arrowSpawnPos, Quaternion.identity));
                    }

                    if (shootVfxParticle != null) shootVfxParticle.Play();

                    int intervalMs = Mathf.RoundToInt(arrowSpawnInterval * 1000f);
                    await Task.Delay(intervalMs, ct);
                }

                if (chargeVfxParticle != null) chargeVfxParticle.Stop();

                // 3. Kỹ năng Grapple Arrow (Bắn tên móc)
                await ExecuteGrappleArrowAsync(ct);
                if (this == null) return;

                // 4. Triệu hồi quái con (Spawn Enemy Group)
                await SpawnEnemyGroupAsync(ct);
                if (this == null) return;

                // 5. Kỹ năng Horizontal Arrow Barrage (Bắn tên ngang)
                Debug.Log("[QuyVuongPhase1Boss] Bắt đầu Horizontal Arrow Barrage!");
                FacePlayer();
                if (chargeVfxParticle != null) chargeVfxParticle.Play();
                if (horizontalArrowChargeVFX != null) horizontalArrowChargeVFX.Play();
                if (bossPhase != null)
                {
                    bossPhase.PlaySpineAnimation(horizontalArrowAnim, false);
                }

                for (int i = 0; i < horizontalArrowCount; i++)
                {
                    if (this == null) return;
                    if (playerTransform == null) FindPlayer();

                    // Xác định hướng bắn dựa theo hướng Boss đang nhìn Player (hoặc hướng Facing)
                    Vector2 direction = (playerTransform != null)
                        ? (playerTransform.position.x >= transform.position.x ? Vector2.right : Vector2.left)
                        : (transform.localScale.x > 0 ? Vector2.right : Vector2.left);

                    // Vị trí xuất phát mũi tên là vị trí của Boss (ưu tiên arrowFirePoint nếu có)
                    Vector3 spawnPos = arrowFirePoint != null ? arrowFirePoint.position : transform.position;
                    // Độ lệch Y nhỏ cho mỗi mũi tên tạo độ phủ ngang
                    spawnPos.y += UnityEngine.Random.Range(-0.5f, 1.5f);

                    SpawnSingleArrow(direction, spawnPos, horizontalArrowSpeed, horizontalArrowDamage, horizontalArrowProjectileVFX, horizontalArrowImpactVFX, horizontalArrowRotationOffset);

                    if (arrowSpawnVfx != null)
                    {
                        TrackSpawned(Instantiate(arrowSpawnVfx, spawnPos, Quaternion.identity));
                    }

                    if (shootVfxParticle != null) shootVfxParticle.Play();

                    int intervalMs = Mathf.RoundToInt(horizontalArrowInterval * 1000f);
                    await Task.Delay(intervalMs, ct);
                }

                if (chargeVfxParticle != null) chargeVfxParticle.Stop();
                if (horizontalArrowChargeVFX != null) horizontalArrowChargeVFX.Stop();

                // 6. Triệu hồi & bắn xong -> Di chuyển húc sang vị trí mới
                GameObject nextPosObj = ChooseNewPosition();
                if (nextPosObj != null)
                {
                    Debug.Log("[QuyVuongPhase1Boss] Đã xong chuỗi skill -> Húc sang vị trí mới!");
                    await MoveToPositionAsync(nextPosObj.transform.position, flyToPositionAnim, ct);
                    if (this == null) return;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[QuyVuongPhase1Boss] Lỗi ở Boss Combo Sequence: {ex.Message}");
            }
            finally
            {
                if (this != null)
                {
                    isExecutingCombo = false;
                    if (!ct.IsCancellationRequested && !isFrozen && (bossHealth == null || !bossHealth.IsDead))
                    {
                        isFlowCompleted = true;
                        OnFlowCompleted?.Invoke();
                    }
                }
            }
        }
        #endregion

        #region Grapple Arrow Skill
        private async Task ExecuteGrappleArrowAsync(CancellationToken ct)
        {
            if (grappleArrowPrefab == null) return;

            Debug.Log("[QuyVuongPhase1Boss] Bắt đầu Grapple Arrow!");
            FacePlayer();

            if (bossPhase != null && !string.IsNullOrEmpty(grappleArrowAnim))
            {
                bossPhase.PlaySpineAnimation(grappleArrowAnim, false);
            }

            Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
            if (playerTransform != null)
            {
                direction = (playerTransform.position - transform.position).normalized;
            }

            Transform spawnOrigin = bowStrapOriginPoint != null ? bowStrapOriginPoint : (arrowFirePoint != null ? arrowFirePoint : transform);
            Vector3 spawnPos = spawnOrigin.position;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0, 0, angle - 90f) * Quaternion.Euler(grappleArrowRotationOffset);

            // Sinh hiệu ứng VFX Cung Nước tại gốc điểm bắn/cây cung
            if (bowCastVFXPrefab != null)
            {
                GameObject bowVfx = TrackSpawned(Instantiate(bowCastVFXPrefab, spawnPos, rotation));
                bowVfx.transform.SetParent(spawnOrigin);
                Destroy(bowVfx, 3f);
            }

            GameObject arrowObj = TrackSpawned(Instantiate(grappleArrowPrefab, spawnPos, rotation));
            if (!arrowObj.TryGetComponent<QuyVuongGrappleArrow>(out var grappleArrow))
            {
                grappleArrow = arrowObj.AddComponent<QuyVuongGrappleArrow>();
            }

            grappleArrow.Initialize(direction, grappleArrowSpeed, grappleArrowDamage, transform, spawnOrigin, grappleImpactVFX, grappleArrowRotationOffset, wallGrappleLayerMask, grappleStrapMaterial, grappleStrapMaterial);

            if (shootVfxParticle != null) shootVfxParticle.Play();

            // Wait for arrow hit or destruction
            while (grappleArrow != null && grappleArrow.CurrentState == QuyVuongGrappleArrow.GrappleState.Flying)
            {
                await Task.Yield();
                if (ct.IsCancellationRequested || this == null)
                {
                    if (grappleArrow != null) Destroy(grappleArrow.gameObject);
                    return;
                }
            }

            if (grappleArrow == null) return;

            if (grappleArrow.CurrentState == QuyVuongGrappleArrow.GrappleState.HitWall)
            {
                // Pull Boss to target wall position
                Vector3 targetPos = grappleArrow.HitPoint;
                if (flyTrailVFX != null) flyTrailVFX.Play();
                isMoving = true;
                try
                {
                    FacePlayer();

                    while (Vector3.Distance(transform.position, targetPos) > 0.3f)
                    {
                        FacePlayer();
                        transform.position = Vector3.MoveTowards(transform.position, targetPos, grapplePullSpeed * Time.deltaTime);
                        grappleArrow.UpdateStrap(targetPos);
                        await Task.Yield();

                        if (ct.IsCancellationRequested || this == null)
                        {
                            if (flyTrailVFX != null) flyTrailVFX.Stop();
                            if (grappleArrow != null) Destroy(grappleArrow.gameObject);
                            return;
                        }
                    }

                    if (flyTrailVFX != null) flyTrailVFX.Stop();
                    Destroy(grappleArrow.gameObject);
                    FacePlayer();
                }
                finally
                {
                    isMoving = false;
                }
            }
            else if (grappleArrow.CurrentState == QuyVuongGrappleArrow.GrappleState.HitPlayer && grappleArrow.HitPlayerTransform != null)
            {
                // Pull Player to Boss position & lock input during pull
                Transform targetPlayer = grappleArrow.HitPlayerTransform;
                var inputLock = targetPlayer.GetComponent<IPlayerInputLock>()
                    ?? targetPlayer.GetComponentInChildren<IPlayerInputLock>()
                    ?? targetPlayer.GetComponentInParent<IPlayerInputLock>();

                inputLock?.SetInputLocked(true);

                Vector3 targetBossPos = transform.position;
                while (targetPlayer != null && Vector3.Distance(targetPlayer.position, targetBossPos) > 1.2f)
                {
                    targetPlayer.position = Vector3.MoveTowards(targetPlayer.position, targetBossPos, grapplePullSpeed * Time.deltaTime);
                    grappleArrow.UpdateStrap(targetPlayer.position);
                    await Task.Yield();

                    if (ct.IsCancellationRequested || this == null)
                    {
                        inputLock?.SetInputLocked(false);
                        if (grappleArrow != null) Destroy(grappleArrow.gameObject);
                        return;
                    }
                }

                // Khi kéo Player về sát vị trí Boss -> Sinh lồng giam Player lại (độc lập, không bị hủy khi boss chuyển phase)
                ApplyCageToPlayer(targetPlayer, inputLock);

                if (grappleArrow != null) Destroy(grappleArrow.gameObject);
            }
            else
            {
                if (grappleArrow != null) Destroy(grappleArrow.gameObject);
            }
        }

        private void ApplyCageToPlayer(Transform targetPlayer, IPlayerInputLock inputLock)
        {
            if (targetPlayer == null) return;

            if (cagePrefab != null)
            {
                // Không đưa vào activeSpawnedObjects để lồng nước không bị ClearAllSpawnedSkills() hủy khi đổi phase
                GameObject cageObj = Instantiate(cagePrefab, targetPlayer.position, Quaternion.identity);
                cageObj.transform.SetParent(targetPlayer);
                var waterCage = cageObj.GetComponent<QuyVuongWaterCage>() ?? cageObj.AddComponent<QuyVuongWaterCage>();
                waterCage.Initialize(targetPlayer, inputLock, cageDuration);
            }
            else
            {
                var fallbackCage = targetPlayer.gameObject.GetComponent<QuyVuongWaterCage>() ?? targetPlayer.gameObject.AddComponent<QuyVuongWaterCage>();
                fallbackCage.Initialize(targetPlayer, inputLock, cageDuration);
            }
        }
        #endregion

        #region Enemy Spawn
        private async Task SpawnEnemyGroupAsync(CancellationToken ct)
        {
            if (enemyPrefab == null && (enemyPrefabs == null || enemyPrefabs.Count == 0)) return;

            Debug.Log("[QuyVuongPhase1Boss] Rain & Horizontal Arrow kết thúc -> Spawn Enemy Con!");

            if (bossPhase != null && !string.IsNullOrEmpty(enemySpawnAnim))
            {
                bossPhase.PlaySpineAnimation(enemySpawnAnim, false);
            }

            List<Transform> availableSpawnPoints = GetAvailableEnemySpawnPoints();
            // Nếu muốn spawn nhiều hơn số điểm spawn, cho phép lặp lại/random spawn point
            int countToSpawn = enemySpawnCount;

            for (int i = 0; i < countToSpawn; i++)
            {
                if (this == null) return;

                // Xác định vị trí spawn
                Vector3 spawnPos;
                if (availableSpawnPoints.Count > 0)
                {
                    Transform selectedPoint = availableSpawnPoints[UnityEngine.Random.Range(0, availableSpawnPoints.Count)];
                    spawnPos = selectedPoint != null ? selectedPoint.position : transform.position;
                }
                else
                {
                    spawnPos = transform.position + new Vector3(UnityEngine.Random.Range(-5f, 5f), 0f, 0f);
                }

                // Chọn prefab enemy để spawn (từ danh sách enemyPrefabs hoặc enemyPrefab đơn)
                GameObject prefabToSpawn = enemyPrefab;
                if (enemyPrefabs != null && enemyPrefabs.Count > 0)
                {
                    prefabToSpawn = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
                }
                if (prefabToSpawn == null) continue;

                // 2.1 Warning VFX
                if (enemySpawnWarningVFX != null)
                {
                    GameObject warningFx = TrackSpawned(Instantiate(enemySpawnWarningVFX, spawnPos, Quaternion.identity));
                    Destroy(warningFx, enemySpawnWarningDuration + 0.2f);
                }

                if (enemySpawnWarningDuration > 0f)
                {
                    int warnMs = Mathf.RoundToInt(enemySpawnWarningDuration * 1000f);
                    await Task.Delay(warnMs, ct);
                }

                // 2.2 Spawn Enemy + VFX
                TrackSpawned(Instantiate(prefabToSpawn, spawnPos, Quaternion.identity));

                if (enemySpawnVFX != null)
                {
                    TrackSpawned(Instantiate(enemySpawnVFX, spawnPos, Quaternion.identity));
                }

                if (enemySpawnDelay > 0f)
                {
                    int delayMs = Mathf.RoundToInt(enemySpawnDelay * 1000f);
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        private List<Transform> GetAvailableEnemySpawnPoints()
        {
            List<Transform> list = new List<Transform>();
            if (enemySpawnPoints != null)
            {
                foreach (var obj in enemySpawnPoints)
                {
                    if (obj != null)
                    {
                        list.Add(obj.transform);
                    }
                }
            }
            return list;
        }
        #endregion

        #region Position & Movement System
        private GameObject ChoosePositionFarFromPlayer()
        {
            if (flyPositions == null || flyPositions.Count == 0 || playerTransform == null) return null;

            GameObject bestPos = null;
            float maxDist = -1f;

            foreach (var posObj in flyPositions)
            {
                if (posObj == null) continue;
                float dist = Vector2.Distance(posObj.transform.position, playerTransform.position);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    bestPos = posObj;
                }
            }

            if (bestPos != null) currentPositionObj = bestPos;
            return bestPos;
        }

        private GameObject ChooseNewPosition()
        {
            if (flyPositions == null || flyPositions.Count == 0) return null;

            List<GameObject> candidates = new List<GameObject>();
            foreach (var posObj in flyPositions)
            {
                if (posObj != null && posObj != currentPositionObj)
                {
                    candidates.Add(posObj);
                }
            }

            if (candidates.Count == 0)
            {
                currentPositionObj = flyPositions[0];
                return currentPositionObj;
            }

            int idx = UnityEngine.Random.Range(0, candidates.Count);
            currentPositionObj = candidates[idx];
            return currentPositionObj;
        }

        private async Task MoveToPositionAsync(Vector3 targetPos, string animName, CancellationToken ct)
        {
            if (flyTakeOffVFX != null)
            {
                Instantiate(flyTakeOffVFX, transform.position, Quaternion.identity);
            }

            if (flyTrailVFX != null) flyTrailVFX.Play();

            if (bossPhase != null && !string.IsNullOrEmpty(animName))
            {
                bossPhase.PlaySpineAnimation(animName, true);
            }

            // Danh sách các collider đã trúng chiêu húc trong đợt di chuyển này (tránh gây damage nhiều lần)
            HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();

            float moveSpeedToUse = ramSpeed > 0f ? ramSpeed : flySpeed;

            isMoving = true;
            try
            {
                FacePlayer();

                while (Vector3.Distance(transform.position, targetPos) > 0.1f)
                {
                    FacePlayer();

                    transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeedToUse * Time.deltaTime);

                    // Kỹ năng húc: Kiểm tra va chạm với Player trên đường di chuyển
                    Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ramHitRadius);
                    foreach (var col in hits)
                    {
                        if (col == null || hitColliders.Contains(col)) continue;

                        if (col.CompareTag("Player") || col.GetComponentInParent<PlayerHealth>() != null)
                        {
                            hitColliders.Add(col);
                            Vector2 moveDir = (targetPos - transform.position).normalized;
                            PlayerHitResolver.TryApplyHitToPlayer(
                                col,
                                ramDamage,
                                ParryableAttackType.Melee,
                                transform,
                                moveDir,
                                true
                            );

                            if (ramImpactVFX != null)
                            {
                                Instantiate(ramImpactVFX, col.transform.position, Quaternion.identity);
                            }
                        }
                    }

                    await Task.Yield();
                    if (ct.IsCancellationRequested || this == null)
                    {
                        if (flyTrailVFX != null) flyTrailVFX.Stop();
                        return;
                    }
                }

                transform.position = targetPos;

                if (flyTrailVFX != null) flyTrailVFX.Stop();

                if (flyLandingVFX != null)
                {
                    Instantiate(flyLandingVFX, transform.position, Quaternion.identity);
                }

                FacePlayer();
            }
            finally
            {
                isMoving = false;
            }
        }
        #endregion

        #region Arrow Spawn Helper
        private void SpawnSingleArrow(Vector2 direction, Vector3 spawnPos, float speed, int damage, GameObject projectileVfx = null, GameObject impactVfx = null, Vector3 customRotationOffset = default)
        {
            GameObject prefabToSpawn = projectileVfx != null ? projectileVfx : arrowPrefab;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, angle - 90f) * Quaternion.Euler(customRotationOffset);

            if (arrowParticleSystem != null)
            {
                arrowParticleSystem.transform.SetPositionAndRotation(spawnPos, targetRotation);
                arrowParticleSystem.Play();
            }

            if (prefabToSpawn != null)
            {
                GameObject projObj = TrackSpawned(Instantiate(prefabToSpawn, spawnPos, targetRotation));

                bool hasProjectileScript = false;

                if (projObj.TryGetComponent<QuyVuongPhase1Arrow>(out var arrow))
                {
                    arrow.Initialize(direction, speed, damage, transform, impactVfx);
                    projObj.transform.rotation = targetRotation;
                    hasProjectileScript = true;
                }
                else if (projObj.TryGetComponent<MamaTickArrow>(out var mamaArrow))
                {
                    mamaArrow.Initialize(direction, speed, damage, transform);
                    hasProjectileScript = true;
                }

                if (!hasProjectileScript)
                {
                    // Đảm bảo projectile bay đi được bằng cách tự động gắn QuyVuongPhase1Arrow nếu chưa có script di chuyển
                    arrow = projObj.AddComponent<QuyVuongPhase1Arrow>();
                    arrow.Initialize(direction, speed, damage, transform, impactVfx);
                    projObj.transform.rotation = targetRotation;
                }
            }
        }
        #endregion

        #region Utilities
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, farDistance);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, nearDistance);
        }
        #endregion
    }

    public class QuyVuongGrappleArrow : MonoBehaviour
    {
        public enum GrappleState { Idle, Flying, HitWall, HitPlayer }

        [Header("Settings")]
        [SerializeField] private int damage = 12;
        [SerializeField] private float speed = 18f;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private LayerMask customWallLayer;

        [Header("Strap / Line Renderer Settings")]
        [SerializeField] private LineRenderer strapLineRenderer;
        [SerializeField] private float strapWidth = 0.5f;
        [SerializeField] private Color strapColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Material strapMaterial;
        [SerializeField] private LineTextureMode strapTextureMode = LineTextureMode.Stretch;
        [SerializeField] private Vector2 strapTextureTiling = Vector2.one;
        [SerializeField] private Vector2 strapTextureOffset = Vector2.zero;
        [SerializeField] private Vector3 strapOriginOffset = Vector3.zero;

        [Header("Strap Trail Settings")]
        [SerializeField] private Material strapTrailMaterial;
        [SerializeField] private LineTextureMode trailTextureMode = LineTextureMode.Stretch;
        [SerializeField] private float trailTime = 0.4f;
        [SerializeField] private float trailWidthMultiplier = 1f;

        [Header("Visual Effects")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private ParticleSystem trailParticleSystem;
        [SerializeField] private TrailRenderer trailRenderer;

        private Vector2 moveDirection = Vector2.right;
        private Transform ownerTransform;
        private Transform strapOrigin;
        private GameObject customImpactVfx;

        private GrappleState state = GrappleState.Idle;
        private Vector3 hitPoint;
        private Transform hitPlayerTransform;

        public GrappleState CurrentState => state;
        public Vector3 HitPoint => hitPoint;
        public Transform HitPlayerTransform => hitPlayerTransform;

        public void Initialize(Vector2 direction, float customSpeed, int customDamage,
            Transform owner, Transform origin, GameObject impactVfx = null, Vector3 customRotationOffset = default, LayerMask customWallLayerMask = default, Material customStrapMaterial = null, Material customStrapTrailMaterial = null)
        {
            moveDirection = direction.normalized;
            speed = customSpeed;
            damage = customDamage;
            ownerTransform = owner;
            strapOrigin = origin;
            customImpactVfx = impactVfx;
            state = GrappleState.Flying;
            if (customWallLayerMask != 0) customWallLayer = customWallLayerMask;
            if (customStrapMaterial != null) strapMaterial = customStrapMaterial;
            if (customStrapTrailMaterial != null) strapTrailMaterial = customStrapTrailMaterial;

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f) * Quaternion.Euler(customRotationOffset);

            if (strapLineRenderer == null)
                strapLineRenderer = GetComponent<LineRenderer>();

            if (strapLineRenderer == null)
                strapLineRenderer = gameObject.AddComponent<LineRenderer>();

            if (strapLineRenderer != null)
            {
                strapLineRenderer.enabled = true;
                strapLineRenderer.positionCount = 2;
                strapLineRenderer.startWidth = strapWidth;
                strapLineRenderer.endWidth = strapWidth;
                strapLineRenderer.startColor = strapColor;
                strapLineRenderer.endColor = strapColor;
                strapLineRenderer.useWorldSpace = true;
                strapLineRenderer.textureMode = strapTextureMode;
                if (strapMaterial != null)
                {
                    strapLineRenderer.material = strapMaterial;
                    strapLineRenderer.material.mainTextureScale = strapTextureTiling;
                    strapLineRenderer.material.mainTextureOffset = strapTextureOffset;
                }
                else if (strapLineRenderer.material == null)
                {
                    strapLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                }
            }

            if (trailParticleSystem != null) trailParticleSystem.Play();

            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
                if (trailRenderer == null) trailRenderer = GetComponentInChildren<TrailRenderer>();
            }
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.enabled = true;
                trailRenderer.textureMode = trailTextureMode;
                trailRenderer.time = trailTime;
                trailRenderer.widthMultiplier = trailWidthMultiplier;
                if (strapTrailMaterial != null)
                {
                    trailRenderer.material = strapTrailMaterial;
                }
            }

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (state == GrappleState.Flying)
            {
                transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
                UpdateStrap(transform.position);
            }
            else if (state == GrappleState.HitWall)
            {
                UpdateStrap(hitPoint);
            }
            else if (state == GrappleState.HitPlayer && hitPlayerTransform != null)
            {
                UpdateStrap(hitPlayerTransform.position);
            }
        }

        public void UpdateStrap(Vector3 targetPos)
        {
            if (strapLineRenderer == null) return;
            Vector3 startPos = strapOrigin != null ? strapOrigin.position + strapOriginOffset
                             : ownerTransform != null ? ownerTransform.position + strapOriginOffset
                             : transform.position + strapOriginOffset;
            strapLineRenderer.SetPosition(0, startPos);
            strapLineRenderer.SetPosition(1, targetPos);
        }

        public void ClearStrap()
        {
            if (strapLineRenderer != null) strapLineRenderer.enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (state != GrappleState.Flying) return;
            if (ownerTransform != null && collision.transform.root == ownerTransform.root) return;
            if (PlayerHitResolver.IsThreatSensorCollider(collision)) return;

            if (collision.CompareTag("Player") || collision.GetComponentInParent<PlayerHealth>() != null)
            {
                hitPlayerTransform = collision.transform.root;
                PlayerHitResolver.TryApplyHitToPlayer(
                    collision, damage, ParryableAttackType.Projectile,
                    ownerTransform != null ? ownerTransform : transform,
                    moveDirection, true);
                state = GrappleState.HitPlayer;
                SpawnHitEffect();
                HideArrowHead();
                return;
            }

            LayerMask targetWallLayer = customWallLayer != 0 ? customWallLayer : PlayerLayerConfig.EnvironmentLayer;
            if (((1 << collision.gameObject.layer) & targetWallLayer) != 0)
            {
                hitPoint = transform.position;
                state = GrappleState.HitWall;
                SpawnHitEffect();
                HideArrowHead();
            }
        }

        private void HideArrowHead()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            if (trailParticleSystem != null) trailParticleSystem.Stop();
            if (trailRenderer != null) trailRenderer.enabled = false;
        }

        private void SpawnHitEffect()
        {
            GameObject fx = customImpactVfx != null ? customImpactVfx : hitVfxPrefab;
            if (fx != null) Instantiate(fx, transform.position, Quaternion.identity);
        }

        private void OnDestroy() => ClearStrap();
    }
}