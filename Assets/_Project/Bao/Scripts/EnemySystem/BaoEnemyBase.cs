using System.Collections.Generic;
using UnityEngine;

namespace Bao.Enemies
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    public class BaoEnemyBase : MonoBehaviour
    {
        [Header("Base - Health")]
        [SerializeField] private int maxHealth = 10;

        [Header("Base - Movement")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float patrolDistance = 4f;
        [SerializeField] private float turnPauseTime = 0.2f;

        [Header("Base - Detection")]
        [SerializeField] private float detectionRange = 6f;
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private LayerMask environmentLayer;

        [Header("Base - Combat Hook")]
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackStateDuration = 0.45f;

        [Header("Base - Warning")]
        [Tooltip("Kéo object chứa dấu chấm than vào đây (đã có sẵn Animator popup của bạn)")]
        [SerializeField] protected GameObject warningObject;
        [Tooltip("Thời gian chờ tính từ lúc bắt đầu gọi StartWarningRoutine")]
        [SerializeField] protected float warningDelay = 0f;
        [Tooltip("Kéo object chứa hiệu ứng choáng (ngôi sao) vào đây")]
        [SerializeField] protected GameObject stunEffectObject;
        [Tooltip("Kéo prefab hiệu ứng khi quái chết vào đây")]
        [SerializeField] protected GameObject deathVFXPrefab;

        [Header("Base - Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool logStateChanges = false;

        [Header("Base - UI")]
        protected Bao.Enemies.WorldSpaceHealthBar healthBar;

        private EnemySystem.EnemyHealthSystem health;
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public Transform Target { get; private set; }
        public BaoEnemyStateMachine StateMachine { get; private set; }
        public Vector2 HomePosition { get; private set; }
        public int CurrentHealth => health != null ? Mathf.RoundToInt(health.CurrentHealth) : maxHealth;
        public bool IsDead { get; private set; }
        public Vector3 CenterPosition => Collider2D != null ? Collider2D.bounds.center : transform.position;
        public float FacingDirection { get; protected set; } = 1f;
        public float PatrolDirection => FacingDirection;
        public float DetectionRangeSqr { get; private set; }
        public float AttackRangeSqr { get; private set; }
        public float MoveSpeed => moveSpeed;
        public float PatrolDistance => patrolDistance;
        public float TurnPauseTime => turnPauseTime;
        public float AttackCooldown => attackCooldown;
        public float AttackStateDuration => attackStateDuration;
        protected virtual float FacingVisualMultiplier => 1f;
        public LayerMask EnvironmentLayer => environmentLayer;
        public Transform WarningTransform => warningObject != null ? warningObject.transform : null;
        public Transform StunEffectTransform => stunEffectObject != null ? stunEffectObject.transform : null;
        public SpriteRenderer[] GetVisualSpriteRenderers()
        {
            SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            List<SpriteRenderer> visualRenderers = new List<SpriteRenderer>(allRenderers.Length);

            foreach (SpriteRenderer spriteRenderer in allRenderers)
            {
                Transform rendererTransform = spriteRenderer.transform;
                bool isWarningRenderer = warningObject != null && rendererTransform.IsChildOf(warningObject.transform);
                bool isStunRenderer = stunEffectObject != null && rendererTransform.IsChildOf(stunEffectObject.transform);
                bool isParticleRenderer = spriteRenderer.GetComponentInParent<ParticleSystem>() != null;

                if (!isWarningRenderer && !isStunRenderer && !isParticleRenderer)
                {
                    visualRenderers.Add(spriteRenderer);
                }
            }

            return visualRenderers.ToArray();
        }

        private float lastAttackTime;
        private float turnPauseTimer;
        private BaoEnemyPatrolState patrolState;
        private BaoEnemyChaseState chaseState;
        private BaoEnemyAttackState attackState;
        private BaoEnemyDeadState deadState;
        private bool isTimelineControlled;
        private Transform timelinePreviousTarget;
        private Transform timelineControlTarget;

        private float hitStunTimer;
        protected SpriteRenderer[] spriteRenderers;
        protected Color[] originalColors;
        protected Coroutine warningCoroutine;

        private Material[] baseMaterials;
        private bool isFlashing = false;
        private static Material flashMat;
        private static Material GetFlashMaterial()
        {
            if (flashMat == null)
            {
                flashMat = new Material(Shader.Find("Sprites/Default"));
            }
            return flashMat;
        }

        protected virtual void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();
            StateMachine = new BaoEnemyStateMachine();
            health = new EnemySystem.EnemyHealthSystem(maxHealth);
            spriteRenderers = GetVisualSpriteRenderers();

            if (spriteRenderers != null && spriteRenderers.Length > 0)
            {
                originalColors = new Color[spriteRenderers.Length];
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    originalColors[i] = spriteRenderers[i].color;
                }
            }

            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
            if (stunEffectObject != null)
            {
                stunEffectObject.SetActive(false);
            }

            Rigidbody2D.freezeRotation = true;
            HomePosition = transform.position;
            IsDead = false;
            RefreshRanges();

            if (Animator != null)
            {
                Animator.applyRootMotion = false;
            }

            patrolState = new BaoEnemyPatrolState(this);
            chaseState = new BaoEnemyChaseState(this);
            attackState = new BaoEnemyAttackState(this);
            deadState = new BaoEnemyDeadState(this);

            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(false);
            }
        }

        protected virtual void Start()
        {
            ApplyFacing(FacingDirection);
            StateMachine.SetState(patrolState);
        }

        public void StartWarningRoutine()
        {
            if (stunEffectObject != null && stunEffectObject.activeSelf)
            {
                return;
            }

            if (warningObject == null)
            {
                return;
            }

            StopWarningRoutine();
            warningCoroutine = StartCoroutine(WarningDelayRoutine());
        }

        private System.Collections.IEnumerator WarningDelayRoutine()
        {
            if (warningDelay > 0)
            {
                yield return new WaitForSeconds(warningDelay);
            }
            warningCoroutine = null;
            ShowWarningNow();
        }

        public void ShowWarningNow()
        {
            if (warningObject != null)
            {
                warningObject.SetActive(true);
            }
        }

        public void StopWarningRoutine()
        {
            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }
            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
        }



        public void SetStunEffectActive(bool isActive)
        {
            if (stunEffectObject != null)
            {
                if (isActive)
                {
                    StopWarningRoutine(); // Ưu tiên stun, tắt warning
                }
                stunEffectObject.SetActive(isActive);
            }
        }

        protected virtual void Update()
        {
            if (hitStunTimer > 0f)
            {
                hitStunTimer -= Time.deltaTime;
                StopHorizontalMovement();
                return;
            }

            if (IsDead)
            {
                return;
            }

            if (isTimelineControlled)
            {
                Target = timelineControlTarget;
                StopHorizontalMovement();
                return;
            }

            if (Target == null)
            {
                FindPlayer();
            }

            StateMachine.LogicUpdate();
        }

        protected virtual void FixedUpdate()
        {
            if (hitStunTimer > 0f) return;

            if (IsDead)
            {
                return;
            }

            if (isTimelineControlled)
            {
                StopHorizontalMovement();
                return;
            }

            StateMachine.PhysicsUpdate();
        }

        protected virtual void LateUpdate()
        {
            if (IsDead)
            {
                return;
            }

            if (isTimelineControlled)
            {
                return;
            }

            StateMachine.LateUpdate();
        }

        protected virtual void OnValidate()
        {
            RefreshRanges();
        }

        private void RefreshRanges()
        {
            DetectionRangeSqr = detectionRange * detectionRange;
            AttackRangeSqr = attackRange * attackRange;
        }

        protected void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                Target = PlayerRegistry.Current.transform;
                return;
            }
        }

        public bool HasTargetInDetectionRange()
        {
            if (Target == null)
            {
                return false;
            }

            float sqrDistance = ((Vector2)Target.position - (Vector2)CenterPosition).sqrMagnitude;
            return sqrDistance <= DetectionRangeSqr;
        }

        public bool HasTargetInAttackRange()
        {
            if (Target == null)
            {
                return false;
            }

            float sqrDistance = ((Vector2)Target.position - (Vector2)CenterPosition).sqrMagnitude;
            return sqrDistance <= AttackRangeSqr;
        }

        public bool CanAttackNow()
        {
            return Time.time >= lastAttackTime + attackCooldown;
        }

        public void MarkAttackStarted()
        {
            lastAttackTime = Time.time;
        }

        public void BeginTimelineControl(Transform forcedTarget, bool stopMovement)
        {
            if (!isTimelineControlled)
            {
                timelinePreviousTarget = Target;
            }

            isTimelineControlled = true;
            timelineControlTarget = forcedTarget;
            Target = forcedTarget;

            if (stopMovement)
            {
                StopHorizontalMovement();
                SetLocomotionAnimation(false);
                SetFlyingAnimation(false);
            }
        }

        public void EndTimelineControl(Transform forcedTarget)
        {
            if (!isTimelineControlled)
            {
                return;
            }

            if (forcedTarget != null && timelineControlTarget != null && forcedTarget != timelineControlTarget)
            {
                return;
            }

            Target = timelinePreviousTarget;
            timelinePreviousTarget = null;
            timelineControlTarget = null;
            isTimelineControlled = false;
        }

        public virtual void SetStateToPatrol()
        {
            LogStateChange(patrolState);
            StateMachine.SetState(patrolState);
        }

        public virtual void SetStateToChase()
        {
            LogStateChange(chaseState);
            StateMachine.SetState(chaseState);
        }

        public virtual void SetStateToAttack()
        {
            LogStateChange(attackState);
            StateMachine.SetState(attackState);
        }

        public void SetStateToDead()
        {
            LogStateChange(deadState);
            StateMachine.SetState(deadState);
        }

        private void LogStateChange(BaoEnemyState nextState)
        {
            if (!logStateChanges || nextState == null)
            {
                return;
            }

            Debug.Log($"{name} -> {nextState.GetType().Name}", this);
        }

        public virtual void MoveForward(float direction)
        {
            FacingDirection = Mathf.Sign(direction == 0f ? FacingDirection : direction);
            ApplyFacing(FacingDirection);
            Rigidbody2D.linearVelocity = new Vector2(FacingDirection * moveSpeed, Rigidbody2D.linearVelocity.y);
        }

        public virtual void StopHorizontalMovement()
        {
            Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
        }

        public void PauseTurning()
        {
            turnPauseTimer = turnPauseTime;
        }

        public bool IsTurnPauseActive()
        {
            return turnPauseTimer > 0f;
        }

        public void TickTurnPause()
        {
            if (turnPauseTimer <= 0f)
            {
                return;
            }

            turnPauseTimer -= Time.deltaTime;
            if (turnPauseTimer < 0f)
            {
                turnPauseTimer = 0f;
            }
        }

        public void FlipDirection()
        {
            FacingDirection *= -1f;
            ApplyFacing(FacingDirection);
        }

        public void FacePosition(Vector2 position)
        {
            float deltaX = position.x - CenterPosition.x;
            if (Mathf.Abs(deltaX) < 0.1f)
            {
                return;
            }

            float desiredDirection = Mathf.Sign(deltaX);
            if (!Mathf.Approximately(desiredDirection, FacingDirection))
            {
                FacingDirection = desiredDirection;
                ApplyFacing(FacingDirection);
            }
        }

        protected void ApplyFacing(float direction)
        {
            Vector3 scale = transform.localScale;
            float visualDirection = direction * FacingVisualMultiplier;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(visualDirection);
            transform.localScale = scale;
        }

        public bool ShouldTurnAround()
        {
            Bounds bounds = Collider2D.bounds;
            Vector2 forwardPoint = new Vector2(bounds.center.x + FacingDirection * (bounds.extents.x + 0.05f), bounds.center.y);
            Vector2 frontFeetPoint = new Vector2(bounds.center.x + FacingDirection * (bounds.extents.x + 0.05f), bounds.min.y + 0.05f);

            RaycastHit2D wallHit = Physics2D.Raycast(forwardPoint, Vector2.right * FacingDirection, 0.15f, environmentLayer);
            RaycastHit2D groundAheadHit = Physics2D.Raycast(frontFeetPoint, Vector2.down, 0.3f, environmentLayer);

            bool hitWall = wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.transform.root != transform.root && wallHit.distance > 0f;
            bool missingGround = groundAheadHit.collider == null || groundAheadHit.collider.transform.root == transform.root;

            float displacement = transform.position.x - HomePosition.x;
            bool movingAwayFromHome = (displacement > 0f && FacingDirection > 0f) || (displacement < 0f && FacingDirection < 0f);
            bool exceededPatrol = Mathf.Abs(displacement) >= patrolDistance && movingAwayFromHome;

            return hitWall || missingGround || exceededPatrol;
        }

        public bool ShouldTurnAroundFlying()
        {
            Bounds bounds = Collider2D.bounds;
            Vector2 forwardPoint = new Vector2(bounds.center.x + FacingDirection * (bounds.extents.x + 0.05f), bounds.center.y);

            RaycastHit2D wallHit = Physics2D.Raycast(forwardPoint, Vector2.right * FacingDirection, 0.15f, environmentLayer);
            bool hitWall = wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.transform.root != transform.root && wallHit.distance > 0f;

            float displacement = transform.position.x - HomePosition.x;
            bool movingAwayFromHome = (displacement > 0f && FacingDirection > 0f) || (displacement < 0f && FacingDirection < 0f);
            bool exceededPatrol = Mathf.Abs(displacement) >= patrolDistance && movingAwayFromHome;

            return hitWall || exceededPatrol;
        }

        private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType type)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null || string.IsNullOrEmpty(paramName))
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
            {
                if (parameter.type == type && parameter.name == paramName)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void PlayAttackAnimation()
        {
            if (Animator == null)
            {
                return;
            }

            if (HasAnimatorParameter("Attack", AnimatorControllerParameterType.Trigger))
            {
                Animator.SetTrigger("Attack");
            }
            else if (HasAnimatorParameter("isAttack", AnimatorControllerParameterType.Trigger))
            {
                Animator.SetTrigger("isAttack");
            }
        }

        public virtual void PlayShootAnimation()
        {
            if (Animator != null && HasAnimatorParameter("Shoot", AnimatorControllerParameterType.Trigger))
            {
                Animator.SetTrigger("Shoot");
            }
        }

        public virtual void SetLocomotionAnimation(bool isWalking)
        {
            if (Animator == null)
            {
                return;
            }

            if (HasAnimatorParameter("isWalk", AnimatorControllerParameterType.Bool))
            {
                Animator.SetBool("isWalk", isWalking);
            }

            if (HasAnimatorParameter("isIdle", AnimatorControllerParameterType.Bool))
            {
                Animator.SetBool("isIdle", !isWalking);
            }
        }

        public virtual void SetIdleAnimation(bool isIdle)
        {
            if (Animator != null && HasAnimatorParameter("isIdle", AnimatorControllerParameterType.Bool))
            {
                Animator.SetBool("isIdle", isIdle);
            }
        }

        public void SetFlyingAnimation(bool isFlying)
        {
            if (Animator != null && HasAnimatorParameter("IsFlying", AnimatorControllerParameterType.Bool))
            {
                Animator.SetBool("IsFlying", isFlying);
            }
        }

        public virtual void TakeDamage(int damage)
        {
            ApplyDamage(damage, false);
        }

        public virtual void TakeDamage(float damage, bool isCrit = false)
        {
            ApplyDamage(Mathf.RoundToInt(damage), isCrit);
        }

        private void ApplyDamage(int damage, bool isCrit)
        {
            if (IsDead)
            {
                return;
            }

            HealthChangeResult result = health.TakeDamage(damage);
            if (!result.WasApplied)
            {
                return;
            }

            Vector3 healthBarAnchor = Collider2D != null
                ? new Vector3(transform.position.x, Collider2D.bounds.max.y, transform.position.z)
                : transform.position;
            Bao.Enemies.WorldSpaceHealthBar.Show(transform, healthBarAnchor, CurrentHealth, maxHealth);

            ShowDamagePopup(result.DamageApplied, isCrit);

            PlayHitEffect();

            if (result.KilledTarget)
            {
                Die();
            }
        }

        public void ApplyKnockback(Transform attacker, float force)
        {
            if (IsDead || Rigidbody2D == null) return;
            Vector2 direction = (transform.position - attacker.position).normalized;
            direction.y = 0f;
            Rigidbody2D.linearVelocity = Vector2.zero;
            Rigidbody2D.AddForce(direction.normalized * force, ForceMode2D.Impulse);
        }

        [ContextMenu("Test Hit Effect (Play Mode Only)")]
        public void TestHitEffect()
        {
            if (Application.isPlaying)
            {
                PlayHitEffect();
            }
            else
            {
                Debug.LogWarning("Please enter Play Mode to test hit effects!");
            }
        }

        [ContextMenu("Test Hit Flash Only (Play Mode Only)")]
        public void TestHitFlashOnly()
        {
            if (Application.isPlaying)
            {
                if (spriteRenderers != null && spriteRenderers.Length > 0)
                {
                    StopCoroutine("HitFlashRoutine");
                    StartCoroutine(HitFlashRoutine());
                }
            }
            else
            {
                Debug.LogWarning("Please enter Play Mode to test hit flashes!");
            }
        }

        protected virtual void PlayHitEffect()
        {
            if (!IsDead && Animator != null)
            {
                Animator.SetTrigger("Hit");
            }

            hitStunTimer = 0.25f;

            if (spriteRenderers != null && spriteRenderers.Length > 0)
            {
                if (!isFlashing)
                {
                    if (baseMaterials == null || baseMaterials.Length != spriteRenderers.Length)
                    {
                        baseMaterials = new Material[spriteRenderers.Length];
                    }
                    for (int i = 0; i < spriteRenderers.Length; i++)
                    {
                        if (spriteRenderers[i] != null)
                        {
                            baseMaterials[i] = spriteRenderers[i].sharedMaterial;
                        }
                    }
                }

                StopCoroutine("HitFlashRoutine");
                StartCoroutine(HitFlashRoutine());
            }
        }

        protected virtual System.Collections.IEnumerator HitFlashRoutine()
        {
            if (originalColors == null || spriteRenderers == null || spriteRenderers.Length == 0)
            {
                yield break;
            }

            isFlashing = true;
            Material fMat = GetFlashMaterial();

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].sharedMaterial = fMat;
                    Color c = Color.white;
                    c.a = spriteRenderers[i].color.a;
                    spriteRenderers[i].color = c;
                }
            }

            yield return new WaitForSecondsRealtime(0.05f);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    Color c = Color.red;
                    c.a = spriteRenderers[i].color.a;
                    spriteRenderers[i].color = c;
                }
            }

            yield return new WaitForSecondsRealtime(0.1f);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    Color c = originalColors[i];
                    c.a = spriteRenderers[i].color.a;
                    spriteRenderers[i].color = c;
                    spriteRenderers[i].sharedMaterial = baseMaterials[i];
                }
            }

            isFlashing = false;
        }

        private void ShowDamagePopup(float damage, bool isCrit)
        {
            global::DamagePopupSystem.ShowEnemy(CenterPosition + Vector3.up, damage, isCrit);
        }

        public void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            SetStateToDead();

            // Bao.Enemies.WorldSpaceHealthBar.Hide(transform);

            if (Animator != null)
            {
                SetLocomotionAnimation(false);
                SetFlyingAnimation(false);
                Animator.SetTrigger("Die");
            }

            StopHorizontalMovement();
            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.gravityScale = 0f;
                Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            Vector3 spawnPos = CenterPosition;

            if (Collider2D != null)
            {
                Collider2D.enabled = false;
            }

            if (deathVFXPrefab != null)
            {
                Instantiate(deathVFXPrefab, spawnPos, Quaternion.identity);
            }

            OnDead();
            Destroy(gameObject, 2f);
        }

        protected virtual void OnDead() { }

        public bool IsPlayerInFront()
        {
            if (Target == null)
            {
                return false;
            }

            float deltaX = Target.position.x - CenterPosition.x;
            return Mathf.Sign(deltaX) == FacingDirection || Mathf.Approximately(deltaX, 0f);
        }

        public virtual void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CenterPosition, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(CenterPosition, attackRange);

            Gizmos.color = Color.cyan;
            Vector3 left = HomePosition + Vector2.left * patrolDistance;
            Vector3 right = HomePosition + Vector2.right * patrolDistance;
            Gizmos.DrawLine(left, right);
        }
    }
}
