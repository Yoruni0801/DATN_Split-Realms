using UnityEngine;

namespace Bao.Enemies
{
    public class WildrootEnemy : BaoEnemyBase
    {
        [Header("Wildroot - Trap")]
        [SerializeField] private int trapDamage = 15;
        [SerializeField] private float trapActiveTime = 1f;
        [SerializeField] private float trapCooldown = 2f;

        public int TrapDamage => trapDamage;
        public float TrapActiveTime => trapActiveTime;
        public float TrapCooldown => trapCooldown;

        private WildrootIdleState idleState;
        private WildrootVFXController vfxController;
        public WildrootCloseState CloseState { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            idleState = new WildrootIdleState(this);
            CloseState = new WildrootCloseState(this);
            vfxController = GetComponent<WildrootVFXController>();
        }

        protected override void Start()
        {
            StateMachine.SetState(idleState);
        }

        public override void SetStateToAttack() { }
        public override void SetStateToChase() { }

        public void SetStateToIdle()
        {
            StateMachine.SetState(idleState);
        }

        public void BeginWhipChargeVFX()
        {
            vfxController?.BeginWhipCharge();
        }

        public void PlayWhipThrustVFX()
        {
            vfxController?.PlayWhipThrust();
        }

        private Collider2D playerInBounds;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (IsDead) return;

            if (collision.CompareTag("Player"))
            {
                playerInBounds = collision;
                if (StateMachine.CurrentState == idleState)
                {
                    StateMachine.SetState(CloseState);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player") && playerInBounds == collision)
            {
                playerInBounds = null;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead) return;

            if (collision.gameObject.CompareTag("Player"))
            {
                playerInBounds = collision.collider;
                if (StateMachine.CurrentState == idleState)
                {
                    StateMachine.SetState(CloseState);
                }
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player") && playerInBounds == collision.collider)
            {
                playerInBounds = null;
            }
        }

        public void EnableCollisionTag()
        {
            if (IsDead) return;

            vfxController?.PlayWhipThrust();

            if (playerInBounds == null) return;

            IncomingAttackResolution hitResult = PlayerHitResolver.TryApplyHitToPlayer(
                playerInBounds,
                trapDamage,
                ParryableAttackType.Melee,
                EnemyParryResponder.Ensure(this),
                transform,
                playerInBounds.transform.position - transform.position);

            if (hitResult != IncomingAttackResolution.Ignored)
            {
                vfxController?.PlayWhipImpact();
            }
        }

        public void DisableCollisionTag()
        {
        }
    }
}