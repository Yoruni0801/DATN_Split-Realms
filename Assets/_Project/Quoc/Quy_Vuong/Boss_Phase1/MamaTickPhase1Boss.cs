using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase1
{
    public class MamaTickPhase1Boss : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuyVuongBossPhase bossPhase;
        [SerializeField] private MamaTickArrow arrowPrefab;
        [SerializeField] private GameObject waterBeamPrefab;
        [SerializeField] private Transform arrowSpawnPoint;

        [Header("Distance Thresholds")]
        [SerializeField] private float rangedDistanceThreshold = 7f;

        [Header("Positions Setup (Kéo thả GameObject)")]
        [SerializeField] private Transform skyPosition; // Vị trí bay lên trời khi tấn công ở xa
        [SerializeField] private Transform[] arenaRelocationPoints; // Các vị trí bay đến sau khi tấn công ở gần
        [SerializeField] private Transform[] fixedWaterBeamPositions; // 3 (hoặc nhiều) vị trí cố định để triệu hồi Water Beam Static

        [Header("Rain Attack (Ở Xa) Settings")]
        [SerializeField] private int rainArrowCount = 8;
        [SerializeField] private float rainSpawnInterval = 0.35f;
        [SerializeField] private float rainWidth = 14f;
        [SerializeField] private float rainSpawnHeightOffset = 6f;
        [SerializeField] private float rainArrowSpeed = 14f;
        [SerializeField] private int rainArrowDamage = 15;

        [Header("Water Beam Burst Settings")]
        [SerializeField] private bool spawnWaterBeamsInRain = true;
        [SerializeField] private int beamBurstCount = 3;
        [SerializeField] private float beamDuration = 2.5f;
        [SerializeField] private int beamDamage = 20;

        [Header("Horizontal Attack (Ở Gần) Settings")]
        [SerializeField] private int horizontalArrowCount = 4;
        [SerializeField] private float horizontalInterval = 0.4f;
        [SerializeField] private float horizontalArrowSpeed = 16f;
        [SerializeField] private int horizontalArrowDamage = 18;
        [SerializeField] private float horizontalSpawnDistance = 10f;

        [Header("Movement & Cooldown")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float attackCooldown = 2.5f;

        [Header("Animation Settings")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string flyAnim = "Run";
        [SerializeField] private string attackAnim = "Skill";

        [Header("Particle Effects")]
        [SerializeField] private ParticleSystem shootVfxParticle;
        [SerializeField] private ParticleSystem chargeVfxParticle;

        private Transform playerTransform;
        private bool isExecutingCombo = false;

        private void Awake()
        {
            if (bossPhase == null)
            {
                bossPhase = GetComponent<QuyVuongBossPhase>();
            }
            if (bossPhase == null)
            {
                bossPhase = GetComponentInChildren<QuyVuongBossPhase>();
            }
        }

        private void Start()
        {
            FindPlayer();
            TeleportToSkyPosition();
        }

        private void OnEnable()
        {
            TeleportToSkyPosition();
        }

        private void TeleportToSkyPosition()
        {
            if (skyPosition != null)
            {
                transform.position = skyPosition.position;
            }
        }

        private void FindPlayer()
        {
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
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            if (isExecutingCombo) return;

            // Decision based on distance to player
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer > rangedDistanceThreshold)
            {
                _ = ExecuteFarDistanceComboAsync();
            }
            else
            {
                _ = ExecuteNearDistanceComboAsync();
            }
        }

        /// <summary>
        /// Combo ở xa: Bay lên trời và bắn các mũi tên mưa từ trên xuống đất
        /// </summary>
        private async Task ExecuteFarDistanceComboAsync()
        {
            isExecutingCombo = true;
            var ct = destroyCancellationToken;

            try
            {
                Debug.Log("[MamaTickPhase1Boss] Ở xa Player -> Bay lên trời thực hiện Rain Arrow Attack!");

                // 1. Bay lên vị trí trên trời (Sky Position)
                Vector3 targetSkyPos = skyPosition != null ? skyPosition.position : transform.position + new Vector3(0, 6f, 0);
                await FlyToPositionAsync(targetSkyPos, ct);
                if (this == null) return;

                // 2. Play Skill Animation khi ở trên trời & bật Particle Charge
                FacePlayer();
                if (chargeVfxParticle != null) chargeVfxParticle.Play();
                if (bossPhase != null)
                {
                    bossPhase.PlaySpineAnimation(attackAnim, false);
                }

                // 3. Bắn các mũi tên từ trên trời xuống xung quanh vị trí Player
                for (int i = 0; i < rainArrowCount; i++)
                {
                    if (this == null) return;

                    Vector3 spawnPos = GetRainSpawnPosition();
                    if (arrowPrefab != null)
                    {
                        MamaTickArrow arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
                        arrow.Initialize(Vector2.down, rainArrowSpeed, rainArrowDamage, transform);
                    }

                    if (shootVfxParticle != null) shootVfxParticle.Play();

                    int intervalMs = Mathf.RoundToInt(rainSpawnInterval * 1000f);
                    await Task.Delay(intervalMs, ct);
                }

                // 3.5 Triệu hồi đồng loạt các cột WaterBeamStatic xuống khu vực Player
                if (spawnWaterBeamsInRain && waterBeamPrefab != null)
                {
                    SpawnWaterBeamBurst();
                }

                if (chargeVfxParticle != null) chargeVfxParticle.Stop();

                // 4. Cooldown và chuyển lại trạng thái Idle
                if (bossPhase != null) bossPhase.PlaySpineAnimation(idleAnim, true);
                int cdMs = Mathf.RoundToInt(attackCooldown * 1000f);
                await Task.Delay(cdMs, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[MamaTickPhase1Boss] Lỗi ở Far Combo: {ex.Message}");
            }
            finally
            {
                if (this != null) isExecutingCombo = false;
            }
        }

        /// <summary>
        /// Combo ở gần: Bắn 4 mũi tên đan xen Trái <-> Phải, sau đó bay sang vị trí ngẫu nhiên
        /// </summary>
        private async Task ExecuteNearDistanceComboAsync()
        {
            isExecutingCombo = true;
            var ct = destroyCancellationToken;

            try
            {
                Debug.Log("[MamaTickPhase1Boss] Ở gần Player -> Bắn 4 mũi tên Trái/Phải đan xen!");

                // 1. Play Attack animation & bật Particle
                FacePlayer();
                if (chargeVfxParticle != null) chargeVfxParticle.Play();
                if (bossPhase != null)
                {
                    bossPhase.PlaySpineAnimation(attackAnim, false);
                }

                // 2. Bắn 4 mũi tên Trái sang Phải / Phải sang Trái đan xen
                bool shootFromLeft = true;
                for (int i = 0; i < horizontalArrowCount; i++)
                {
                    if (this == null) return;

                    Vector2 dir = shootFromLeft ? Vector2.right : Vector2.left;
                    Vector3 playerPos = playerTransform != null ? playerTransform.position : transform.position;
                    float offsetX = shootFromLeft ? -horizontalSpawnDistance : horizontalSpawnDistance;
                    Vector3 spawnPos = new Vector3(playerPos.x + offsetX, playerPos.y + UnityEngine.Random.Range(-0.5f, 1.5f), 0);

                    if (arrowPrefab != null)
                    {
                        MamaTickArrow arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
                        arrow.Initialize(dir, horizontalArrowSpeed, horizontalArrowDamage, transform);
                    }

                    if (shootVfxParticle != null) shootVfxParticle.Play();

                    shootFromLeft = !shootFromLeft; // Đổi hướng cho đạn tiếp theo

                    int intervalMs = Mathf.RoundToInt(horizontalInterval * 1000f);
                    await Task.Delay(intervalMs, ct);
                }

                if (chargeVfxParticle != null) chargeVfxParticle.Stop();

                // 3. Sau khi bắn đủ 4 mũi -> Bay đến 1 trong các vị trí arenaRelocationPoints được chọn
                if (arenaRelocationPoints != null && arenaRelocationPoints.Length > 0)
                {
                    Transform randomPoint = GetRandomRelocationPoint();
                    if (randomPoint != null)
                    {
                        Debug.Log($"[MamaTickPhase1Boss] Đã bắn xong 4 mũi đạn -> Bay đến vị trí: {randomPoint.name}");
                        await FlyToPositionAsync(randomPoint.position, ct);
                        if (this == null) return;
                    }
                }

                // 4. Cooldown và chuyển lại trạng thái Idle
                if (bossPhase != null) bossPhase.PlaySpineAnimation(idleAnim, true);
                int cdMs = Mathf.RoundToInt(attackCooldown * 1000f);
                await Task.Delay(cdMs, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[MamaTickPhase1Boss] Lỗi ở Near Combo: {ex.Message}");
            }
            finally
            {
                if (this != null) isExecutingCombo = false;
            }
        }

        private void SpawnWaterBeamBurst()
        {
            if (waterBeamPrefab == null) return;

            if (fixedWaterBeamPositions != null && fixedWaterBeamPositions.Length > 0)
            {
                // Triệu hồi tại các vị trí cố định đã thiết lập
                foreach (Transform fixedPos in fixedWaterBeamPositions)
                {
                    if (fixedPos == null) continue;
                    SpawnSingleWaterBeam(fixedPos.position);
                }
            }
            else
            {
                // Fallback nếu quên chưa thiết lập vị trí cố định -> spawn ngẫu nhiên quanh boss
                Vector3 center = transform.position;
                for (int i = 0; i < beamBurstCount; i++)
                {
                    float randomX = UnityEngine.Random.Range(-rainWidth * 0.5f, rainWidth * 0.5f);
                    Vector3 spawnPos = new(center.x + randomX, center.y, 0);
                    SpawnSingleWaterBeam(spawnPos);
                }
            }
        }

        private void SpawnSingleWaterBeam(Vector3 spawnPos)
        {
            GameObject beamObj = Instantiate(waterBeamPrefab, spawnPos, Quaternion.identity);

            // Add or configure WaterBeamHazard for damaging player
            if (!beamObj.TryGetComponent<WaterBeamHazard>(out var hazard))
            {
                hazard = beamObj.AddComponent<WaterBeamHazard>();
            }
            hazard.Initialize(beamDamage, beamDuration, transform);
        }

        private Vector3 GetRainSpawnPosition()
        {
            Vector3 center = playerTransform != null ? playerTransform.position : transform.position;
            float randomX = UnityEngine.Random.Range(-rainWidth * 0.5f, rainWidth * 0.5f);
            return new Vector3(center.x + randomX, center.y + rainSpawnHeightOffset, 0);
        }

        private Transform GetRandomRelocationPoint()
        {
            if (arenaRelocationPoints == null || arenaRelocationPoints.Length == 0) return null;
            int idx = UnityEngine.Random.Range(0, arenaRelocationPoints.Length);
            return arenaRelocationPoints[idx];
        }

        private async Task FlyToPositionAsync(Vector3 targetPos, System.Threading.CancellationToken ct)
        {
            if (bossPhase != null) bossPhase.PlaySpineAnimation(flyAnim, true);

            FaceDirection(targetPos.x - transform.position.x);

            while (Vector3.Distance(transform.position, targetPos) > 0.1f)
            {
                float dirX = targetPos.x - transform.position.x;
                FaceDirection(dirX);

                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                await Task.Yield();
                if (ct.IsCancellationRequested || this == null) return;
            }

            transform.position = targetPos;
        }

        public void FaceDirection(float directionX)
        {
            if (Mathf.Abs(directionX) > 0.15f)
            {
                Vector3 scale = transform.localScale;
                scale.x = directionX > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }

        public void FacePlayer()
        {
            if (playerTransform == null) return;
            FaceDirection(playerTransform.position.x - transform.position.x);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, rangedDistanceThreshold);

            if (skyPosition != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(skyPosition.position, 0.4f);
                Gizmos.DrawLine(transform.position, skyPosition.position);
            }

            if (arenaRelocationPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var p in arenaRelocationPoints)
                {
                    if (p != null)
                    {
                        Gizmos.DrawSphere(p.position, 0.4f);
                    }
                }
            }

            if (fixedWaterBeamPositions != null)
            {
                Gizmos.color = Color.blue;
                foreach (var p in fixedWaterBeamPositions)
                {
                    if (p != null)
                    {
                        Gizmos.DrawSphere(p.position, 0.5f);
                    }
                }
            }
        }
    }
}
