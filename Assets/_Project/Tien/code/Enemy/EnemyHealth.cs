using System.Collections;
using UnityEngine;

namespace Tien
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyHealth : MonoBehaviour, IEnemyTarget
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hurtAnimation = "Hurt";
        [SerializeField] private string deathAnimation = "Death";

        [Header("Control")]
        [SerializeField] private float flySpeed = 25f;
        [SerializeField] private float arrivalDistance = 0.2f;
        [SerializeField] private float deathDestroyDelay = 1f;

        private EnemySystem.EnemyHealthSystem health;
        private Rigidbody2D rb;
        private Collider2D bodyCollider;
        private Coroutine pullCoroutine;
        private float stunTimer;
        private RigidbodyType2D bodyTypeBeforeStun;

        public Transform Transform => transform;
        public bool IsAlive => health != null && !health.IsDead;
        public float CurrentHealth => health != null ? health.CurrentHealth : 0f;

        private void Awake()
        {
            health = new EnemySystem.EnemyHealthSystem(maxHealth);
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void Update()
        {
            TickStun(Time.deltaTime);
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, false);
        }

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive)
            {
                return;
            }

            HealthChangeResult result = health.TakeDamage(damage);
            if (!result.WasApplied)
            {
                return;
            }

            Debug.Log($"Enemy hit. Health left: {result.CurrentHealth}");

            ShowDamagePopup(result.DamageApplied, isCrit);
            Bao.Enemies.WorldSpaceHealthBar.Show(transform, CurrentHealth, maxHealth);
            PlayAnimation(hurtAnimation);

            if (result.KilledTarget)
            {
                Die();
            }
        }

        public void FlyToTarget(Vector3 targetPosition, float holdTime)
        {
            if (!IsAlive)
            {
                return;
            }

            if (pullCoroutine != null)
            {
                StopCoroutine(pullCoroutine);
            }

            pullCoroutine = StartCoroutine(FlyRoutine(targetPosition, holdTime));
        }

        public void ApplyStun(float duration)
        {
            if (!IsAlive || duration <= 0f)
            {
                return;
            }

            if (stunTimer <= 0f)
            {
                bodyTypeBeforeStun = rb.bodyType;
            }

            stunTimer = Mathf.Max(stunTimer, duration);
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        public void ApplyVelocity(Vector2 velocity, bool forceDynamicBody)
        {
            if (!IsAlive)
            {
                return;
            }

            if (forceDynamicBody)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }

            rb.linearVelocity = velocity;
        }

        private IEnumerator FlyRoutine(Vector3 targetPosition, float holdTime)
        {
            float originalGravity = rb.gravityScale;
            bool wasTrigger = bodyCollider.isTrigger;

            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            bodyCollider.isTrigger = true;

            while (IsAlive && Vector2.Distance(transform.position, targetPosition) > arrivalDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, flySpeed * Time.deltaTime);
                yield return null;
            }

            if (IsAlive)
            {
                transform.position = targetPosition;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(holdTime);
            }

            bodyCollider.isTrigger = wasTrigger;
            rb.gravityScale = originalGravity;
            pullCoroutine = null;
        }

        private void TickStun(float deltaTime)
        {
            if (stunTimer <= 0f)
            {
                return;
            }

            stunTimer -= deltaTime;
            if (stunTimer <= 0f && IsAlive)
            {
                rb.bodyType = bodyTypeBeforeStun;
            }
        }

        private void Die()
        {
            Debug.Log("Enemy defeated.");
            Bao.Enemies.WorldSpaceHealthBar.Hide(transform);

            if (pullCoroutine != null)
            {
                StopCoroutine(pullCoroutine);
                pullCoroutine = null;
            }

            PlayAnimation(deathAnimation);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            bodyCollider.enabled = false;
            enabled = false;
            Destroy(gameObject, deathDestroyDelay);
        }

        private void ShowDamagePopup(float damage, bool isCrit)
        {
            global::DamagePopupSystem.ShowEnemy(transform.position + Vector3.up, damage, isCrit);
        }

        private void PlayAnimation(string animationName)
        {
            if (animator != null && !string.IsNullOrEmpty(animationName))
            {
                animator.Play(animationName);
            }
        }
    }
}
