using UnityEngine;

namespace Bao.Enemies
{
    public class PhantomStunState : BaoEnemyState
    {
        private PhantomEnemy phantom;
        private float stunStartTime;

        public PhantomStunState(PhantomEnemy enemy) : base(enemy)
        {
            phantom = enemy;
        }

        public override void Enter()
        {
            stunStartTime = Time.time;
            phantom.StopHorizontalMovement();
            phantom.SetLocomotionAnimation(false);
            phantom.SetFlyingAnimation(false);
            
            phantom.SetStunEffectActive(true);
            
            if (phantom.Animator != null)
            {
                // Try to play a hit/stun animation if available
                phantom.Animator.SetTrigger("Hit"); 
            }
        }

        public override void LogicUpdate()
        {
            if (Time.time >= stunStartTime + phantom.StunDuration)
            {
                if (phantom.HasTargetInDetectionRange())
                {
                    phantom.SetStateToChase();
                }
                else
                {
                    phantom.SetStateToPatrol();
                }
            }
        }

        public override void Exit()
        {
            phantom.SetStunEffectActive(false);
        }
    }
}
