using UnityEngine;

namespace Bao.Enemies
{
    public class ChubblooEnemy : BaoEnemyBase
    {
        [Header("Chubbloo - Shoot")]
        [SerializeField] private GameObject iceProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private float shootDelay = 0.5f;

        [Header("Chubbloo - Hop Movement")]
        [SerializeField, Min(0.1f)] private float hopHorizontalSpeed = 3f;
        [SerializeField, Min(0.1f)] private float hopVerticalSpeed = 4.3f;
        [SerializeField, Min(0f)] private float landingPause = 0.14f;
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.12f;
        [SerializeField, Min(0f)] private float minimumAirTime = 0.12f;
        [SerializeField, Min(0.1f)] private float landingProbeDepth = 1.5f;
        [SerializeField, Range(0.2f, 1f)] private float landingProbeWidth = 0.55f;
        [SerializeField, Min(0.1f)] private float jumpAnimationDuration = 1.6666666f;
        [SerializeField, Range(0f, 0.2f)] private float jumpCrossFadeDuration = 0.04f;

        private static readonly int IdleHash = Animator.StringToHash("isIdle");
        private static readonly int WalkHash = Animator.StringToHash("isWalk");
        private static readonly int HopAnimationSpeedHash = Animator.StringToHash("hopAnimationSpeed");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Small Frog Jump");

        private ChubblooAttackState attackState;
        private ChubblooPatrolState patrolState;
        private ChubblooChaseState chaseState;
        private bool isGrounded;
        private bool isHopping;
        private float hopStartedAt;
        private float nextHopTime;

        public GameObject IceProjectilePrefab => iceProjectilePrefab;
        public Transform ShootPoint => shootPoint;
        public float ShootDelay => shootDelay;
        public bool IsGrounded => isGrounded;
        public bool IsHopping => isHopping;
        public bool CanBeginAttack => isGrounded && !isHopping;
        public ChubblooVFXController VfxController { get; private set; }

        protected override float FacingVisualMultiplier => -1f;

        protected override void Awake()
        {
            base.Awake();
            attackState = new ChubblooAttackState(this);
            patrolState = new ChubblooPatrolState(this);
            chaseState = new ChubblooChaseState(this);
            VfxController = GetComponent<ChubblooVFXController>();

            if (shootDelay > 1.5f)
            {
                shootDelay = 0.5f;
            }

            if (Rigidbody2D != null)
            {
                Rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
                Rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        protected override void Start()
        {
            ApplyFacing(FacingDirection);
            isGrounded = CheckGrounded();
            SetGroundedAnimation();
            StateMachine.SetState(patrolState);
        }

        protected override void FixedUpdate()
        {
            RefreshHopState();
            base.FixedUpdate();
        }

        public override void SetStateToPatrol()
        {
            StateMachine.SetState(patrolState);
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(chaseState);
        }

        public override void SetStateToAttack()
        {
            if (isHopping)
            {
                return;
            }

            StateMachine.SetState(attackState);
        }

        public override void MoveForward(float direction)
        {
            if (isHopping || !isGrounded || Time.time < nextHopTime)
            {
                return;
            }

            float hopDirection = Mathf.Sign(direction == 0f ? FacingDirection : direction);
            if (!CanHopInDirection(hopDirection))
            {
                return;
            }

            BeginHop(hopDirection);
        }

        public override void StopHorizontalMovement()
        {
            if (!isHopping && Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
            }

            if (!isHopping)
            {
                SetGroundedAnimation();
            }
        }

        public override void SetLocomotionAnimation(bool isWalking)
        {
            if (!isWalking && !isHopping)
            {
                SetGroundedAnimation();
            }
        }

        public bool CanHopInDirection(float direction)
        {
            if (Collider2D == null || Mathf.Approximately(direction, 0f))
            {
                return true;
            }

            Bounds bounds = Collider2D.bounds;
            float normalizedDirection = Mathf.Sign(direction);
            Vector2 wallOrigin = new Vector2(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, 0.55f));
            RaycastHit2D wallHit = Physics2D.Raycast(
                wallOrigin,
                Vector2.right * normalizedDirection,
                bounds.extents.x + 0.18f,
                EnvironmentLayer);

            if (IsValidEnvironmentHit(wallHit))
            {
                return false;
            }

            float landingDistance = hopHorizontalSpeed * CalculateAirTime();
            Vector2 landingOrigin = new Vector2(
                bounds.center.x + normalizedDirection * landingDistance,
                bounds.center.y + 0.1f);
            Vector2 probeSize = new Vector2(
                Mathf.Max(0.08f, bounds.size.x * landingProbeWidth),
                0.08f);
            RaycastHit2D landingHit = Physics2D.BoxCast(
                landingOrigin,
                probeSize,
                0f,
                Vector2.down,
                bounds.extents.y + landingProbeDepth,
                EnvironmentLayer);

            return IsValidEnvironmentHit(landingHit);
        }

        public void PrepareForAttack()
        {
            isHopping = false;
            isGrounded = CheckGrounded();
            nextHopTime = Time.time + landingPause;

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
            }

            SetGroundedAnimation();
        }

        public void SpawnProjectile()
        {
            if (iceProjectilePrefab == null || shootPoint == null)
            {
                return;
            }

            GameObject projectile = Instantiate(iceProjectilePrefab, shootPoint.position, Quaternion.identity);
            float direction = FacingDirection;
            projectile.transform.localScale = new Vector3(
                Mathf.Abs(projectile.transform.localScale.x) * direction,
                projectile.transform.localScale.y,
                projectile.transform.localScale.z);

            if (!projectile.TryGetComponent(out ChubblooProjectile bullet))
            {
                bullet = projectile.AddComponent<ChubblooProjectile>();
            }

            bullet.Launch(new Vector2(direction, 0f), 10f, 15);
            VfxController?.TriggerShotFired(shootPoint.position, direction);
        }

        protected override void OnDead()
        {
            base.OnDead();
            VfxController?.TriggerDeathBurst();
        }

        private void BeginHop(float direction)
        {
            FacingDirection = direction;
            ApplyFacing(direction);
            isGrounded = false;
            isHopping = true;
            hopStartedAt = Time.time;

            Rigidbody2D.linearVelocity = new Vector2(direction * hopHorizontalSpeed, hopVerticalSpeed);

            if (Animator != null)
            {
                Animator.SetBool(IdleHash, false);
                Animator.SetBool(WalkHash, true);
                Animator.SetFloat(HopAnimationSpeedHash, CalculateHopAnimationSpeed());

                if (Animator.HasState(0, JumpStateHash))
                {
                    Animator.CrossFadeInFixedTime(JumpStateHash, jumpCrossFadeDuration, 0, 0f);
                }
            }
        }

        private void RefreshHopState()
        {
            bool groundedNow = CheckGrounded();

            if (!isHopping)
            {
                isGrounded = groundedNow;
                return;
            }

            bool minimumTimePassed = Time.time >= hopStartedAt + minimumAirTime;
            bool descending = Rigidbody2D == null || Rigidbody2D.linearVelocity.y <= 0.1f;
            if (!minimumTimePassed || !descending || !groundedNow)
            {
                isGrounded = false;
                return;
            }

            isGrounded = true;
            isHopping = false;
            nextHopTime = Time.time + landingPause;

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
            }

            SetGroundedAnimation();
        }

        private bool CheckGrounded()
        {
            if (Collider2D == null)
            {
                return false;
            }

            Bounds bounds = Collider2D.bounds;
            Vector2 probeSize = new Vector2(Mathf.Max(0.08f, bounds.size.x * 0.65f), 0.06f);
            Vector2 castCenter = new Vector2(bounds.center.x, bounds.min.y + 0.05f);

            RaycastHit2D groundHit = Physics2D.BoxCast(
                castCenter,
                probeSize,
                0f,
                Vector2.down,
                groundCheckDistance + 0.1f,
                EnvironmentLayer);

            return IsValidEnvironmentHit(groundHit);
        }

        private bool IsValidEnvironmentHit(RaycastHit2D hit)
        {
            return hit.collider != null
                && !hit.collider.isTrigger
                && hit.collider.transform.root != transform.root;
        }

        private float CalculateAirTime()
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * (Rigidbody2D != null ? Rigidbody2D.gravityScale : 1f));
            return gravity > 0.01f ? (2f * hopVerticalSpeed) / gravity : jumpAnimationDuration;
        }

        private float CalculateHopAnimationSpeed()
        {
            return Mathf.Clamp(jumpAnimationDuration / Mathf.Max(0.1f, CalculateAirTime()), 0.5f, 3f);
        }

        private void SetGroundedAnimation()
        {
            if (Animator == null)
            {
                return;
            }

            Animator.SetBool(WalkHash, false);
            Animator.SetBool(IdleHash, true);
            Animator.SetFloat(HopAnimationSpeedHash, 1f);
        }
    }
}
