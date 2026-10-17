using UnityEngine;

namespace Bao.Enemies
{
    public sealed class BugEnemy : BaoEnemyBase
    {
        private static readonly int FlyStateHash = Animator.StringToHash("Fly");

        private enum MovementMode
        {
            Airborne,
            Descending,
            Grounded,
            TakingOff
        }

        [Header("Bug - Flight")]
        [SerializeField, Min(0f)] private float flightHeight = 3f;
        [SerializeField, Min(0f)] private float descentSpeed = 5f;
        [SerializeField, Min(0f)] private float takeoffSpeed = 4f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 12f;
        [SerializeField, Min(0f)] private float landingClearance = 0.05f;

        [Header("Bug - Ground Combat")]
        [SerializeField, Min(0f)] private float groundMoveSpeed = 3f;
        [SerializeField, Min(0f)] private float groundGravityScale = 3f;
        [SerializeField, Min(0f)] private float attackWindup = 0.25f;
        [SerializeField, Min(0f)] private float lungeSpeed = 6f;
        [SerializeField, Min(0f)] private float lungeDuration = 0.18f;
        [SerializeField, Min(0f)] private float attackRecovery = 0.3f;
        [SerializeField, Min(1)] private int attackDamage = 10;
        [SerializeField] private Vector2 hitboxOffset = new Vector2(-3.2f, 2.8f);
        [SerializeField] private Vector2 hitboxSize = new Vector2(2.4f, 2.2f);

        protected override float FacingVisualMultiplier => -1f;

        public float FlightHeight => flightHeight;
        public float DescentSpeed => descentSpeed;
        public float TakeoffSpeed => takeoffSpeed;
        public float GroundProbeDistance => groundProbeDistance;
        public float LandingClearance => landingClearance;
        public float GroundMoveSpeed => groundMoveSpeed;
        public float GroundGravityScale => groundGravityScale;
        public float AttackWindup => attackWindup;
        public float LungeSpeed => lungeSpeed;
        public float LungeDuration => lungeDuration;
        public float AttackRecovery => attackRecovery;
        public int AttackDamage => attackDamage;
        public Vector2 HitboxOffset => hitboxOffset;
        public Vector2 HitboxSize => hitboxSize;
        public Vector2 FlightHomePosition { get; private set; }

        private MovementMode movementMode;
        private BugFlyingPatrolState flyingPatrolState;
        private BugDescendState descendState;
        private BugGroundChaseState groundChaseState;
        private BugBiteAttackState biteAttackState;
        private BugTakeoffState takeoffState;
        private GameObject attackHitboxObject;
        private BugVFXController vfx;

        public Transform PlayerTarget
        {
            get
            {
                Player player = PlayerRegistry.Current;
                return player != null && player.isActiveAndEnabled ? player.transform : null;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            flyingPatrolState = new BugFlyingPatrolState(this);
            descendState = new BugDescendState(this);
            groundChaseState = new BugGroundChaseState(this);
            biteAttackState = new BugBiteAttackState(this);
            takeoffState = new BugTakeoffState(this);

            Rigidbody2D.gravityScale = 0f;
            CreateAttackHitbox();

            vfx = new BugVFXController(this);
            vfx.Initialize();
        }

        protected override void Start()
        {
            ApplyFacing(FacingDirection);
            FlightHomePosition = Rigidbody2D.position + Vector2.up * flightHeight;
            Rigidbody2D.position = FlightHomePosition;
            MarkAirborne();
            StateMachine.SetState(flyingPatrolState);

            if (Animator != null)
            {
                Animator.Play(FlyStateHash, 0, 0f);
                Animator.Update(0f);
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            flightHeight = Mathf.Max(0f, flightHeight);
            descentSpeed = Mathf.Max(0f, descentSpeed);
            takeoffSpeed = Mathf.Max(0f, takeoffSpeed);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            landingClearance = Mathf.Max(0f, landingClearance);
            groundMoveSpeed = Mathf.Max(0f, groundMoveSpeed);
            groundGravityScale = Mathf.Max(0f, groundGravityScale);
            attackWindup = Mathf.Max(0f, attackWindup);
            lungeSpeed = Mathf.Max(0f, lungeSpeed);
            lungeDuration = Mathf.Max(0f, lungeDuration);
            attackRecovery = Mathf.Max(0f, attackRecovery);
            attackDamage = Mathf.Max(1, attackDamage);
            hitboxSize.x = Mathf.Max(0.01f, hitboxSize.x);
            hitboxSize.y = Mathf.Max(0.01f, hitboxSize.y);
        }

        public override void SetStateToPatrol()
        {
            if (movementMode == MovementMode.Grounded || movementMode == MovementMode.Descending)
            {
                movementMode = MovementMode.TakingOff;
                StateMachine.SetState(takeoffState);
                return;
            }

            if (movementMode != MovementMode.TakingOff)
            {
                MarkAirborne();
                StateMachine.SetState(flyingPatrolState);
            }
        }

        public override void SetStateToChase()
        {
            if (movementMode == MovementMode.Grounded)
            {
                StateMachine.SetState(groundChaseState);
                return;
            }

            if (movementMode == MovementMode.Airborne)
            {
                movementMode = MovementMode.Descending;
                StateMachine.SetState(descendState);
            }
        }

        public override void SetStateToAttack()
        {
            if (movementMode == MovementMode.Grounded)
            {
                StateMachine.SetState(biteAttackState);
            }
        }

        public override void SetLocomotionAnimation(bool isWalking)
        {
            base.SetLocomotionAnimation(isWalking);
        }

        public bool HasPlayerInDetectionRange()
        {
            return IsPlayerWithinSqrRange(DetectionRangeSqr);
        }

        public bool HasPlayerInAttackRange()
        {
            return IsPlayerWithinSqrRange(AttackRangeSqr);
        }

        public void MoveHorizontal(float direction, float speed)
        {
            if (Mathf.Approximately(direction, 0f))
            {
                StopHorizontalMovement();
                return;
            }

            float signedDirection = Mathf.Sign(direction);
            FacePosition((Vector2)CenterPosition + Vector2.right * signedDirection);
            Rigidbody2D.linearVelocity = new Vector2(
                signedDirection * speed,
                Rigidbody2D.linearVelocity.y);
        }

        public void MarkAirborne()
        {
            movementMode = MovementMode.Airborne;
            Rigidbody2D.gravityScale = 0f;
            SetAttackHitboxActive(false);
            vfx?.SetFlying(true);
        }

        public void MarkGrounded()
        {
            movementMode = MovementMode.Grounded;
            Rigidbody2D.gravityScale = groundGravityScale;
            SetAttackHitboxActive(false);
            vfx?.SetFlying(false);
        }

        public bool TryGetLandingPosition(out Vector2 landingPosition)
        {
            landingPosition = Rigidbody2D.position;
            Transform playerTarget = PlayerTarget;
            if (playerTarget == null)
            {
                return false;
            }

            float rayOriginY = Mathf.Max(CenterPosition.y, playerTarget.position.y + 0.5f);
            Vector2 rayOrigin = new Vector2(playerTarget.position.x, rayOriginY);
            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                groundProbeDistance,
                EnvironmentLayer);

            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.root == transform.root)
            {
                return false;
            }

            float rootToBottom = transform.position.y - Collider2D.bounds.min.y;
            landingPosition = new Vector2(
                hit.point.x,
                hit.point.y + rootToBottom + landingClearance);
            return true;
        }

        public void SetAttackHitboxActive(bool active)
        {
            if (attackHitboxObject != null && attackHitboxObject.activeSelf != active)
            {
                attackHitboxObject.SetActive(active);
            }
        }

        public bool TryDealBiteDamage()
        {
            Transform playerTarget = PlayerTarget;
            if (playerTarget == null || attackHitboxObject == null)
            {
                return false;
            }

            Vector3 lossyScale = transform.lossyScale;
            Vector2 worldSize = new Vector2(
                hitboxSize.x * Mathf.Abs(lossyScale.x),
                hitboxSize.y * Mathf.Abs(lossyScale.y));
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                attackHitboxObject.transform.position,
                worldSize,
                0f);

            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.transform.root != playerTarget.root)
                {
                    continue;
                }

                PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    attackDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(this),
                    transform,
                    hit.bounds.center - CenterPosition,
                    canBeBlocked: true);
                return true;
            }

            return false;
        }

        public void ShowDetectionVFX()
        {
            vfx?.ShowDetection();
        }

        public void HideDetectionVFX()
        {
            vfx?.HideDetection();
        }

        public void SetDescendingVFX(bool active)
        {
            vfx?.SetDescending(active);
        }

        public void PlayLandingVFX()
        {
            vfx?.PlayLanding();
        }

        public void PlayAttackTelegraphVFX()
        {
            vfx?.PlayAttackTelegraph();
        }

        public void PlayBiteVFX()
        {
            vfx?.PlayBite(hitboxOffset);
        }

        public void StopAttackVFX()
        {
            vfx?.StopAttack();
        }

        public void PlayTakeoffVFX()
        {
            vfx?.PlayTakeoff();
        }

        protected override void OnDead()
        {
            SetAttackHitboxActive(false);
            HideDetectionVFX();
            SetDescendingVFX(false);
            vfx?.PlayDeath();
        }

        public override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundProbeDistance);
        }

        private bool IsPlayerWithinSqrRange(float sqrRange)
        {
            Transform playerTarget = PlayerTarget;
            if (playerTarget == null)
            {
                return false;
            }

            return ((Vector2)playerTarget.position - (Vector2)CenterPosition).sqrMagnitude <= sqrRange;
        }

        private void CreateAttackHitbox()
        {
            attackHitboxObject = new GameObject("Bug Bite Hitbox");
            attackHitboxObject.layer = gameObject.layer;
            attackHitboxObject.transform.SetParent(transform, false);
            attackHitboxObject.transform.localPosition = hitboxOffset;

            BoxCollider2D hitboxCollider = attackHitboxObject.AddComponent<BoxCollider2D>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.size = hitboxSize;
            attackHitboxObject.SetActive(false);
        }

        private sealed class BugFlyingPatrolState : BaoEnemyState
        {
            private readonly BugEnemy bug;
            private bool hasFlipped;

            public BugFlyingPatrolState(BugEnemy bug) : base(bug)
            {
                this.bug = bug;
            }

            public override void Enter()
            {
                hasFlipped = false;
                bug.PauseTurning();
                bug.MarkAirborne();
                bug.SetFlyingAnimation(true);

                if (bug.Animator != null)
                {
                    bug.Animator.Play(FlyStateHash, 0, 0f);
                }
            }

            public override void LogicUpdate()
            {
                bug.TickTurnPause();
                if (bug.HasPlayerInDetectionRange())
                {
                    bug.SetStateToChase();
                }
            }

            public override void PhysicsUpdate()
            {
                float yVelocity = Mathf.Clamp(
                    (bug.FlightHomePosition.y - bug.transform.position.y) * 2f,
                    -bug.MoveSpeed,
                    bug.MoveSpeed);

                if (bug.IsTurnPauseActive())
                {
                    bug.Rigidbody2D.linearVelocity = new Vector2(0f, yVelocity);
                    return;
                }

                if (bug.ShouldTurnAroundFlying())
                {
                    if (!hasFlipped)
                    {
                        bug.FlipDirection();
                        bug.PauseTurning();
                        hasFlipped = true;
                    }

                    bug.Rigidbody2D.linearVelocity = new Vector2(0f, yVelocity);
                    return;
                }

                hasFlipped = false;
                bug.Rigidbody2D.linearVelocity = new Vector2(
                    bug.Rigidbody2D.linearVelocity.x,
                    yVelocity);
                bug.MoveHorizontal(bug.PatrolDirection, bug.MoveSpeed);
            }
        }

        private sealed class BugDescendState : BaoEnemyState
        {
            private readonly BugEnemy bug;
            private Vector2 landingPosition;

            public BugDescendState(BugEnemy bug) : base(bug)
            {
                this.bug = bug;
            }

            public override void Enter()
            {
                bug.Rigidbody2D.gravityScale = 0f;
                bug.SetFlyingAnimation(true);
                bug.StopHorizontalMovement();
                bug.ShowDetectionVFX();
                bug.SetDescendingVFX(true);

                if (!bug.TryGetLandingPosition(out landingPosition))
                {
                    bug.SetStateToPatrol();
                }
            }

            public override void LogicUpdate()
            {
                if (!bug.HasPlayerInDetectionRange() || !bug.TryGetLandingPosition(out landingPosition))
                {
                    bug.SetStateToPatrol();
                    return;
                }

                if (Vector2.Distance(bug.Rigidbody2D.position, landingPosition) <= 0.08f)
                {
                    bug.Rigidbody2D.position = landingPosition;
                    bug.Rigidbody2D.linearVelocity = Vector2.zero;
                    bug.MarkGrounded();
                    bug.PlayLandingVFX();
                    bug.HideDetectionVFX();
                    bug.SetStateToChase();
                }
            }

            public override void PhysicsUpdate()
            {
                Vector2 direction = landingPosition - bug.Rigidbody2D.position;
                if (direction.sqrMagnitude <= 0.0064f)
                {
                    bug.Rigidbody2D.linearVelocity = Vector2.zero;
                    return;
                }

                bug.FacePosition(landingPosition);
                bug.Rigidbody2D.linearVelocity = direction.normalized * bug.DescentSpeed;
            }

            public override void Exit()
            {
                bug.SetDescendingVFX(false);
            }
        }

        private sealed class BugGroundChaseState : BaoEnemyState
        {
            private readonly BugEnemy bug;

            public BugGroundChaseState(BugEnemy bug) : base(bug)
            {
                this.bug = bug;
            }

            public override void Enter()
            {
                bug.MarkGrounded();
                bug.SetFlyingAnimation(false);
            }

            public override void LogicUpdate()
            {
                if (!bug.HasPlayerInDetectionRange())
                {
                    bug.SetStateToPatrol();
                    return;
                }

                if (bug.HasPlayerInAttackRange() && bug.CanAttackNow())
                {
                    bug.SetStateToAttack();
                }
            }

            public override void PhysicsUpdate()
            {
                Transform playerTarget = bug.PlayerTarget;
                if (playerTarget == null || bug.HasPlayerInAttackRange())
                {
                    bug.StopHorizontalMovement();
                    return;
                }

                float direction = playerTarget.position.x >= bug.CenterPosition.x ? 1f : -1f;
                bug.MoveHorizontal(direction, bug.GroundMoveSpeed);
            }
        }

        private sealed class BugBiteAttackState : BaoEnemyState
        {
            private enum AttackPhase
            {
                Windup,
                Lunge,
                Recovery
            }

            private readonly BugEnemy bug;
            private AttackPhase phase;
            private float phaseStartTime;
            private float lungeDirection;
            private bool hasDealtDamage;

            public BugBiteAttackState(BugEnemy bug) : base(bug)
            {
                this.bug = bug;
            }

            public override void Enter()
            {
                phase = AttackPhase.Windup;
                phaseStartTime = Time.time;
                hasDealtDamage = false;
                bug.StopHorizontalMovement();
                bug.SetFlyingAnimation(false);
                bug.MarkAttackStarted();

                Transform playerTarget = bug.PlayerTarget;
                if (playerTarget != null)
                {
                    bug.FacePosition(playerTarget.position);
                }

                lungeDirection = bug.FacingDirection;
                bug.SetAttackHitboxActive(false);
                bug.PlayAttackTelegraphVFX();
                bug.PlayAttackAnimation();
            }

            public override void LogicUpdate()
            {
                float elapsed = Time.time - phaseStartTime;
                if (phase == AttackPhase.Windup && elapsed >= bug.AttackWindup)
                {
                    phase = AttackPhase.Lunge;
                    phaseStartTime = Time.time;
                    lungeDirection = bug.FacingDirection;
                    bug.SetAttackHitboxActive(true);
                    bug.PlayBiteVFX();
                    return;
                }

                if (phase == AttackPhase.Lunge && elapsed >= bug.LungeDuration)
                {
                    phase = AttackPhase.Recovery;
                    phaseStartTime = Time.time;
                    bug.SetAttackHitboxActive(false);
                    bug.StopHorizontalMovement();
                    return;
                }

                if (phase == AttackPhase.Recovery && elapsed >= bug.AttackRecovery)
                {
                    if (bug.HasPlayerInDetectionRange())
                    {
                        bug.SetStateToChase();
                    }
                    else
                    {
                        bug.SetStateToPatrol();
                    }
                }
            }

            public override void PhysicsUpdate()
            {
                if (phase == AttackPhase.Lunge)
                {
                    bug.MoveHorizontal(lungeDirection, bug.LungeSpeed);
                    if (!hasDealtDamage)
                    {
                        hasDealtDamage = bug.TryDealBiteDamage();
                    }
                }
                else
                {
                    bug.StopHorizontalMovement();
                }
            }

            public override void Exit()
            {
                bug.SetAttackHitboxActive(false);
                bug.StopAttackVFX();
            }
        }

        private sealed class BugTakeoffState : BaoEnemyState
        {
            private readonly BugEnemy bug;

            public BugTakeoffState(BugEnemy bug) : base(bug)
            {
                this.bug = bug;
            }

            public override void Enter()
            {
                bug.SetAttackHitboxActive(false);
                bug.HideDetectionVFX();
                bug.Rigidbody2D.gravityScale = 0f;
                bug.Rigidbody2D.linearVelocity = Vector2.zero;
                bug.SetFlyingAnimation(true);
                bug.PlayTakeoffVFX();
                bug.vfx?.SetFlying(true);
            }

            public override void PhysicsUpdate()
            {
                float deltaY = bug.FlightHomePosition.y - bug.transform.position.y;
                if (Mathf.Abs(deltaY) <= 0.08f)
                {
                    bug.Rigidbody2D.position = new Vector2(
                        bug.Rigidbody2D.position.x,
                        bug.FlightHomePosition.y);
                    bug.Rigidbody2D.linearVelocity = Vector2.zero;
                    bug.MarkAirborne();
                    bug.StateMachine.SetState(bug.flyingPatrolState);
                    return;
                }

                bug.Rigidbody2D.linearVelocity = new Vector2(
                    0f,
                    Mathf.Sign(deltaY) * bug.TakeoffSpeed);
            }
        }
    }
}
