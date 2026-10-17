using UnityEngine;

namespace Bao.Enemies
{
    public class IcefinEnemy : BaoEnemyBase
    {
        [Header("Icefin - Attack")]
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float anticipationDuration = 0.4f; // Thời gian gồng (hiện warning)
        [SerializeField] private float dashSpeed = 16f; // Tốc độ phóng
        [SerializeField] private float dashDuration = 1.0f; // Thời gian tối đa của cú phóng nếu không chạm gì

        public int AttackDamage => attackDamage;
        public float AnticipationDuration => anticipationDuration;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;

        // Đảo ngược Sprite khi di chuyển để khớp với hướng mặc định của sprite
        protected override float FacingVisualMultiplier => -1f;

        private IcefinAttackState attackState;
        private IcefinStunState stunState;
        private bool hasDamagedPlayerThisDash;
        private BaoEnemyFlyingPatrolState customPatrolState;
        private BaoEnemyFlyingChaseState customChaseState;

        public IcefinVFXController VfxController { get; private set; }
        public Vector2 CurrentDashDirection { get; private set; } = Vector2.right;

        protected override void Awake()
        {
            base.Awake();
            attackState = new IcefinAttackState(this);
            stunState = new IcefinStunState(this, 1.2f);
            customPatrolState = new BaoEnemyFlyingPatrolState(this);
            customChaseState = new BaoEnemyFlyingChaseState(this);
            VfxController = GetComponent<IcefinVFXController>();

            if (Rigidbody2D != null)
            {
                Rigidbody2D.gravityScale = 0f; // Icefin flies
                Rigidbody2D.simulated = true;  // Prevent Inspector mistakes from freezing Icefin
            }
        }

        protected override void Start()
        {
            StateMachine.SetState(customPatrolState);
        }

        protected override void OnDead()
        {
            base.OnDead();
            VfxController?.TriggerDeathBurst();
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
            StateMachine.SetState(attackState);
        }

        public void SetStateToStun()
        {
            if (stunState == null)
            {
                stunState = new IcefinStunState(this, 1.2f);
            }
            StateMachine.SetState(stunState);
        }

        public override void PlayAttackAnimation()
        {
            if (Animator == null)
                return;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
            {
                if (parameter.name == "isAttack" && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    Animator.SetBool("isAttack", true);
                    return;
                }

                if (parameter.name == "Dash" && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    Animator.SetTrigger("Dash");
                    return;
                }
            }
        }

        public override void SetLocomotionAnimation(bool isWalking)
        {
            if (Animator == null)
                return;

            Animator.SetBool("isFly", isWalking);
            Animator.SetBool("isIdle", !isWalking);
        }

        public void SetDashDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
                CurrentDashDirection = direction.normalized;
        }

        public void BeginDashDamageWindow()
        {
            hasDamagedPlayerThisDash = false;
        }

        public bool TryDamagePlayer(Collider2D playerCollider, Vector2 incomingDirection)
        {
            if (IsDead || hasDamagedPlayerThisDash || attackState == null || !attackState.IsDashing || playerCollider == null)
            {
                return false;
            }

            if (playerCollider.GetComponentInParent<PlayerHealth>() == null)
            {
                return false;
            }

            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                playerCollider,
                attackDamage,
                ParryableAttackType.Melee,
                EnemyParryResponder.Ensure(this),
                transform,
                incomingDirection);

            if (resolution == IncomingAttackResolution.Ignored)
                return false;

            if (resolution == IncomingAttackResolution.Parried)
            {
                hasDamagedPlayerThisDash = true;
                if (Rigidbody2D != null)
                {
                    Rigidbody2D.linearVelocity = Vector2.zero;
                }
                SetStateToStun();
                return false;
            }

            hasDamagedPlayerThisDash = true;
            VfxController?.TriggerDashImpact(playerCollider.bounds.ClosestPoint(transform.position), incomingDirection, true);
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryDamagePlayer(collision.collider, collision.transform.position - transform.position);
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            TryDamagePlayer(collider, collider.transform.position - transform.position);
        }

        // Animation Event Receivers
        public void EnableAttack()
        {
            // Do nothing, handled by script overlap
        }

        public void DisableAttack()
        {
            // Do nothing
        }

        public void AnimationTrigger()
        {
            // Do nothing
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (Animator != null)
            {
                // Nếu đang Stun, giữ nguyên trạng thái idle
                if (StateMachine != null && StateMachine.CurrentState == stunState)
                {
                    Animator.SetBool("isAttack", false);
                    Animator.SetBool("isFly", false);
                    Animator.SetBool("isIdle", true);
                    return;
                }

                // Nếu đang tấn công lao xoay (isAttack = true) thì tắt Fly và Idle
                bool isSpinning = StateMachine != null && StateMachine.CurrentState == attackState && Animator.GetBool("isAttack");
                if (isSpinning)
                {
                    Animator.SetBool("isFly", false);
                    Animator.SetBool("isIdle", false);
                }
            }
        }

        private void OnDisable()
        {
            if (!gameObject.activeInHierarchy)
                return;

            // Được gọi khi quái bị Parry (EnemyParryResponder sẽ disable script này)
            if (!IsDead && StateMachine != null && StateMachine.CurrentState == attackState)
            {
                StopWarningRoutine();
                if (Animator != null)
                {
                    Animator.SetBool("isAttack", false);
                    Animator.SetBool("isFly", false);
                    Animator.SetBool("isIdle", true);
                }
                if (Rigidbody2D != null)
                {
                    Rigidbody2D.linearVelocity = Vector2.zero;
                }

                VfxController?.TriggerParryBreak();
                SetStunEffectActive(true); // Bật hiệu ứng choáng
                SetStateToStun(); // Chuyển sang StunState thay vì Chase!
            }
        }

        private void OnEnable()
        {
            // Chỉ tắt stun effect nếu không phải đang trong stun state
            if (StateMachine == null || StateMachine.CurrentState != stunState)
            {
                SetStunEffectActive(false);
            }
        }
    }
}
