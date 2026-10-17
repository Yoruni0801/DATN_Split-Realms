using System.Collections;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>Hất bom dung nham lên trời rồi lần lượt điều hướng từng bom tới Player.</summary>
    public class Phase4ObsidianGuillotineSkill : QuyVuongPhase4SkillBase
    {
        [Header("=== TRIỆU HỒI BOM DUNG NHAM ===")]
        [SerializeField] private float summonWindupTime = 0.25f;
        [SerializeField] private GameObject floatingRockPrefab;
        [SerializeField] private AudioClip summonSFX;
        [SerializeField, Min(1)] private int floatingRockCount = 3;
        [SerializeField] private float rockFloatHeight = 5.5f;
        [SerializeField] private float rockSpawnSpread = 4f;
        [SerializeField] private Vector3 bombScale = new Vector3(1.5f, 1.5f, 1f);
        [SerializeField, Min(0.01f)] private float bombLaunchDuration = 0.28f;
        [SerializeField] private float launchArcPeakBonus = 1.2f;

        [Header("=== NHỊP NÉM TỪNG BOM ===")]
        [Tooltip("Chờ sau khi toàn bộ bom đã lên không, trước khi bom đầu tiên bay tới Player.")]
        [SerializeField, Min(0f)] private float firstRockHomingDelay = 0.2f;
        [Tooltip("Thời gian giữa lúc kích hoạt hai quả bom.")]
        [SerializeField, Min(0f)] private float rockThrowInterval = 0.25f;
        [Tooltip("Chờ trước từng quả bom. Phần tử 0 là bom đầu tiên; nếu thiếu phần tử thì dùng Rock Throw Interval.")]
        [SerializeField] private float[] individualRockThrowDelays = { 0f, 0.25f, 0.25f };
        [Tooltip("Bật: quả sau chỉ bay sau khi quả trước nổ. Tắt: các quả có thể bay chồng thời gian.")]
        [SerializeField] private bool launchRocksSequentially = true;

        [Header("=== NÉM BOM VÀO VỊ TRÍ ĐÃ KHÓA ===")]
        [SerializeField, Min(0.01f)] private float homingStartSpeed = 7f;
        [SerializeField, Min(0.01f)] private float homingMaxSpeed = 16f;
        [SerializeField, Min(0f)] private float homingAcceleration = 20f;
        [Tooltip("Tốc độ xoay của bom khi được phóng, tính bằng độ/giây. Giá trị âm sẽ xoay ngược chiều.")]
        [SerializeField] private float rockSpinSpeed = 720f;
        [SerializeField, Min(0.1f)] private float maxHomingDuration = 3f;
        [SerializeField] private bool explodeWhenHomingExpires = true;

        [Header("=== VA CHẠM & NỔ BOM ===")]
        [SerializeField, Min(0.01f)] private float rockHitRadius = 0.45f;
        [SerializeField] private LayerMask rockCollisionMask = ~0;
        [Tooltip("Tag được tính là mặt chắn bom. Player luôn được xử lý riêng.")]
        [SerializeField] private string[] rockStopTags = { "Ground", "Wall" };
        [SerializeField] private int rockImpactDamage = 20;
        [SerializeField] private float rockImpactKnockback = 4f;
        [SerializeField] private GameObject slamImpactVFXPrefab;
        [SerializeField] private AudioClip slamSFX;
        [SerializeField] private float impactShakeIntensity = 0.22f;
        [SerializeField] private float impactShakeDuration = 0.16f;

        [Header("=== MẢNH ĐẠN SAU KHI NỔ ===")]
        [SerializeField, Min(1)] private int shrapnelPerRock = 6;
        [SerializeField] private int shrapnelDamage = 12;
        [SerializeField] private float shrapnelSpeed = 14f;
        [SerializeField] private GameObject shrapnelPrefab;

        [Header("=== LƠ LỬNG TRƯỚC KHI NÉM ===")]
        [SerializeField] private float bobbingAmplitude = 0.18f;
        [SerializeField] private float bobbingSpeed = 2.5f;

        private GameObject[] spawnedRocks;

        protected override void Awake()
        {
            base.Awake();
            skillName = "Phase 4 - Obsidian Homing Bombardment";
        }

        public override IEnumerator ExecuteSkill(Transform playerTransform)
        {
            if (playerTransform == null) yield break;
            ResolveReferences();
            FlipTowardsPlayer(playerTransform);
            yield return StartCoroutine(SummonRocks(playerTransform));
            yield return new WaitForSeconds(firstRockHomingDelay);
            yield return StartCoroutine(LaunchRocksAtPlayer(playerTransform));
            FlipTowardsPlayer(playerTransform);
            PlayIdleAnimation();
        }

        private IEnumerator SummonRocks(Transform playerTransform = null)
        {
            if (playerTransform != null) FlipTowardsPlayer(playerTransform);
            SetBossColor(new Color(1f, 0.6f, 0.2f));
            PlayAttackAnimation();
            if (summonSFX != null) AudioSource.PlayClipAtPoint(summonSFX, transform.position);
            yield return new WaitForSeconds(summonWindupTime);
            SetBossColor(Color.white);
            if (playerTransform != null) FlipTowardsPlayer(playerTransform);

            spawnedRocks = new GameObject[floatingRockCount];
            Vector3 bossPosition = transform.position;
            Vector3[] starts = new Vector3[floatingRockCount];
            Vector3[] targets = new Vector3[floatingRockCount];
            for (int i = 0; i < floatingRockCount; i++)
            {
                float offsetX = (i - (floatingRockCount - 1) / 2f) * rockSpawnSpread;
                starts[i] = bossPosition + new Vector3(offsetX * 0.4f, 0.3f, 0f);
                targets[i] = bossPosition + new Vector3(offsetX, rockFloatHeight, 0f);
                GameObject rock = floatingRockPrefab != null ? Instantiate(floatingRockPrefab, starts[i], Quaternion.identity) : CreateFallbackRock(starts[i]);
                rock.transform.localScale = bombScale;
                Phase4FloatingRock floater = rock.GetComponent<Phase4FloatingRock>();
                if (floater != null) floater.enabled = false;
                spawnedRocks[i] = rock;
            }

            float elapsed = 0f;
            while (elapsed < bombLaunchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bombLaunchDuration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                float arcHeight = Mathf.Sin(t * Mathf.PI) * launchArcPeakBonus;
                for (int i = 0; i < spawnedRocks.Length; i++)
                    if (spawnedRocks[i] != null)
                        spawnedRocks[i].transform.position = Vector3.Lerp(starts[i], targets[i], easedT) + Vector3.up * arcHeight;
                yield return null;
            }

            for (int i = 0; i < spawnedRocks.Length; i++)
            {
                if (spawnedRocks[i] == null) continue;
                spawnedRocks[i].transform.position = targets[i];
                Phase4FloatingRock floater = spawnedRocks[i].GetComponent<Phase4FloatingRock>();
                if (floater == null) floater = spawnedRocks[i].AddComponent<Phase4FloatingRock>();
                floater.SetAnchorPosition(targets[i], bobbingAmplitude, bobbingSpeed);
                floater.enabled = true;
            }
            if (playerTransform != null) FlipTowardsPlayer(playerTransform);
        }

        private IEnumerator LaunchRocksAtPlayer(Transform playerTransform)
        {
            if (spawnedRocks == null) yield break;
            for (int i = 0; i < spawnedRocks.Length; i++)
            {
                GameObject rock = spawnedRocks[i];
                if (rock == null) continue;
                FlipTowardsPlayer(playerTransform);
                float throwDelay = GetRockThrowDelay(i);
                if (throwDelay > 0f) yield return new WaitForSeconds(throwDelay);
                FlipTowardsPlayer(playerTransform);
                if (launchRocksSequentially) yield return StartCoroutine(HomeRockToPlayer(rock, playerTransform));
                else StartCoroutine(HomeRockToPlayer(rock, playerTransform));
            }
            if (!launchRocksSequentially) yield return new WaitForSeconds(maxHomingDuration);
            spawnedRocks = null;
            FlipTowardsPlayer(playerTransform);
        }

        private float GetRockThrowDelay(int rockIndex)
        {
            if (individualRockThrowDelays != null && rockIndex >= 0 && rockIndex < individualRockThrowDelays.Length)
                return Mathf.Max(0f, individualRockThrowDelays[rockIndex]);
            return rockIndex == 0 ? 0f : rockThrowInterval;
        }

        private IEnumerator HomeRockToPlayer(GameObject rock, Transform playerTransform)
        {
            if (rock == null) yield break;
            Phase4FloatingRock floater = rock.GetComponent<Phase4FloatingRock>();
            if (floater != null) floater.enabled = false;

            // Chỉ khóa vị trí Player đúng một lần khi quả bom được phóng.
            // Sau đó bom không đổi hướng, để Player có thể chạy sang vị trí khác để né.
            Vector2 lockedTargetPosition = playerTransform != null
                ? (Vector2)playerTransform.position
                : (Vector2)rock.transform.position + Vector2.down;
            Vector2 direction = lockedTargetPosition - (Vector2)rock.transform.position;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
            direction.Normalize();
            float speed = homingStartSpeed;
            float elapsed = 0f;
            float spinAngle = 0f;

            while (rock != null && elapsed < maxHomingDuration)
            {
                elapsed += Time.deltaTime;
                speed = Mathf.MoveTowards(speed, homingMaxSpeed, homingAcceleration * Time.deltaTime);
                rock.transform.position += (Vector3)(direction * speed * Time.deltaTime);
                spinAngle += rockSpinSpeed * Time.deltaTime;
                float travelAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                rock.transform.rotation = Quaternion.Euler(0f, 0f, travelAngle + spinAngle);

                Collider2D hit = FindRockCollision(rock.transform.position, rock);
                if (hit != null)
                {
                    DetonateRock(rock, hit.CompareTag("Player") ? hit.gameObject : null);
                    yield break;
                }
                yield return null;
            }
            if (rock != null)
            {
                if (explodeWhenHomingExpires) DetonateRock(rock, null);
                else Destroy(rock);
            }
        }

        private Collider2D FindRockCollision(Vector3 position, GameObject rock)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, rockHitRadius, rockCollisionMask);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.transform.root == transform.root || hit.transform == rock.transform || hit.transform.IsChildOf(rock.transform)) continue;
                if (hit.CompareTag("Player") || HasStopTag(hit)) return hit;
            }
            return null;
        }

        private bool HasStopTag(Collider2D hit)
        {
            if (rockStopTags == null) return false;
            foreach (string stopTag in rockStopTags)
                if (!string.IsNullOrWhiteSpace(stopTag) && hit.CompareTag(stopTag)) return true;
            return false;
        }

        private void DetonateRock(GameObject rock, GameObject directlyHitPlayer)
        {
            if (rock == null) return;
            Vector3 explosionPosition = rock.transform.position;
            if (directlyHitPlayer != null) ApplyPlayerDamage(directlyHitPlayer, rockImpactDamage, rockImpactKnockback);
            if (slamImpactVFXPrefab != null) SpawnVFX(slamImpactVFXPrefab, explosionPosition, Quaternion.identity, 2f);
            if (slamSFX != null) AudioSource.PlayClipAtPoint(slamSFX, explosionPosition);
            TriggerCameraShake(impactShakeIntensity, impactShakeDuration);

            for (int i = 0; i < shrapnelPerRock; i++)
            {
                float angle = 360f / shrapnelPerRock * i;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
                GameObject shrapnel = shrapnelPrefab != null ? Instantiate(shrapnelPrefab, explosionPosition, Quaternion.identity) : CreateFallbackShrapnel(explosionPosition);
                Phase4ShrapnelProjectile projectile = shrapnel.GetComponent<Phase4ShrapnelProjectile>();
                if (projectile == null) projectile = shrapnel.AddComponent<Phase4ShrapnelProjectile>();
                projectile.Initialize(direction, shrapnelDamage, shrapnelSpeed);
            }
            Destroy(rock);
        }

        private static GameObject CreateFallbackRock(Vector3 position)
        {
            GameObject rock = new GameObject("Phase4_Runtime_HomingBomb");
            rock.transform.position = position;
            rock.AddComponent<SpriteRenderer>().color = new Color(1f, 0.45f, 0.1f, 1f);
            rock.AddComponent<CircleCollider2D>().radius = 0.5f;
            return rock;
        }

        private static GameObject CreateFallbackShrapnel(Vector3 position)
        {
            GameObject shrapnel = new GameObject("Phase4_Runtime_Shrapnel");
            shrapnel.transform.position = position;
            shrapnel.AddComponent<SpriteRenderer>().color = new Color(1f, 0.3f, 0f, 1f);
            shrapnel.transform.localScale = new Vector3(0.25f, 0.5f, 1f);
            CapsuleCollider2D collider = shrapnel.AddComponent<CapsuleCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.3f, 0.6f);
            Rigidbody2D body = shrapnel.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            return shrapnel;
        }

        public void ClearActiveProjectiles()
        {
            StopAllCoroutines();
            if (spawnedRocks != null)
            {
                for (int i = 0; i < spawnedRocks.Length; i++)
                {
                    if (spawnedRocks[i] != null) Destroy(spawnedRocks[i]);
                }
                spawnedRocks = null;
            }

            var shrapnels = FindObjectsByType<Phase4ShrapnelProjectile>(FindObjectsSortMode.None);
            foreach (var s in shrapnels) if (s != null) Destroy(s.gameObject);

            var rocks = FindObjectsByType<Phase4FloatingRock>(FindObjectsSortMode.None);
            foreach (var r in rocks) if (r != null) Destroy(r.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, rockHitRadius);
        }
    }
}
