using UnityEngine;

namespace Bao.Enemies
{
    public class MoltenfishEnemy : BaoEnemyBase
    {
        [Header("Moltenfish - Jump Bite")]
        [SerializeField] private int biteDamage = 25;
        [SerializeField] private float jumpHeight = 4f;
        [SerializeField] private Transform[] jumpPoints;
        [SerializeField] private float jumpPointThreshold = 0.5f;
        [SerializeField] private float jumpCooldown = 2f;

        public int BiteDamage => biteDamage;
        public float JumpHeight => jumpHeight;
        public Transform[] JumpPoints => jumpPoints;
        public float JumpPointThreshold => jumpPointThreshold;

        [HideInInspector] public float LastJumpTime = -10f;
        private Transform lastJumpPoint = null;
        public MoltenfishVFXController VFXController { get; private set; }

        [Header("Moltenfish - Swimming")]
        [SerializeField] private float swimSpeed = 2.5f;
        [SerializeField] private float swimDepth = 1.5f;
        [SerializeField] private float swimHeightLimit = 0.5f;

        public float SwimSpeed => swimSpeed;
        public float SwimDepth => swimDepth;
        public float SwimHeightLimit => swimHeightLimit;

        protected override float FacingVisualMultiplier => -1f;

        private MoltenfishJumpState jumpState;
        private MoltenfishPatrolState customPatrolState;
        private MoltenfishChaseState customChaseState;

        protected override void Awake()
        {
            base.Awake();
            jumpState = new MoltenfishJumpState(this);
            customPatrolState = new MoltenfishPatrolState(this);
            customChaseState = new MoltenfishChaseState(this);
            VFXController = GetComponent<MoltenfishVFXController>();
            
            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Swimming initially
            }
        }

        protected override void Start()
        {
            ApplyFacing(FacingDirection);
            StateMachine.SetState(customPatrolState);
        }

        public bool CheckForJumpPoints()
        {
            if (jumpPoints == null || jumpPoints.Length == 0) return false;

            // Clear the last jump point if we have moved far enough away from it
            if (lastJumpPoint != null)
            {
                if (Mathf.Abs(transform.position.x - lastJumpPoint.position.x) > jumpPointThreshold * 2f)
                {
                    lastJumpPoint = null;
                }
            }

            if (Time.time - LastJumpTime < jumpCooldown) return false;

            foreach (var point in jumpPoints)
            {
                if (point == null) continue;
                if (Mathf.Abs(transform.position.x - point.position.x) <= jumpPointThreshold)
                {
                    // Don't jump on the exact same point twice in a row if we haven't moved away
                    if (point == lastJumpPoint) continue;

                    lastJumpPoint = point;
                    // Snap X position to the jump point so it jumps straight up from the center
                    transform.position = new Vector3(point.position.x, transform.position.y, transform.position.z);
                    return true;
                }
            }
            return false;
        }

        public override void SetStateToPatrol()
        {
            StateMachine.SetState(customPatrolState);
        }

        public override void SetStateToChase()
        {
            StateMachine.SetState(customChaseState);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(jumpState);
        }

        public override void SetLocomotionAnimation(bool isWalking)
        {
            if (Animator != null)
            {
                Animator.SetBool("isWalk", isWalking);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead) return;
            if (StateMachine.CurrentState == jumpState && collision.gameObject.CompareTag("Player"))
            {
                PlayerHitResolver.TryApplyHitToPlayer(
                    collision.collider,
                    biteDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(this),
                    transform,
                    collision.transform.position - transform.position);
            }
        }
        
        private void OnTriggerEnter2D(Collider2D collider)
        {
            if (IsDead) return;
            if (StateMachine.CurrentState == jumpState && collider.CompareTag("Player"))
            {
                PlayerHitResolver.TryApplyHitToPlayer(
                    collider,
                    biteDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(this),
                    transform,
                    collider.transform.position - transform.position);
            }
        }
    }
}
