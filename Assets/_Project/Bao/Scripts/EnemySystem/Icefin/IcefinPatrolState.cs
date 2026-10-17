using UnityEngine;

namespace Bao.Enemies
{
    public class IcefinPatrolState : BaoEnemyState
    {
        private readonly IcefinEnemy icefin;
        private bool hasFlipped = false;
        private bool isReturningToHome = false;

        public IcefinPatrolState(BaoEnemyBase enemy) : base(enemy)
        {
            icefin = enemy as IcefinEnemy;
        }

        public override void Enter()
        {
            icefin.PauseTurning();
            hasFlipped = false;
            icefin.SetFlyingAnimation(true);
            icefin.SetLocomotionAnimation(false);

            if (icefin.Rigidbody2D != null)
            {
                icefin.Rigidbody2D.gravityScale = 0f;
            }

            float distanceFromHomeX = Mathf.Abs(icefin.transform.position.x - icefin.HomePosition.x);
            isReturningToHome = distanceFromHomeX > icefin.PatrolDistance;
        }

        public override void LogicUpdate()
        {
            icefin.TickTurnPause();

            if (icefin.HasTargetInDetectionRange())
            {
                icefin.SetStateToChase();
                return;
            }

            if (isReturningToHome)
            {
                float distanceFromHomeX = Mathf.Abs(icefin.transform.position.x - icefin.HomePosition.x);
                if (distanceFromHomeX <= icefin.PatrolDistance * 0.5f)
                {
                    isReturningToHome = false;
                }
            }
        }

        public override void PhysicsUpdate()
        {
            // Smoothly bring Y coordinate back to home Y position
            float targetY = icefin.HomePosition.y;
            float currentY = icefin.transform.position.y;
            float newYVelocity = Mathf.MoveTowards(0f, (targetY - currentY) * 2f, icefin.MoveSpeed);

            if (icefin.IsTurnPauseActive())
            {
                icefin.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                icefin.SetLocomotionAnimation(false);
                return;
            }

            if (isReturningToHome)
            {
                float dirToHome = icefin.HomePosition.x > icefin.transform.position.x ? 1f : -1f;
                icefin.SetLocomotionAnimation(true);
                icefin.FacePosition(new Vector2(icefin.transform.position.x + dirToHome, icefin.transform.position.y));
                icefin.Rigidbody2D.linearVelocity = new Vector2(dirToHome * icefin.MoveSpeed, newYVelocity);
                return;
            }

            if (icefin.ShouldTurnAroundFlying())
            {
                if (!hasFlipped)
                {
                    icefin.FlipDirection();
                    icefin.PauseTurning();
                    icefin.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                    icefin.SetLocomotionAnimation(false);
                    hasFlipped = true;
                    return;
                }

                icefin.Rigidbody2D.linearVelocity = new Vector2(0f, newYVelocity);
                icefin.SetLocomotionAnimation(false);
                return;
            }

            hasFlipped = false;
            icefin.SetLocomotionAnimation(true);
            icefin.Rigidbody2D.linearVelocity = new Vector2(icefin.PatrolDirection * icefin.MoveSpeed, newYVelocity);
        }
    }
}
