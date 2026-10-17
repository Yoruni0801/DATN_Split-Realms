using UnityEngine;

namespace Bao.Enemies
{
    public class BaoEnemyAttackState : BaoEnemyState
    {
        private float enterTime;
        private bool attackPlayed;

        public BaoEnemyAttackState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            enterTime = Time.time;
            attackPlayed = false;
            Enemy.StopHorizontalMovement();
            Enemy.SetFlyingAnimation(false);
            Enemy.SetLocomotionAnimation(false);
            Enemy.MarkAttackStarted();
            Enemy.StartWarningRoutine();
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
        }

        public override void LogicUpdate()
        {
            if (!attackPlayed)
            {
                Enemy.PlayAttackAnimation();
                attackPlayed = true;
            }

            if (!Enemy.HasTargetInDetectionRange())
            {
                if (Time.time >= enterTime + Enemy.AttackStateDuration)
                {
                    Enemy.SetStateToPatrol();
                }

                return;
            }

            if (Time.time >= enterTime + Enemy.AttackStateDuration)
            {
                Enemy.SetStateToChase();
            }
        }
    }
}