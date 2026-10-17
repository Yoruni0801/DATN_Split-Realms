using UnityEngine;

namespace Bao.Enemies
{
    public class BaoEnemyFlyingPatrolState : BaoEnemyState
    {
        private bool hasFlipped = false;
        private bool isReturningToHome = false;

        public BaoEnemyFlyingPatrolState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            Enemy.PauseTurning();
            hasFlipped = false;
            Enemy.SetFlyingAnimation(true);
            Enemy.SetLocomotionAnimation(false);

            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.gravityScale = 0f;
            }

            float distanceFromHomeX = Mathf.Abs(Enemy.transform.position.x - Enemy.HomePosition.x);
            isReturningToHome = distanceFromHomeX > Enemy.PatrolDistance;
        }

        public override void LogicUpdate()
        {
            Enemy.TickTurnPause();

            if (Enemy.HasTargetInDetectionRange())
            {
                Enemy.SetStateToChase();
                return;
            }

            if (isReturningToHome)
            {
                float distanceFromHomeX = Mathf.Abs(Enemy.transform.position.x - Enemy.HomePosition.x);
                if (distanceFromHomeX <= Enemy.PatrolDistance * 0.5f)
                {
                    isReturningToHome = false;
                }
            }
        }

        public override void PhysicsUpdate()
        {
            // Smoothly bring Y coordinate back to home Y position
            float targetY = Enemy.HomePosition.y;
            float currentY = Enemy.transform.position.y;
            float newYVelocity = Mathf.Clamp((targetY - currentY) * 2f, -Enemy.MoveSpeed, Enemy.MoveSpeed);

            if (Enemy.IsTurnPauseActive())
            {
                Enemy.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            if (isReturningToHome)
            {
                float dirToHome = Enemy.HomePosition.x > Enemy.transform.position.x ? 1f : -1f;
                Enemy.SetLocomotionAnimation(true);
                // Set the flying altitude Y velocity first, then let MoveForward set X velocity and handle sprite facing
                Enemy.Rigidbody2D.linearVelocity = new Vector2(Enemy.Rigidbody2D.linearVelocity.x, newYVelocity);
                Enemy.MoveForward(dirToHome);
                return;
            }

            if (Enemy.ShouldTurnAroundFlying())
            {
                if (!hasFlipped)
                {
                    Enemy.FlipDirection();
                    Enemy.PauseTurning();
                    Enemy.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                    Enemy.SetLocomotionAnimation(false);
                    hasFlipped = true;
                    return;
                }

                Enemy.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            hasFlipped = false;
            Enemy.SetLocomotionAnimation(true);
            
            // Set the flying altitude Y velocity first, then let MoveForward set X velocity and handle sprite facing
            Enemy.Rigidbody2D.linearVelocity = new Vector2(Enemy.Rigidbody2D.linearVelocity.x, newYVelocity);
            Enemy.MoveForward(Enemy.PatrolDirection);
        }
    }
}
