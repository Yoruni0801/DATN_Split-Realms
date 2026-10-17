using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase2
{
    [RequireComponent(typeof(Collider2D))]
    public class QuyVuongHomingProjectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private int damage = 15;
        [SerializeField] private float speed = 12f;
        [SerializeField] private float homingRotateSpeed = 200f; // Tốc độ xoay hướng đuổi theo player (độ/giây)
        [SerializeField] private float homingDuration = 3f; // Thời gian tối đa đuổi theo player trước khi bay thẳng
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private float waitBeforeChase = 1.0f; // Thời gian đứng yên/lơ lửng trước khi bắt đầu đuổi
        [SerializeField] private float speedDecayRate = 0.85f; // Tốc độ bay giảm dần (mỗi giây nhân với hệ số này, <1 = giảm)

        [Header("Parry & Hit Settings")]
        [SerializeField] private ParryableAttackType attackType = ParryableAttackType.Projectile;
        [SerializeField] private bool canBeBlocked = true;
        [SerializeField] private bool isSlowAttack = false;

        [Header("Visual Effects")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private ParticleSystem trailParticleSystem;

        private Transform playerTransform;
        private Transform ownerTransform;
        private Vector2 currentDirection = Vector2.left;
        private bool isInitialized = false;
        private float timer = 0f;
        private ParryableProjectileAdapter parryAdapter;

        private Rigidbody2D rb;

        public void Initialize(Vector2 initialDirection, float customSpeed, int customDamage, Transform owner, Transform targetPlayer)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }
            rb.gravityScale = 0f;

            currentDirection = initialDirection.normalized;
            speed = customSpeed;
            damage = customDamage;
            ownerTransform = owner;
            playerTransform = targetPlayer;
            isInitialized = true;
            timer = 0f;

            // Xoay hình ảnh/prefab về hướng bay ban đầu
            UpdateRotation();

            // Tự động gắn ParryableProjectileAdapter nếu chưa có và cấu hình tự hủy khi parry
            parryAdapter = ParryableProjectileAdapter.Ensure(this);
            if (parryAdapter != null)
            {
                parryAdapter.ConfigureParticleAttack(
                    damage,
                    isSlowAttack,
                    canBeBlocked,
                    ProjectileParryResponseMode.Destroy, // Hủy luôn khi bị Parry (theo yêu cầu)
                    true,
                    this.GetComponentInParent<IParryable>()
                );
            }

            if (trailParticleSystem != null)
            {
                trailParticleSystem.Play();
            }

            Destroy(gameObject, lifetime);
        }

        private void FindPlayerIfNull()
        {
            if (playerTransform != null) return;

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
            if (!isInitialized) return;

            FindPlayerIfNull();

            // Nếu đạn đã bị Player Parry phản lại -> đạn thuộc về Player và không đuổi theo Player nữa
            if (parryAdapter != null && parryAdapter.HasBeenParried)
            {
                Vector2 parriedVel = parryAdapter.CurrentMotion;
                if (rb != null) rb.linearVelocity = parriedVel;
                else transform.Translate(parriedVel * Time.deltaTime, Space.World);
                return;
            }

            timer += Time.deltaTime;

            // Giai đoạn chờ: quả cầu đứng yên / lơ lửng tại chỗ trước khi bắt đầu đuổi
            if (timer < waitBeforeChase)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            float chaseTimer = timer - waitBeforeChase;

            // Xử lý Homing: Đuổi theo vị trí của Player trong thời gian homingDuration
            if (playerTransform != null && chaseTimer <= homingDuration)
            {
                Vector2 targetDirection = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                if (targetDirection.sqrMagnitude > 0.001f)
                {
                    // Xoay/hướng đuổi về phía Player với tốc độ homingRotateSpeed
                    currentDirection = Vector3.RotateTowards(currentDirection, targetDirection, homingRotateSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
                    UpdateRotation();
                }
            }

            // Giảm tốc độ dần theo speedDecayRate mỗi giây
            speed *= Mathf.Pow(speedDecayRate, Time.deltaTime);

            // Cập nhật vị trí di chuyển qua Rigidbody2D velocity hoặc Translate
            Vector2 velocity = currentDirection * speed;
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }
            else
            {
                transform.Translate(velocity * Time.deltaTime, Space.World);
            }

            // Track motion cho adapter parry
            if (parryAdapter != null)
            {
                parryAdapter.TrackMotion(velocity);
            }
        }

        private void UpdateRotation()
        {
            if (currentDirection.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!isInitialized) return;

            // Đạn đã parry dội lại boss / enemy
            if (parryAdapter != null && parryAdapter.HasBeenParried)
            {
                if (parryAdapter.TryHandleEnemyCollision(collision, damage))
                {
                    SpawnHitEffect();
                    Destroy(gameObject);
                }
                return;
            }

            // Bỏ qua va chạm với Boss đã bắn ra
            if (ownerTransform != null && collision.transform.root == ownerTransform.root) return;

            // Bỏ qua sensor parry threat
            if (PlayerHitResolver.IsThreatSensorCollider(collision)) return;

            // Trúng Player
            if (collision.CompareTag("Player") || collision.GetComponentInParent<PlayerHealth>() != null)
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    attackType,
                    parryAdapter != null ? (IParryable)parryAdapter : null,
                    ownerTransform != null ? ownerTransform : transform,
                    currentDirection,
                    canBeBlocked,
                    isSlowAttack
                );

                if (resolution == IncomingAttackResolution.Parried || resolution == IncomingAttackResolution.Guarded)
                {
                    // Khi Player Parry hoặc Đỡ/Guard thành công -> Quả cầu bị tiêu biến và không gây sát thương
                    Debug.Log($"[QuyVuongHomingProjectile] Player đã {resolution} thành công -> Quả cầu bị tiêu biến không gây damage!");
                    SpawnHitEffect();
                    Destroy(gameObject);
                    return;
                }
                else if (resolution == IncomingAttackResolution.Damaged)
                {
                    // Chỉ gây sát thương và hủy đạn khi Player thực sự trúng đòn (không đỡ/parry)
                    SpawnHitEffect();
                    Destroy(gameObject);
                    return;
                }
            }

            // Xử lý khi Player dùng đòn đánh (Melee Attack / Skill) chém trúng quả cầu -> Quả cầu bị phá hủy
            if (collision.CompareTag("PlayerAttack") || collision.gameObject.layer == LayerMask.NameToLayer("PlayerAttack") || collision.name.Contains("Melee"))
            {
                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }
            if (((1 << collision.gameObject.layer) & PlayerLayerConfig.EnvironmentLayer) != 0)
            {
                SpawnHitEffect();
                Destroy(gameObject);
            }
        }

        private void SpawnHitEffect()
        {
            if (hitVfxPrefab != null)
            {
                Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            }
            if (trailParticleSystem != null)
            {
                trailParticleSystem.Stop();
            }
        }
    }
}
