using UnityEngine;

namespace Bao.Enemies
{
    public sealed class ChubblooPatrolState : BaoEnemyState
    {
        private readonly ChubblooEnemy chubbloo;
        private bool hasFlipped;
        private bool isReturningToHome;

        public ChubblooPatrolState(ChubblooEnemy enemy) : base(enemy)
        {
            chubbloo = enemy;
        }

        public override void Enter()
        {
            chubbloo.PauseTurning();
            chubbloo.SetFlyingAnimation(false);
            chubbloo.SetLocomotionAnimation(false);
            hasFlipped = false;
            isReturningToHome = Mathf.Abs(chubbloo.transform.position.x - chubbloo.HomePosition.x) > chubbloo.PatrolDistance;
        }

        public override void LogicUpdate()
        {
            chubbloo.TickTurnPause();

            if (chubbloo.HasTargetInDetectionRange())
            {
                chubbloo.SetStateToChase();
                return;
            }

            if (isReturningToHome
                && Mathf.Abs(chubbloo.transform.position.x - chubbloo.HomePosition.x) <= chubbloo.PatrolDistance * 0.5f)
            {
                isReturningToHome = false;
            }
        }

        public override void PhysicsUpdate()
        {
            if (chubbloo.IsHopping)
            {
                chubbloo.SetLocomotionAnimation(true);
                return;
            }

            if (chubbloo.IsTurnPauseActive())
            {
                StopMoving();
                return;
            }

            float direction = isReturningToHome
                ? Mathf.Sign(chubbloo.HomePosition.x - chubbloo.transform.position.x)
                : chubbloo.PatrolDirection;

            bool shouldTurn = !isReturningToHome && chubbloo.ShouldTurnAround();
            bool unsafeHop = !chubbloo.CanHopInDirection(direction);
            if (shouldTurn || unsafeHop)
            {
                if (!hasFlipped && !isReturningToHome)
                {
                    chubbloo.FlipDirection();
                    chubbloo.PauseTurning();
                    hasFlipped = true;
                }

                StopMoving();
                return;
            }

            hasFlipped = false;
            chubbloo.SetLocomotionAnimation(true);
            chubbloo.MoveForward(direction);
        }

        private void StopMoving()
        {
            chubbloo.StopHorizontalMovement();
            chubbloo.SetLocomotionAnimation(false);
        }
    }
}
