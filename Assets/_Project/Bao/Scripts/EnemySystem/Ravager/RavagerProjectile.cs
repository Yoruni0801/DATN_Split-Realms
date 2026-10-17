using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 3f;
        private Vector2 direction;
        private float speed;
        private int damage;
        private Rigidbody2D rb;
        private float spawnTime;
        private ParryableProjectileAdapter parryAdapter;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            parryAdapter = ParryableProjectileAdapter.Ensure(this);
        }

        private void Start()
        {
            spawnTime = Time.time;
            AlignRotationWithVelocity();
        }

        private void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
            }

            AlignRotationWithVelocity();
        }

        private void AlignRotationWithVelocity()
        {
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
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
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (parryAdapter != null && parryAdapter.IgnorePlayerCollision && collision.CompareTag("Player"))
            {
                return;
            }

            if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(collision, damage))
            {
                Destroy(gameObject);
                return;
            }

            if (collision.CompareTag("Player"))
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Projectile,
                    parryAdapter,
                    transform,
                    rb != null ? rb.linearVelocity : direction * speed);

                if (resolution != IncomingAttackResolution.Parried)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
