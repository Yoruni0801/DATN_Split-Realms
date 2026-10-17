using UnityEngine;

namespace Bao.Enemies
{
    public class HiveBeeProjectile : MonoBehaviour, IDamageable
    {
        [Header("Bee Stats")]
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float speed = 5f;
        [SerializeField] private int damage = 10;

        private Transform playerTransform;
        private Transform attackerTransform;
        private Rigidbody2D rb;
        private Collider2D hitCollider;
        private float spawnTime;
        private ParryableProjectileAdapter parryAdapter;
        private readonly Collider2D[] playerOverlapResults = new Collider2D[8];

        public void Initialize(Transform target, Transform attacker)
        {
            playerTransform = target;
            attackerTransform = attacker;
        }

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            gameObject.tag = "Enemy";

            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Make sure the collider is set as a trigger, adding one if missing
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider2D>();
            }
            col.isTrigger = true;
            hitCollider = col;

            parryAdapter = ParryableProjectileAdapter.Ensure(this);
        }

        public void TakeDamage(int damageAmount)
        {
            // Destroy bee when hit by player
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            spawnTime = Time.time;
            
            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (playerTransform == null && player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
            {
                parryAdapter.TrackMotion(rb != null ? rb.linearVelocity : Vector2.zero);
                return;
            }

            if (playerTransform == null)
            {
                // If player is not found, fly straight based on current localScale.x (left if positive, right if negative)
                float moveDir = transform.localScale.x > 0 ? -1f : 1f;
                rb.linearVelocity = new Vector2(moveDir * speed, 0f);
                parryAdapter?.TrackMotion(rb.linearVelocity);
                return;
            }

            // Homing movement towards player
            Vector2 direction = (Vector2)playerTransform.position - (Vector2)transform.position;
            direction.Normalize();

            // Move towards player
            rb.linearVelocity = direction * speed;
            parryAdapter?.TrackMotion(rb.linearVelocity);

            // Flip visual sprite scale x based on direction of movement relative to X axis
            // Standard direction of the bee sprite in Unity is facing left (positive scale.x)
            if (direction.x != 0f)
            {
                Vector3 scale = transform.localScale;
                scale.x = (direction.x > 0f) ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            CheckForPlayerOverlap();
        }

        // Homing can cross a player collider between two discrete physics ticks.  Querying the
        // bee's trigger explicitly keeps its hit behaviour reliable while preserving TryHit as
        // the single damage/parry path.
        private void CheckForPlayerOverlap()
        {
            if (hitCollider == null || (parryAdapter != null && parryAdapter.IgnorePlayerCollision))
            {
                return;
            }

            ContactFilter2D playerFilter = new ContactFilter2D();
            playerFilter.useLayerMask = true;
            playerFilter.SetLayerMask(LayerMask.GetMask("Player"));
            playerFilter.useTriggers = true;

            int overlapCount = hitCollider.Overlap(playerFilter, playerOverlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D playerCollider = playerOverlapResults[i];
                if (playerCollider != null && playerCollider.GetComponentInParent<PlayerHealth>() != null)
                {
                    TryHit(playerCollider);
                    return;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            TryHit(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            TryHit(collision);
        }

        private void TryHit(Collider2D collision)
        {
            if (collision == null)
            {
                return;
            }

            PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
            bool isPlayerCollider = playerHealth != null;

            // Ignore enemies and non-player triggers. Player body colliders may be triggers.
            if (collision.gameObject.CompareTag("Enemy") || (!isPlayerCollider && collision.isTrigger))
            {
                return;
            }

            if (parryAdapter != null && parryAdapter.IgnorePlayerCollision && isPlayerCollider)
            {
                return;
            }

            if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(collision, damage))
            {
                Destroy(gameObject);
                return;
            }

            if (isPlayerCollider)
            {
                Collider2D playerCollider = collision.GetComponentInChildren<Collider2D>();
                if (playerCollider == null)
                {
                    playerCollider = collision;
                }

                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    playerCollider,
                    damage,
                    ParryableAttackType.Projectile,
                    parryAdapter,
                    attackerTransform != null ? attackerTransform : transform,
                    rb != null ? rb.linearVelocity : Vector2.zero);

                if (resolution != IncomingAttackResolution.Parried)
                {
                    Destroy(gameObject);
                }
            }
            else if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") || collision.gameObject.CompareTag("Ground"))
            {
                // Destroy on hitting walls or ground
                Destroy(gameObject);
            }
        }
    }
}
