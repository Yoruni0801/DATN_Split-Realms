using UnityEngine;

namespace Bao.Bosses
{
    public class NidusAcidProjectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float fallSpeed = 10f;
        [SerializeField] private GameObject splashVFXPrefab;

        private Vector3 targetPosition;
        private int damage;
        private float damageRadius;
        private float poolDuration;
        private int poolDps;
        private GameObject poolPrefab;
        private bool hasLanded = false;
        private LayerMask envLayer;
        private ParryableProjectileAdapter parryAdapter;
        private NidusCocoonBoss owner;

        private void Awake()
        {
            parryAdapter = ParryableProjectileAdapter.Ensure(this);
        }

        public void Initialize(Vector3 target, int dmg, float radius, float duration, int dps, GameObject poolPrefab, LayerMask environmentLayer)
        {
            Initialize(target, dmg, radius, duration, dps, poolPrefab, environmentLayer, null);
        }

        public void Initialize(Vector3 target, int dmg, float radius, float duration, int dps, GameObject poolPrefab, LayerMask environmentLayer, NidusCocoonBoss owner)
        {
            this.targetPosition = target;
            this.damage = dmg;
            this.damageRadius = radius;
            this.poolDuration = duration;
            this.poolDps = dps;
            this.poolPrefab = poolPrefab;
            this.envLayer = environmentLayer;
            this.owner = owner;

            hasLanded = false;
        }

        private void Update()
        {
            if (hasLanded) return;

            if (parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
            {
                transform.position += (Vector3)parryAdapter.CurrentMotion * Time.deltaTime;
                return;
            }

            // Move towards target position (fallback in case it misses triggers)
            Vector3 previousPosition = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);
            parryAdapter?.TrackMotion((transform.position - previousPosition) / Mathf.Max(Time.deltaTime, 0.0001f));

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                OnLanded();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (hasLanded) return;

            if (parryAdapter != null && parryAdapter.IgnorePlayerCollision && collision.CompareTag("Player"))
            {
                return;
            }

            bool isPlayer = collision.CompareTag("Player");
            bool isGround = ((1 << collision.gameObject.layer) & envLayer) != 0;

            if (isPlayer)
            {
                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Projectile,
                    parryAdapter,
                    transform,
                    transform.position - targetPosition);

                if (resolution == IncomingAttackResolution.Parried)
                {
                    return;
                }
            }

            if (isPlayer || isGround)
            {
                targetPosition = transform.position; // Cập nhật lại vị trí nổ tại điểm va chạm
                OnLanded();
            }
        }

        private void OnLanded()
        {
            hasLanded = true;

            // Spawn splash effect
            if (splashVFXPrefab != null)
            {
                GameObject vfx = Instantiate(splashVFXPrefab, targetPosition, Quaternion.identity);
                Destroy(vfx, 2f);
            }

            // Deal splash damage
            Collider2D[] hits = Physics2D.OverlapCircleAll(targetPosition, damageRadius);
            foreach (var hit in hits)
            {
                if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(hit, damage))
                {
                    continue;
                }

                if (hit.CompareTag("Player"))
                {
                    if (parryAdapter != null && parryAdapter.IgnorePlayerCollision)
                        continue;

                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit,
                        damage,
                        ParryableAttackType.Projectile,
                        parryAdapter,
                        transform,
                        Vector2.down);
                }
            }

            // Spawn lingering acid pool
            if (poolPrefab != null)
            {
                GameObject pool = Instantiate(poolPrefab, targetPosition, Quaternion.identity);

                // Inherit premium shader/material
                SpriteRenderer mySprite = GetComponentInChildren<SpriteRenderer>();
                SpriteRenderer poolSprite = pool.GetComponentInChildren<SpriteRenderer>();
                if (mySprite != null && poolSprite != null)
                {
                    poolSprite.material = mySprite.material;
                }

                NidusAcidPool poolScript = pool.GetComponent<NidusAcidPool>();
                if (poolScript != null)
                {
                    poolScript.Initialize(poolDuration, poolDps, damageRadius, parryAdapter != null && parryAdapter.IsPlayerOwned ? null : owner);
                }
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }
    }
}
