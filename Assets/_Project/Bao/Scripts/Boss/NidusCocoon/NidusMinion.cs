using System.Collections;
using UnityEngine;

namespace Bao.Bosses
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    public class NidusMinion : MonoBehaviour, Bao.Enemies.IDamageable
    {
        [Header("Minion - Health")]
        [SerializeField] private int maxHealth = 15;

        [Header("Minion - Combat")]
        [SerializeField] private int attackDamage = 3;
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float hitDelay = 0.25f;
        [SerializeField] private float attackAnimDuration = 0.6f;
        [SerializeField] private float hitStunDuration = 0.25f;

        [Header("Minion - Warning & Stun")]
        [Tooltip("Kéo object chứa dấu chấm than vào đây")]
        [SerializeField] private GameObject warningObject;
        [SerializeField] private float chargeDelay = 0.4f;
        [Tooltip("Kéo effect xoáng váng/choáng vào đây")]
        [SerializeField] private GameObject stunEffectPrefab;
        [Tooltip("Kéo effect khi chết vào đây")]
        [SerializeField] private GameObject deathVFXPrefab;

        [Header("Minion - Audio")]
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private float pitchVariance = 0.1f;

        [Header("Minion - Movement")]
        [SerializeField] private float moveSpeed = 3.0f;
        [SerializeField] private float detectionRange = 12f;
        [SerializeField] private float facingVisualMultiplier = -1f;

        private EnemySystem.EnemyHealthSystem health;
        public Transform Target { get; private set; }
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public bool IsDead { get; private set; } = false;
        public int CurrentHealth => health != null ? Mathf.RoundToInt(health.CurrentHealth) : maxHealth;
        public NidusCocoonBoss Owner { get; private set; }

        private bool ownerUnregistered;
        private float lastAttackTime = -99f;
        private bool isAttacking = false;
        private bool isStunned = false;
        private float facingDirection = 1f;
        private Coroutine attackCoroutine;

        private void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();

            Rigidbody2D.freezeRotation = true;
            health = new EnemySystem.EnemyHealthSystem(maxHealth);

            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (IsDead || isStunned) return;

            if (Target == null)
            {
                FindPlayer();
                if (Target == null) return;
            }

            float dist = Vector2.Distance(transform.position, Target.position);

            if (isAttacking) return;

            // Attack state decision
            if (dist <= attackRange)
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    attackCoroutine = StartCoroutine(AttackRoutine());
                }
                else
                {
                    // Wait on cooldown: stand idle
                    StopHorizontalMovement();
                    SetWalkAnimation(false);
                }
            }
            // Chase state decision
            else
            {
                FacePosition(Target.position);
                SetWalkAnimation(true);
            }
        }

        private void FixedUpdate()
        {
            if (IsDead || isAttacking || isStunned || Target == null) return;

            float dist = Vector2.Distance(transform.position, Target.position);
            if (dist > attackRange)
            {
                float direction = Target.position.x >= transform.position.x ? 1f : -1f;
                MoveForward(direction);
            }
            else
            {
                StopHorizontalMovement();
            }
        }

        public void InitializeOwner(NidusCocoonBoss owner)
        {
            if (Owner == owner && !ownerUnregistered)
                return;

            if (Owner != null && !ownerUnregistered)
                Owner.UnregisterMinion(this);

            Owner = owner;
            ownerUnregistered = false;
            Owner?.RegisterMinion(this);
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                Target = PlayerRegistry.Current.transform;
                return;
            }
        }

        private void FacePosition(Vector2 position)
        {
            float deltaX = position.x - transform.position.x;
            if (Mathf.Approximately(deltaX, 0f)) return;

            float desiredDirection = Mathf.Sign(deltaX);
            if (!Mathf.Approximately(desiredDirection, facingDirection))
            {
                facingDirection = desiredDirection;
                ApplyFacing(facingDirection);
            }
        }

        private void MoveForward(float direction)
        {
            facingDirection = Mathf.Sign(direction == 0f ? facingDirection : direction);
            ApplyFacing(facingDirection);
            Rigidbody2D.linearVelocity = new Vector2(facingDirection * moveSpeed, Rigidbody2D.linearVelocity.y);
        }

        private void StopHorizontalMovement()
        {
            Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
        }

        private void ApplyFacing(float direction)
        {
            Vector3 scale = transform.localScale;
            float visualDirection = direction * facingVisualMultiplier;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(visualDirection);
            transform.localScale = scale;
        }

        private IEnumerator AttackRoutine()
        {
            isAttacking = true;
            lastAttackTime = Time.time;
            StopHorizontalMovement();
            SetWalkAnimation(false);

            ShowWarning();
            yield return new WaitForSeconds(chargeDelay);
            HideWarning();

            if (Animator != null)
            {
                Animator.SetTrigger("Attack");
            }

            if (attackClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                AudioManager.Instance.PlaySFX(attackClip, transform.position, pitch);
            }

            yield return new WaitForSeconds(hitDelay);

            // Deal Melee Damage
            if (!IsDead && Target != null && Vector2.Distance(transform.position, Target.position) <= attackRange + 0.3f)
            {
                Collider2D playerCollider = Target.GetComponent<Collider2D>();
                if (playerCollider != null)
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        playerCollider,
                        attackDamage,
                        ParryableAttackType.Melee,
                        EnemyParryResponder.Ensure(this),
                        transform,
                        Target.position - transform.position);
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0f, attackAnimDuration - hitDelay));
            isAttacking = false;
            attackCoroutine = null;
        }

        public void TakeDamage(int damage)
        {
            if (IsDead) return;

            HealthChangeResult result = health.TakeDamage(damage);
            if (!result.WasApplied)
            {
                return;
            }

            global::DamagePopupSystem.ShowEnemy(transform.position + Vector3.up, result.DamageApplied);

            if (result.KilledTarget)
            {
                Die();
            }
            else
            {
                StartCoroutine(HitStunRoutine());
            }
        }

        private IEnumerator HitStunRoutine()
        {
            isStunned = true;
            StopHorizontalMovement();
            HideWarning();

            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
                isAttacking = false;
            }

            SetWalkAnimation(false);

            SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in srs) if (sr != null) sr.color = Color.red;

            GameObject stunVfx = null;
            if (stunEffectPrefab != null)
            {
                stunVfx = Instantiate(stunEffectPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
            }

            yield return new WaitForSeconds(hitStunDuration);

            if (stunVfx != null)
            {
                Destroy(stunVfx);
            }

            foreach (var sr in srs) if (sr != null) sr.color = Color.white;
            isStunned = false;
        }

        private void Die()
        {
            IsDead = true;
            UnregisterOwner();
            StopAllCoroutines();
            StopHorizontalMovement();

            Vector3 deathSpawnPos = Collider2D != null ? Collider2D.bounds.center : transform.position + Vector3.up * 1f;

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.gravityScale = 0f;
                Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            if (Collider2D != null)
            {
                Collider2D.enabled = false;
            }

            if (Animator != null)
            {
                Animator.SetTrigger("Die");
            }

            if (deathClip != null && AudioManager.Instance != null)
            {
                float pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
                AudioManager.Instance.PlaySFX(deathClip, transform.position, pitch);
            }

            if (deathVFXPrefab != null)
            {
                Instantiate(deathVFXPrefab, deathSpawnPos, Quaternion.identity);
            }

            Destroy(gameObject, 1f);
        }

        public void DissolveForPhaseTransition(float duration = 0.45f)
        {
            if (IsDead) return;
            StartCoroutine(DissolveForPhaseTransitionRoutine(Mathf.Max(0.18f, duration)));
        }

        private IEnumerator DissolveForPhaseTransitionRoutine(float duration)
        {
            IsDead = true;
            StopHorizontalMovement();
            HideWarning();
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            if (Collider2D != null)
                Collider2D.enabled = false;

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.gravityScale = 0f;
                Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);
            Color[] startColors = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++)
                startColors[i] = srs[i] != null ? srs[i].color : Color.white;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(transform.localScale, transform.localScale * 0.72f, Time.deltaTime * 5f);

                for (int i = 0; i < srs.Length; i++)
                {
                    if (srs[i] == null) continue;
                    Color c = startColors[i];
                    c.a = 1f - t;
                    srs[i].color = c;
                }

                yield return null;
            }

            UnregisterOwner();
            Destroy(gameObject);
        }

        private void UnregisterOwner()
        {
            if (ownerUnregistered) return;
            ownerUnregistered = true;
            NidusCocoonBoss previousOwner = Owner;
            Owner = null;
            previousOwner?.UnregisterMinion(this);
        }

        private void OnDestroy()
        {
            UnregisterOwner();
        }

        private void SetWalkAnimation(bool walk)
        {
            if (Animator != null)
            {
                Animator.SetBool("isWalk", walk);
            }
        }

        private void ShowWarning()
        {
            if (warningObject != null)
            {
                warningObject.SetActive(true);
            }
        }

        private void HideWarning()
        {
            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
