using UnityEngine;

namespace Bao.Enemies
{
    public class IcecrawlerEnemy : BaoEnemyBase
    {
        [Header("Icecrawler - Charge")]
        [SerializeField] private float chargeAnticipationDuration = 0.5f;
        [SerializeField] private float chargeSpeed = 8f;
        [SerializeField] private float chargeDuration = 1.2f;
        [SerializeField] private int chargeDamage = 15;
        
        public float ChargeAnticipationDuration => chargeAnticipationDuration;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeDuration => chargeDuration;
        public int ChargeDamage => chargeDamage;

        // Đảo ngược Sprite khi di chuyển
        protected override float FacingVisualMultiplier => -1f;

        private IcecrawlerChargeState chargeState;

        protected override void Awake()
        {
            base.Awake();
            chargeState = new IcecrawlerChargeState(this);
        }

        public override void SetStateToAttack()
        {
            StateMachine.SetState(chargeState);
        }
        
        // Damage is handled via Overlap in IcecrawlerChargeState to ensure it only hits once per charge.

        private void OnDisable()
        {
            if (StateMachine != null && StateMachine.CurrentState == chargeState)
            {
                StopWarningRoutine();
                if (Animator != null)
                {
                    Animator.SetTrigger("Hit");
                }
                if (Rigidbody2D != null)
                {
                    Rigidbody2D.linearVelocity = Vector2.zero;
                }
                SetStateToChase();
            }
        }
    }
}
