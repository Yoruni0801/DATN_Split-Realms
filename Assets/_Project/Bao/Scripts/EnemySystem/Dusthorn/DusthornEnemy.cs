using UnityEngine;
using UnityEngine.Events;

namespace Bao.Enemies
{
    public class DusthornEnemy : BaoEnemyBase
    {
        [Header("Dusthorn - Roll")]
        [SerializeField] private float rollSpeed = 6f;
        [SerializeField] private float rollDuration = 1.5f;
        [SerializeField] private int rollDamage = 10;
        [SerializeField] private float rollAnticipationDuration = 0.4f;

        [Header("Dusthorn - Windup Chase")]
        [SerializeField] private float rollWindupSpeed = 3f;

        [Header("Dusthorn - Events")]
        [SerializeField] private UnityEvent dcd;

        public float RollSpeed => rollSpeed;
        public float RollDuration => rollDuration;
        public int RollDamage => rollDamage;
        public float RollAnticipationDuration => rollAnticipationDuration;
        public float RollWindupSpeed => rollWindupSpeed;

        // Đảo ngược Sprite khi di chuyển
        protected override float FacingVisualMultiplier => -1f;

        public DusthornVFXController VfxController { get; private set; }

        private DusthornRollState rollState;

        private bool isDamageActive = false;
        private float lastHitTime = -999f;
        private float hitCooldown = 1f;

        protected override void Awake()
        {
            base.Awake();
            rollState = new DusthornRollState(this);
            VfxController = GetComponent<DusthornVFXController>();
        }

        protected override void OnDead()
        {
            base.OnDead();
            VfxController?.TriggerDeathCollapse();
            dcd?.Invoke();
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(rollState);
        }

        public bool TryDealRollDamage(Collider2D playerCollider)
        {
            if (IsDead || !isDamageActive) return false;
            if (Time.time < lastHitTime + hitCooldown) return false;
            if (StateMachine.CurrentState != rollState) return false;
            if (playerCollider == null || playerCollider.GetComponentInParent<PlayerHealth>() == null) return false;

            lastHitTime = Time.time;
            Vector3 hitPoint = playerCollider.ClosestPoint(transform.position);

            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                playerCollider,
                rollDamage,
                ParryableAttackType.Melee,
                EnemyParryResponder.Ensure(this),
                transform,
                playerCollider.transform.position - transform.position,
                true,
                true);

            if (resolution == IncomingAttackResolution.Damaged)
                VfxController?.TriggerPlayerImpact(hitPoint);

            return resolution != IncomingAttackResolution.Ignored;
        }

        public void EnableCollisionTag()
        {
            isDamageActive = true;
        }

        public void DisableCollisionTag()
        {
            isDamageActive = false;
        }

        // Khi bị Parry, hệ thống sẽ tự động tắt (disable) script này đi để làm choáng (stun).
        private void OnDisable()
        {
            if (StateMachine != null && StateMachine.CurrentState == rollState)
            {
                DisableCollisionTag();
                StopWarningRoutine();
                VfxController?.TriggerParryBreak();

                if (Animator != null)
                {
                    Animator.SetBool("isRoll", false);
                    Animator.SetTrigger("Hit");
                }

                SetStateToChase();
            }
        }
    }
}
