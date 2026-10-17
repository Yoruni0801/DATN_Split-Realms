using UnityEngine;

namespace Bao.Enemies
{
    public class BaoEnemyChaseState : BaoEnemyState
    {
        public BaoEnemyChaseState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            Enemy.SetFlyingAnimation(false);
            if (Enemy.HasTargetInAttackRange())
            {
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
            }
            else
            {
                Enemy.SetLocomotionAnimation(true);
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
                Debug.Log($"{Enemy.name} attacking! In range: {inAttackRange}, Can attack: {canAttack}", Enemy);
                Enemy.SetStateToAttack();
            }
        }

        public override void PhysicsUpdate()
        {
            if (Enemy.Target == null)
            {
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            if (Enemy.HasTargetInAttackRange())
            {
                Enemy.StopHorizontalMovement();
                Enemy.SetLocomotionAnimation(false);
                return;
            }

            float direction = Enemy.Target.position.x >= Enemy.transform.position.x ? 1f : -1f;
            Enemy.SetLocomotionAnimation(true);
            Enemy.MoveForward(direction);
        }
    }
}