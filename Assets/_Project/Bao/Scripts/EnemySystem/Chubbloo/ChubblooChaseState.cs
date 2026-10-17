using UnityEngine;

namespace Bao.Enemies
{
    public sealed class ChubblooChaseState : BaoEnemyState
    {
        private readonly ChubblooEnemy chubbloo;

        public ChubblooChaseState(ChubblooEnemy enemy) : base(enemy)
        {
            chubbloo = enemy;
        }

        public override void Enter()
        {
            chubbloo.SetFlyingAnimation(false);
            chubbloo.SetLocomotionAnimation(!chubbloo.HasTargetInAttackRange());
        }

        public override void LogicUpdate()
        {
            if (!chubbloo.HasTargetInDetectionRange())
            {
                chubbloo.SetStateToPatrol();
                return;
            }

            if (chubbloo.HasTargetInAttackRange())
            {
                if (chubbloo.Target != null)
                {
                    chubbloo.FacePosition(chubbloo.Target.position);
                }

                if (chubbloo.CanAttackNow() && (chubbloo.CanBeginAttack || !chubbloo.IsHopping))
                {
                    chubbloo.SetStateToAttack();
                }
            }
        }

        public override void PhysicsUpdate()
        {
            if (chubbloo.Target == null)
            {
                StopMoving();
                return;
            }

            if (chubbloo.IsHopping)
            {
                return;
            }

            if (chubbloo.HasTargetInAttackRange())
            {
                StopMoving();
                chubbloo.FacePosition(chubbloo.Target.position);
                return;
            }

            float direction = Mathf.Sign(chubbloo.Target.position.x - chubbloo.transform.position.x);
            if (!chubbloo.CanHopInDirection(direction))
            {
                StopMoving();
                return;
            }

            chubbloo.MoveForward(direction);
        }

        private void StopMoving()
        {
            chubbloo.StopHorizontalMovement();
            chubbloo.SetLocomotionAnimation(false);
        }
    }
}
