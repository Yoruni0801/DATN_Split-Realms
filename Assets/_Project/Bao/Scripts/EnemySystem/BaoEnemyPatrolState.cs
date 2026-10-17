using UnityEngine;

namespace Bao.Enemies
{
    public class BaoEnemyPatrolState : BaoEnemyState
    {
        private bool hasFlipped = false;
        private bool isReturningToHome = false;

        public BaoEnemyPatrolState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            Enemy.PauseTurning();
            hasFlipped = false;
            Enemy.SetFlyingAnimation(false);
            Enemy.SetLocomotionAnimation(false);

            // Check if we're too far from home and need to return (X axis only to prevent falling/altitude issues)
            float distanceFromHome = Mathf.Abs(Enemy.transform.position.x - Enemy.HomePosition.x);
            isReturningToHome = distanceFromHome > Enemy.PatrolDistance;
        }

        public override void LogicUpdate()
        {
            Enemy.TickTurnPause();

            if (Enemy.HasTargetInDetectionRange())
            {
                Enemy.SetStateToChase();
                return;
            }

            // Check if we've returned close enough to home
            if (isReturningToHome)
            {
                float distanceFromHome = Mathf.Abs(Enemy.transform.position.x - Enemy.HomePosition.x);
                if (distanceFromHome <= Enemy.PatrolDistance * 0.5f)
                {
                    isReturningToHome = false;
                }
            }
        }

        public override void PhysicsUpdate()
        {
            if (Enemy.IsTurnPauseActive())
            {
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            // If returning to home, move toward home position
            if (isReturningToHome)
            {
                float dirToHome = Enemy.HomePosition.x > Enemy.transform.position.x ? 1f : -1f;
                Enemy.SetLocomotionAnimation(true);
                Enemy.MoveForward(dirToHome);
                return;
            }

            if (Enemy.ShouldTurnAround())
            {
                // Only flip if we haven't already flipped in this patrol cycle
                if (!hasFlipped)
                {
                    Enemy.FlipDirection();
                    Enemy.PauseTurning();
                    Enemy.StopHorizontalMovement();
                    Enemy.SetLocomotionAnimation(false);
                    hasFlipped = true;
                    return;
                }

                // If still exceeding patrol distance after flip, just stay paused
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            // Reset flip flag when moving normally
            hasFlipped = false;
            Enemy.SetLocomotionAnimation(true);
            Enemy.MoveForward(Enemy.PatrolDirection);
        }
    }
}