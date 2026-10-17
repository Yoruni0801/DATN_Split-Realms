using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerChaseState : BaoEnemyState
    {
        private RavagerEnemy ravager;

        public RavagerChaseState(BaoEnemyBase enemy) : base(enemy)
        {
            ravager = enemy as RavagerEnemy;
        }

        public override void Enter()
        {
            ravager.SetFlyingAnimation(false);
            ravager.SetLocomotionAnimation(true);
        }

        public override void LogicUpdate()
        {
            if (!ravager.HasTargetInDetectionRange())
            {
                ravager.SetStateToPatrol();
                return;
            }

            if (ravager.HasTargetInAttackRange())
            {
                ravager.SetStateToGroundCombat();
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            if (ravager.Target == null)
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            if (ravager.HasTargetInAttackRange())
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            float direction = ravager.Target.position.x >= ravager.transform.position.x ? 1f : -1f;
            float chaseSpeed = ravager.MoveSpeed * ravager.ChaseSpeedMultiplier;

            ravager.SetLocomotionAnimation(true);
            ravager.MoveForward(direction);
            ravager.Rigidbody2D.linearVelocity = new Vector2(
                direction * chaseSpeed,
                ravager.Rigidbody2D.linearVelocity.y
            );
        }
    }
}
