using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Quoc.QuyVuong;

namespace Quoc.QuyVuong.Boss_Phase3
{
    public class QuyVuongPhase3Boss : MonoBehaviour, IQuyVuongPhaseFlow
    {
        public event Action OnFlowCompleted;
        public bool IsExecutingFlow => isExecutingSkill;

        [Header("References")]
        [SerializeField] private QuyVuongBossPhase bossPhase;
        [SerializeField] private QuyVuongBossHealth bossHealth;
        [SerializeField] private BoxCollider2D hurtbox;

        [Header("Arena")]
        [SerializeField] private Transform[] arenaRelocationPoints;

        [Header("Animation")]
        [SerializeField] private string idleAnim = "Idle";
        [SerializeField] private string moveAnim = "Run";
        [SerializeField] private string groundLineAnim = "Attack";
        [SerializeField] private string skyLeapAnim = "Gap closer";
        [SerializeField] private string skyLeapImpactAnim = "Attack2";

        [Header("Skill 1 - Ground Line")]
        [SerializeField] private GameObject groundLinePrefab;
        [SerializeField] private Transform groundLineSpawnPoint;
        [SerializeField] private float groundLineSpeed = 16f;
        [SerializeField] private int groundLineDamage = 20;
        [SerializeField] private float groundLineWindupDuration = 0.45f;
        [SerializeField] private GameObject dusthornPrefab;
        [SerializeField] private int maxActiveDusthorns = 2;
        [SerializeField] private LayerMask environmentLayers;

        [Header("Skill 2 - Sky Leap & Spear Meteor Slam")]
        [SerializeField] private float skyLeapDistanceThreshold = 6f;
        [SerializeField] private float skyLeapChargeDuration = 0.18f;
        [SerializeField] private float skyLeapTrackingDuration = 0.36f;
        [SerializeField] private float skyLeapLockDuration = 0.24f;
        [Tooltip("Độ cao (Y offset) mà boss sẽ nhảy lên không trung trước khi lao cắm thương xuống.")]
        [Min(1f)]
        [SerializeField] private float skyLeapHeight = 4.8f;
        [Tooltip("Thời gian bay lên tới đỉnh (giây)")]
        [SerializeField] private float skyLeapAscentDuration = 0.32f;
        [Tooltip("Thời gian lao cắm thương từ trên không xuống đất (giây)")]
        [SerializeField] private float skyLeapDescentDuration = 0.18f;
        [SerializeField] private int skyLeapDamage = 32;
        [SerializeField] private float skyLeapImpactRadius = 2.4f;
        [Tooltip("Bù trừ Y khi boss đáp xuống: raycast trả về bề mặt sàn, cộng thêm offset này để pivot của boss nổi đúng vị trí bình thường thay vì bị cắm xuống đất. Tăng nếu boss vẫn lún, giảm nếu boss đứng lơ lửng.")]
        [SerializeField] private float skyLeapLandingOffset = 1.3f;

        [Header("Skill 2 - Ground Resolution")]
        [SerializeField] private float skyLeapGroundProbeHeight = 8f;
        [SerializeField] private float skyLeapGroundProbeDistance = 24f;
        [SerializeField] private float skyLeapGroundSearchStep = 0.55f;
        [SerializeField] private int skyLeapGroundSearchAttempts = 12;

        [Header("Skill 2 - Dedicated Shockwaves")]

        [Header("Skill 2 - Audio & Camera")]
        [SerializeField] private AudioClip skyLeapChargeSfx;
        [SerializeField] private AudioClip skyLeapDescentSfx;
        [SerializeField] private AudioClip skyLeapImpactSfx;
        [SerializeField] private float impactCameraShakeDuration = 0.22f;
        [SerializeField] private float impactCameraShakeMagnitude = 0.18f;

        [Header("Recovery")]
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float postImpactDelay = 0.72f;

        private Transform playerTransform;
        private bool isExecutingSkill;
        private bool isFrozen;
        private int lastSkillIndex = -1;
        private int manualSkillOverride = -1;
        private readonly List<GameObject> spawnedDusthorns = new();
        private readonly List<GameObject> activeSpawnedObjects = new();
        private CancellationTokenSource skillCts;

        private enum MovementEase
        {
            Linear,
            EaseInCubic,
            EaseOutCubic
        }

        public void TriggerSkill1() => manualSkillOverride = 0;
        public void TriggerSkill2() => manualSkillOverride = 1;

        private void Awake()
        {
            if (bossPhase == null) bossPhase = GetComponent<QuyVuongBossPhase>() ?? GetComponentInChildren<QuyVuongBossPhase>();
            if (bossHealth == null) bossHealth = GetComponentInParent<QuyVuongBossHealth>() ?? GetComponent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            if (hurtbox == null) hurtbox = GetComponent<BoxCollider2D>();
        }

        private void Start() => FindPlayer();

        private void OnEnable()
        {
            isExecutingSkill = false;
            isFrozen = false;
            lastSkillIndex = -1;

            if (bossHealth == null)
            {
                bossHealth = GetComponentInParent<QuyVuongBossHealth>() ?? GetComponent<QuyVuongBossHealth>() ?? GetComponentInChildren<QuyVuongBossHealth>();
            }

            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
                bossHealth.OnDied += HandleDied;
            }

            RelocateToRandomPointInstant();
        }

        private void OnDisable()
        {
            if (hurtbox != null) hurtbox.enabled = true;
            if (bossHealth != null)
            {
                bossHealth.OnDied -= HandleDied;
            }
            ClearAllSpawnedSkills();
        }

        /// <summary>
        /// Đóng băng/mở khóa Boss: Dừng di chuyển, dừng target Player, dừng tấn công khi chuyển phase.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                ClearAllSpawnedSkills();
                isExecutingSkill = false;
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
                skillCts?.Cancel();
                skillCts?.Dispose();
                skillCts = null;
            }
            catch { }

            if (hurtbox != null) hurtbox.enabled = true;

            for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
            {
                if (activeSpawnedObjects[i] != null) Destroy(activeSpawnedObjects[i]);
            }
            activeSpawnedObjects.Clear();

            for (int i = spawnedDusthorns.Count - 1; i >= 0; i--)
            {
                if (spawnedDusthorns[i] != null) Destroy(spawnedDusthorns[i]);
            }
            spawnedDusthorns.Clear();

            var groundLines = FindObjectsByType<GroundLineHazard>(FindObjectsSortMode.None);
            foreach (var gl in groundLines) if (gl != null) Destroy(gl.gameObject);

            var earthEruptions = FindObjectsByType<EarthEruptionHazard>(FindObjectsSortMode.None);
            foreach (var ee in earthEruptions) if (ee != null) Destroy(ee.gameObject);

            var indicators = FindObjectsByType<SkyLeapTelegraphIndicator>(FindObjectsSortMode.None);
            foreach (var ind in indicators) if (ind != null) Destroy(ind.gameObject);

            var thrustHitboxes = FindObjectsByType<SpearThrustHitbox>(FindObjectsSortMode.None);
            foreach (var th in thrustHitboxes) if (th != null) Destroy(th.gameObject);

            var impactVfxs = FindObjectsByType<SkyLeapImpactVfx>(FindObjectsSortMode.None);
            foreach (var iv in impactVfxs) if (iv != null) Destroy(iv.gameObject);
        }

        private GameObject TrackSpawned(GameObject obj)
        {
            if (obj != null)
            {
                activeSpawnedObjects.Add(obj);
            }
            return obj;
        }

        private void Update()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;
            if (playerTransform == null) FindPlayer();
            if (playerTransform == null) return;

            // Luôn quay mặt về phía Player trong mọi frame (kể cả khi đang cast skill)
            FacePlayer();

            if (isExecutingSkill) return;
            _ = ExecuteBehaviorAsync();
        }

        private async Task ExecuteBehaviorAsync()
        {
            if (isFrozen || (bossHealth != null && bossHealth.IsDead)) return;
            isExecutingSkill = true;

            try
            {
                skillCts?.Cancel();
                skillCts?.Dispose();
            }
            catch { }
            skillCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken cancellationToken = skillCts.Token;

            try
            {
                FacePlayer();
                int selectedSkill = SelectNextSkill();
                lastSkillIndex = selectedSkill;

                if (selectedSkill == 0)
                {
                    await ExecuteGroundLineSkillAsync(cancellationToken);
                }
                else
                {
                    await ExecuteSkyLeapSkillAsync(cancellationToken);
                }

                if (this == null || cancellationToken.IsCancellationRequested) return;
                await RelocateAfterSkillAsync(cancellationToken);
                if (this == null || cancellationToken.IsCancellationRequested) return;

                bossPhase?.PlaySpineAnimation(idleAnim, true);
                await Task.Delay(Mathf.Max(0, Mathf.RoundToInt(attackCooldown * 1000f)), cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogError($"[QuyVuongPhase3Boss] Skill flow failed:\n{exception}");
            }
            finally
            {
                if (this != null)
                {
                    isExecutingSkill = false;
                    if (hurtbox != null) hurtbox.enabled = true;
                    if (!cancellationToken.IsCancellationRequested && !isFrozen && (bossHealth == null || !bossHealth.IsDead))
                    {
                        OnFlowCompleted?.Invoke();
                    }
                }
            }
        }

        private int SelectNextSkill()
        {
            if (manualSkillOverride >= 0)
            {
                int overrideSkill = manualSkillOverride;
                manualSkillOverride = -1;
                return overrideSkill;
            }

            if (playerTransform != null && lastSkillIndex != 1 &&
                Vector2.Distance(transform.position, playerTransform.position) > skyLeapDistanceThreshold)
            {
                return 1;
            }

            return lastSkillIndex == 0 ? 1 : 0;
        }

        private async Task ExecuteGroundLineSkillAsync(CancellationToken cancellationToken)
        {
            FacePlayer();
            bossPhase?.PlaySpineAnimation(groundLineAnim, false);

            Vector3 spawnPosition = groundLineSpawnPoint != null
                ? groundLineSpawnPoint.position
                : (bossPhase != null ? bossPhase.GroundPoint.position : transform.position);
            float directionX = playerTransform != null
                ? Mathf.Sign(playerTransform.position.x - spawnPosition.x)
                : Mathf.Sign(transform.localScale.x);
            if (Mathf.Approximately(directionX, 0f)) directionX = 1f;

            // Giữ nhịp wind-up của animation nhưng không vẽ đường cảnh báo dài.
            await Task.Delay(Mathf.RoundToInt(groundLineWindupDuration * 1000f), cancellationToken);
            if (this == null || cancellationToken.IsCancellationRequested) return;

            SpawnGroundLine(spawnPosition, directionX);
            await Task.Delay(350, cancellationToken);
        }

        private async Task ExecuteSkyLeapSkillAsync(CancellationToken cancellationToken)
        {
            FacePlayer();
            bossPhase?.PlaySpineAnimation(skyLeapAnim, false);

            Vector3 takeoffPosition = transform.position;
            // fallbackGroundY là vị trí Y pivot của boss khi đứng trên sàn (đã bao gồm offset pivot).
            // Dùng GroundPoint nếu được gán; nếu không dùng chính transform.position.y hiện tại
            // (vì boss đang đứng trên sàn lúc bắt đầu skill, pivot đã đúng).
            float fallbackGroundY = bossPhase != null && bossPhase.GroundPoint != null
                ? bossPhase.GroundPoint.position.y
                : transform.position.y;
            float apexY = fallbackGroundY + skyLeapHeight;
            Vector3 lockedImpactPosition = default;

            GameObject telegraphObject = null;
            SkyLeapTelegraphIndicator telegraph = null;
            SkyLeapAerialTrail aerialTrail = null;

            try
            {
                // 1. Charge: boss vẫn có thể bị đánh cho tới khi thực sự rời mặt đất.
                PlaySfx(skyLeapChargeSfx, takeoffPosition);
                SkyLeapVfxUtility.SpawnTakeoff(new Vector3(takeoffPosition.x, fallbackGroundY, -0.16f));
                TriggerCameraShake(0.07f, 0.035f);
                await WaitForDurationAsync(skyLeapChargeDuration, cancellationToken);

                // Chụp mục tiêu đúng tại frame rời mặt đất. Từ đây trở đi không đọc
                // lại vị trí player, nên boss và dấu cảnh báo không thể homing theo.
                float capturedTargetX = playerTransform != null
                    ? playerTransform.position.x
                    : transform.position.x;
                lockedImpactPosition = ResolveValidImpactPosition(
                    capturedTargetX, fallbackGroundY, takeoffPosition.x, apexY);

                if (hurtbox != null) hurtbox.enabled = false;
                aerialTrail = SkyLeapVfxUtility.CreateAerialTrail(transform);

                // 2. Ascent: ease-out cho cảm giác bật mạnh và giảm tốc khi tới đỉnh.
                Vector3 apexPosition = new Vector3(takeoffPosition.x, apexY, takeoffPosition.z);
                await MoveToPositionEasedAsync(
                    apexPosition, skyLeapAscentDuration, MovementEase.EaseOutCubic, cancellationToken);

                telegraphObject = CreateSkyLeapTelegraph(lockedImpactPosition);
                telegraph = telegraphObject.GetComponent<SkyLeapTelegraphIndicator>();

                // 3. Căn ngang một lần tới tọa độ đã chụp. Không đọc lại vị trí player.
                Vector3 alignmentStart = transform.position;
                float alignmentElapsed = 0f;
                while (alignmentElapsed < skyLeapTrackingDuration)
                {
                    float progress = Mathf.Clamp01(
                        alignmentElapsed / Mathf.Max(0.01f, skyLeapTrackingDuration));
                    float eased = progress * progress * (3f - 2f * progress);
                    transform.position = new Vector3(
                        Mathf.Lerp(alignmentStart.x, lockedImpactPosition.x, eased),
                        apexY,
                        takeoffPosition.z);

                    alignmentElapsed += Time.deltaTime;
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                transform.position = new Vector3(
                    lockedImpactPosition.x, apexY, takeoffPosition.z);

                // 4. Hard lock: vị trí dừng hoàn toàn để người chơi có cửa né rõ ràng.
                Vector3 impactPosition = lockedImpactPosition;
                telegraph?.LockAt(impactPosition);
                await WaitForDurationAsync(skyLeapLockDuration, cancellationToken);

                // 5. Descent: không homing nữa, ease-in mạnh thành cú cắm thương có trọng lượng.
                aerialTrail?.BeginDescent();
                PlaySfx(skyLeapDescentSfx, transform.position);
                await MoveToPositionEasedAsync(
                    impactPosition, skyLeapDescentDuration, MovementEase.EaseInCubic, cancellationToken);

                aerialTrail?.StopAndFade();
                aerialTrail = null;
                if (telegraphObject != null)
                {
                    Destroy(telegraphObject);
                    telegraphObject = null;
                }

                // 6. Impact: chỉ gây sát thương tại tâm cú giáng, không phát sóng sang hai bên.
                bossPhase?.PlaySpineAnimation(skyLeapImpactAnim, false);
                PerformSkyLeapImpact(impactPosition);
                if (hurtbox != null) hurtbox.enabled = true;
                await WaitForDurationAsync(postImpactDelay, cancellationToken);
            }
            finally
            {
                if (telegraphObject != null) Destroy(telegraphObject);
                if (aerialTrail != null) aerialTrail.StopAndFade();
                if (hurtbox != null) hurtbox.enabled = true;
            }
        }

        private void PerformSkyLeapImpact(Vector3 impactPosition)
        {
            PlaySfx(skyLeapImpactSfx, impactPosition);
            TriggerCameraShake(impactCameraShakeDuration, impactCameraShakeMagnitude);
            SkyLeapVfxUtility.SpawnImpact(impactPosition, skyLeapImpactRadius);

            Collider2D[] hits = Physics2D.OverlapCircleAll(impactPosition, skyLeapImpactRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || PlayerHitResolver.IsThreatSensorCollider(hit) || !IsPlayer(hit)) continue;
                ApplyDamageToPlayer(hit, skyLeapDamage, Vector2.up);
                break;
            }
        }

        private void SpawnGroundLine(Vector3 position, float directionX)
        {
            if (groundLinePrefab == null) return;

            GameObject lineObject = TrackSpawned(Instantiate(groundLinePrefab, position, Quaternion.identity));
            GroundLineHazard hazard = lineObject.GetComponent<GroundLineHazard>();
            if (hazard == null) hazard = lineObject.AddComponent<GroundLineHazard>();

            hazard.Initialize(
                directionX,
                groundLineDamage,
                groundLineSpeed,
                dusthornPrefab,
                maxActiveDusthorns,
                true,
                environmentLayers.value);
        }

        private GameObject CreateSkyLeapTelegraph(Vector3 groundPosition)
        {
            GameObject telegraphObject = TrackSpawned(new GameObject("SkyLeap_Telegraph"));
            telegraphObject.transform.position = new Vector3(groundPosition.x, groundPosition.y, -0.12f);
            SkyLeapTelegraphIndicator indicator = telegraphObject.AddComponent<SkyLeapTelegraphIndicator>();
            indicator.Initialize(skyLeapTrackingDuration, skyLeapLockDuration, skyLeapImpactRadius);
            return telegraphObject;
        }

        private Vector3 ResolveValidImpactPosition(float desiredX, float fallbackY, float fallbackX, float apexY)
        {
            if (TryResolveGround(desiredX, apexY, out Vector3 groundPoint)) return groundPoint;

            float directionToSafety = Mathf.Sign(fallbackX - desiredX);
            if (Mathf.Approximately(directionToSafety, 0f)) directionToSafety = 1f;
            float searchStep = Mathf.Max(0.1f, skyLeapGroundSearchStep);
            int attempts = Mathf.Max(1, skyLeapGroundSearchAttempts);
            for (int i = 1; i <= attempts; i++)
            {
                float candidateX = desiredX + directionToSafety * searchStep * i;
                if (TryResolveGround(candidateX, apexY, out groundPoint)) return groundPoint;
            }

            return new Vector3(fallbackX, fallbackY + skyLeapLandingOffset, transform.position.z);
        }

        private bool TryResolveGround(float x, float apexY, out Vector3 groundPoint)
        {
            float probeY = Mathf.Max(apexY + 1f, transform.position.y + skyLeapGroundProbeHeight);
            Vector2 origin = new Vector2(x, probeY);
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.down,
                Mathf.Max(2f, skyLeapGroundProbeDistance),
                GetEnvironmentLayers());

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < 0.42f) continue;
                // hit.point.y là bề mặt sàn (đáy boss). Cộng thêm offset pivot để
                // transform.position dừng đúng ở vị trí boss đứng bình thường trên sàn.
                groundPoint = new Vector3(hit.point.x, hit.point.y + skyLeapLandingOffset, transform.position.z);
                return true;
            }

            groundPoint = default;
            return false;
        }

        private LayerMask GetEnvironmentLayers()
        {
            return environmentLayers.value != 0
                ? environmentLayers
                : PlayerLayerConfig.EnvironmentLayer;
        }

        private async Task RelocateAfterSkillAsync(CancellationToken cancellationToken)
        {
            Transform targetPoint = GetRandomRelocationPoint();
            if (targetPoint == null) return;
            await MoveToPositionAsync(targetPoint.position, Vector3.Distance(transform.position, targetPoint.position) / Mathf.Max(0.1f, dashSpeed), cancellationToken);
        }

        private async Task MoveToPositionAsync(Vector3 targetPosition, float duration, CancellationToken cancellationToken, bool playMoveAnimation = true, GameObject followVfx = null)
        {
            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            if (playMoveAnimation) bossPhase?.PlaySpineAnimation(moveAnim, true);

            FacePlayer();

            while (elapsed < duration)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, normalizedTime);
                if (followVfx != null) followVfx.transform.position = transform.position;
                FacePlayer();
                elapsed += Time.deltaTime;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }

            transform.position = targetPosition;
            if (followVfx != null) followVfx.transform.position = targetPosition;
        }

        private async Task MoveToPositionEasedAsync(Vector3 targetPosition, float duration,
            MovementEase easing, CancellationToken cancellationToken)
        {
            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            FacePlayer();

            while (elapsed < duration)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedTime;
                switch (easing)
                {
                    case MovementEase.EaseInCubic:
                        easedTime = normalizedTime * normalizedTime * normalizedTime;
                        break;
                    case MovementEase.EaseOutCubic:
                        float inverse = 1f - normalizedTime;
                        easedTime = 1f - inverse * inverse * inverse;
                        break;
                    default:
                        easedTime = normalizedTime;
                        break;
                }

                transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, easedTime);
                FacePlayer();
                elapsed += Time.deltaTime;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }

            transform.position = targetPosition;
        }

        private static async Task WaitForDurationAsync(float duration, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void TriggerCameraShake(float duration, float magnitude)
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ShakeCamera(duration, magnitude);
            }
        }

        private void PlaySfx(AudioClip clip, Vector3 position)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position);
        }

        private void ApplyDamageToPlayer(Collider2D playerCollider, int damage, Vector2 knockbackDirection)
        {
            PlayerHitResolver.TryApplyHitToPlayer(
                playerCollider,
                damage,
                ParryableAttackType.Melee,
                transform,
                knockbackDirection.normalized);
        }

        private bool IsPlayer(Collider2D collider)
        {
            return collider.CompareTag("Player") || collider.GetComponentInParent<PlayerHealth>() != null;
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                playerTransform = PlayerRegistry.Current.transform;
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
                return;
            }

            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null) playerTransform = playerHealth.transform;
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

        private Transform GetRandomRelocationPoint()
        {
            if (arenaRelocationPoints == null || arenaRelocationPoints.Length == 0) return null;
            return arenaRelocationPoints[UnityEngine.Random.Range(0, arenaRelocationPoints.Length)];
        }

        private void RelocateToRandomPointInstant()
        {
            Transform point = GetRandomRelocationPoint();
            if (point != null) transform.position = point.position;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, skyLeapImpactRadius);
        }
    }
}
