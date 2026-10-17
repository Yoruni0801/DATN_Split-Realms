using UnityEngine;

namespace Bao.Enemies
{
    public class ChubblooProjectile : MonoBehaviour
    {
        private const float SlowEffectDuration = 2f;

        [Header("Projectile Stats")]
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private int damage = 15;
        [SerializeField] private float speed = 10f;
        [SerializeField] private GameObject impactVFX;

        private Vector2 direction;
        private Rigidbody2D rb;
        private float spawnTime;
        private ParryableProjectileAdapter parryAdapter;
        private ChubblooProjectileVFX projectileVfx;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();

            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Make sure the collider is set as a trigger
            if (TryGetComponent<Collider2D>(out var col))
                col.isTrigger = true;

            parryAdapter = ParryableProjectileAdapter.Ensure(this);
            // Cấu hình adapter để viên đạn có thể phản ngược lại (Reflect) và gây sát thương cho quái
            parryAdapter.ConfigureParticleAttack(
                damage: damage,
                isSlowAttack: true,
                canBeBlocked: true,
                response: ProjectileParryResponseMode.Reflect,
                parryable: true,
                source: null);

            // Tắt cờ này vì đạn băng của Chubbloo sử dụng OnTriggerEnter2D riêng để xử lý va chạm,
            // không dùng Trigger module của ParticleSystem
            if (TryGetComponent<ParticleSystem>(out _))
            {
                // Mặc định ConfigureParticleAttack bật cờ này lên,
                // nhưng ChubblooProjectile không dùng Trigger module để gây sát thương
                var field = typeof(ParryableProjectileAdapter).GetField("useParticleSystemTriggers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(parryAdapter, false);
            }

            projectileVfx = GetComponent<ChubblooProjectileVFX>();
            if (projectileVfx == null)
                projectileVfx = gameObject.AddComponent<ChubblooProjectileVFX>();
        }

        private void Start()
        {
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime >= lifetime)
                Destroy(gameObject);
        }

        public void Launch(Vector2 dir, float spd, int dmg)
        {
            direction = dir.normalized;
            speed = spd;
            damage = dmg;

            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
                parryAdapter?.TrackMotion(rb.linearVelocity);
            }

            projectileVfx?.OnLaunched(direction, speed);
        }

        private void SpawnImpactVFX()
        {
            if (impactVFX != null)
                Instantiate(impactVFX, transform.position, Quaternion.identity);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.isTrigger && !PlayerHitResolver.IsThreatSensorCollider(collision))
                return;

            Vector2 hitPoint = collision.bounds.ClosestPoint(transform.position);

            // Khi đã bị parry (thuộc sở hữu người chơi), va chạm vào quái sẽ gây sát thương
            if (parryAdapter != null && parryAdapter.IsPlayerOwned)
            {
                if (collision.CompareTag("Player"))
                    return;

                if (PlayerHitResolver.TryApplyDamageToEnemy(collision, damage))
                {
                    projectileVfx?.TriggerImpact(hitPoint, false);
                    SpawnImpactVFX();
                    Destroy(gameObject);
                    return;
                }

                // Phá hủy đạn nếu đã parry mà trúng ground/obstacle
                if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") || collision.gameObject.CompareTag("Ground"))
                {
                    projectileVfx?.TriggerImpact(hitPoint, false);
                    SpawnImpactVFX();
                    Destroy(gameObject);
                }
                return;
            }

            // Khi chưa bị parry, bỏ qua quái
            if (collision.gameObject.layer == LayerMask.NameToLayer("Enemy") || collision.gameObject.CompareTag("Enemy") || collision.GetComponentInParent<BaoEnemyBase>() != null)
                return;

            if (parryAdapter != null && parryAdapter.IgnorePlayerCollision && collision.CompareTag("Player"))
                return;

            if (collision.CompareTag("Player"))
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Projectile,
                    parryAdapter,
                    transform,
                    rb != null ? rb.linearVelocity : direction * speed,
                    true,
                    true); // isSlowAttack

                if (resolution != IncomingAttackResolution.Parried)
                {
                    if (resolution == IncomingAttackResolution.Damaged)
                    {
                        ChubblooFrostImpactVfx.Spawn(
                            collision.transform,
                            hitPoint,
                            SlowEffectDuration);

                        projectileVfx?.TriggerImpact(hitPoint, true);
                    }

                    Destroy(gameObject);
                }
            }
            else if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") || collision.gameObject.CompareTag("Ground"))
            {
                projectileVfx?.TriggerImpact(hitPoint, false);
                SpawnImpactVFX();
                Destroy(gameObject);
            }
        }
    }
}
