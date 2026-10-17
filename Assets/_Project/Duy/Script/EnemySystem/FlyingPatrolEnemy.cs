using UnityEngine;
using EnemySystem;
using Bao.Enemies;

namespace EnemySystem
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class FlyingPatrolEnemy : MonoBehaviour, EnemySystem.IDamageable, Bao.Enemies.IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        [Header("Ping-Pong Movement (Bay qua lại)")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float patrolDistance = 4f;
        [SerializeField] private bool moveVertical = false;
        [SerializeField] private bool invertFlip = false;
        [SerializeField] private float waveAmplitude = 0.3f;
        [SerializeField] private float waveFrequency = 3f;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private float obstacleCheckDistance = 0.5f;

        [Header("Ranged Attack (Bắn lông vũ)")]
        [SerializeField] private GameObject featherProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float attackRange = 8f;
        [SerializeField] private float attackCooldown = 2.5f;
        [SerializeField] private float shootDelay = 0.4f; // Delay truoc khi ban (khop anim)
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private float projectileSpawnRotation = 0f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string flyBool = "isWalk";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string dieTrigger = "Die";

        private Rigidbody2D rb;
        private Collider2D col;
        private Vector2 startPos;
        private int direction = 1;
        private float nextAttackTime = 0f;
        private bool isAttacking = false;
        private bool isDead = false;

        private bool hasFlyBool;
        private bool hasAttackTrigger;
        private bool hasHurtTrigger;
        private bool hasDieTrigger;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            rb.gravityScale = 0f;
            if (animator == null) animator = GetComponent<Animator>();
            CacheAnimatorParameters();
            currentHealth = maxHealth;
            startPos = transform.position;
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == flyBool) hasFlyBool = true;
                else if (param.name == attackTrigger) hasAttackTrigger = true;
                else if (param.name == hurtTrigger) hasHurtTrigger = true;
                else if (param.name == dieTrigger) hasDieTrigger = true;
            }
        }

        private void Update()
        {
            if (isDead) return;

            Collider2D player = CheckPlayerInRange();
            if (player != null && Time.time >= nextAttackTime && !isAttacking)
            {
                StartCoroutine(AttackRoutine(player.transform));
            }

            if (!isAttacking)
            {
                FlyPatrol();
            }
        }

        private void FlyPatrol()
        {
            float halfDist = patrolDistance * 0.5f;

            if (moveVertical)
            {
                float step = direction * moveSpeed * Time.deltaTime;
                float nextY = transform.position.y + step;

                if (nextY > startPos.y + halfDist)
                {
                    direction = -1;
                }
                else if (nextY < startPos.y - halfDist)
                {
                    direction = 1;
                }

                transform.position += new Vector3(0f, direction * moveSpeed * Time.deltaTime, 0f);
            }
            else
            {
                float step = direction * moveSpeed * Time.deltaTime;
                float nextX = transform.position.x + step;

                if (nextX > startPos.x + halfDist)
                {
                    direction = -1;
                }
                else if (nextX < startPos.x - halfDist)
                {
                    direction = 1;
                }

                float waveY = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude * Time.deltaTime;
                transform.position += new Vector3(direction * moveSpeed * Time.deltaTime, waveY, 0f);

                FaceDirection(direction);
            }

            if (animator != null && hasFlyBool) animator.SetBool(flyBool, true);
        }

        private void FaceDirection(int dir)
        {
            Vector3 scale = transform.localScale;
            float flipMultiplier = invertFlip ? 1f : -1f;
            scale.x = Mathf.Abs(scale.x) * dir * flipMultiplier;
            transform.localScale = scale;
        }

        private Collider2D CheckPlayerInRange()
        {
            Vector2 checkPos = shootPoint != null ? shootPoint.position : transform.position;
            return Physics2D.OverlapCircle(checkPos, attackRange, playerLayer);
        }

        private System.Collections.IEnumerator AttackRoutine(Transform player)
        {
            isAttacking = true;
            nextAttackTime = Time.time + attackCooldown;
            rb.linearVelocity = Vector2.zero;

            // Quay mat ve phia Player truoc khi ban
            if (player != null)
            {
                int dirToPlayer = player.position.x >= transform.position.x ? 1 : -1;
                FaceDirection(dirToPlayer);
            }

            if (animator != null)
            {
                if (hasFlyBool) animator.SetBool(flyBool, false);
                if (hasAttackTrigger) animator.SetTrigger(attackTrigger);
            }

            yield return new WaitForSeconds(shootDelay);

            if (!isDead)
            {
                SpawnProjectile(player);
            }

            yield return new WaitForSeconds(0.3f);
            isAttacking = false;
        }

        private void SpawnProjectile(Transform player)
        {
            if (featherProjectilePrefab == null) return;

            Transform spawnTr = shootPoint != null ? shootPoint : transform;
            Vector2 shotDirection = transform.right;

            if (player != null)
            {
                shotDirection = ((Vector2)player.position - (Vector2)spawnTr.position).normalized;
            }

            // Tinh goc quay ban theo huong Player
            float angle = Mathf.Atan2(shotDirection.y, shotDirection.x) * Mathf.Rad2Deg + projectileSpawnRotation;
            GameObject proj = Instantiate(featherProjectilePrefab, spawnTr.position, Quaternion.Euler(0f, 0f, angle));

            if (proj.TryGetComponent<Rigidbody2D>(out var projRb))
            {
                projRb.gravityScale = 0f;
                projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                projRb.linearVelocity = shotDirection * projectileSpeed;
            }
        }

        public void TakeDamage(int damage)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            WorldSpaceHealthBar.Show(transform, currentHealth, maxHealth);

            if (animator != null && hasHurtTrigger) animator.SetTrigger(hurtTrigger);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void Die()
        {
            if (isDead) return;
            isDead = true;

            StopAllCoroutines();
            WorldSpaceHealthBar.Hide(transform);
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 2f;

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            if (animator != null && hasDieTrigger) animator.SetTrigger(dieTrigger);
            Destroy(gameObject, 1.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 origin = Application.isPlaying ? startPos : (Vector2)transform.position;
            float halfDist = patrolDistance * 0.5f;

            Gizmos.color = Color.cyan;
            if (moveVertical)
            {
                Gizmos.DrawLine(origin + Vector2.up * halfDist, origin - Vector2.up * halfDist);
            }
            else
            {
                Gizmos.DrawLine(origin + Vector2.left * halfDist, origin + Vector2.right * halfDist);
            }

            Vector2 checkPos = shootPoint != null ? shootPoint.position : transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(checkPos, attackRange);
        }
    }
}
