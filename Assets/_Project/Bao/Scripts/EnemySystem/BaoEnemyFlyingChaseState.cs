using UnityEngine;

namespace Bao.Enemies
{
    public class BaoEnemyFlyingChaseState : BaoEnemyState
    {
        public BaoEnemyFlyingChaseState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            Enemy.SetFlyingAnimation(true);
            if (Enemy.HasTargetInAttackRange())
            {
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
            }
            else
            {
                Enemy.SetLocomotionAnimation(true);
            }
            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.gravityScale = 0f;
            }
        }

        public override void LogicUpdate()
        {
            if (!Enemy.HasTargetInDetectionRange())
            {
                Enemy.SetStateToPatrol();
                return;
            }

            bool inAttackRange = Enemy.HasTargetInAttackRange();
            bool canAttack = Enemy.CanAttackNow();

            if (inAttackRange && canAttack)
            {
                Enemy.SetStateToAttack();
            }
        }

        public override void PhysicsUpdate()
        {
            if (Enemy.Target == null)
            {
                Enemy.StopHorizontalMovement();
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            Enemy.FacePosition(Enemy.Target.position);

            if (Enemy.HasTargetInAttackRange())
            {
                Enemy.StopHorizontalMovement();
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            // Fly directly towards the target's position in 2D space
            Vector2 direction = ((Vector2)Enemy.Target.position - (Vector2)Enemy.transform.position).normalized;
            
            // Adjust velocity to smoothly track player
            Enemy.Rigidbody2D.linearVelocity = direction * Enemy.MoveSpeed;
            Enemy.SetLocomotionAnimation(true);
        }

        public override void Exit()
        {
            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }
    }
}
