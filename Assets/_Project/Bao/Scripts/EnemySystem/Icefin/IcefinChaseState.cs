using UnityEngine;

namespace Bao.Enemies
{
    public class IcefinChaseState : BaoEnemyState
    {
        private readonly IcefinEnemy icefin;

        public IcefinChaseState(BaoEnemyBase enemy) : base(enemy)
        {
            icefin = enemy as IcefinEnemy;
        }

        public override void Enter()
        {
            icefin.SetFlyingAnimation(true);
            icefin.SetLocomotionAnimation(true);
            if (icefin.Rigidbody2D != null)
            {
                icefin.Rigidbody2D.gravityScale = 0f;
            }
        }

        public override void LogicUpdate()
        {
            if (!icefin.HasTargetInDetectionRange())
            {
                icefin.SetStateToPatrol();
                return;
            }

            bool inAttackRange = icefin.HasTargetInAttackRange();
            bool canAttack = icefin.CanAttackNow();

            if (inAttackRange && canAttack)
            {
                icefin.SetStateToAttack();
            }
        }

        public override void PhysicsUpdate()
        {
            if (icefin.Target == null)
            {
                icefin.StopHorizontalMovement();
                icefin.Rigidbody2D.linearVelocity = Vector2.zero;
                icefin.SetLocomotionAnimation(false);
                return;
            }

            icefin.FacePosition(icefin.Target.position);

            // Fly directly towards the target's position in 2D space
            Vector2 direction = ((Vector2)icefin.Target.position - (Vector2)icefin.transform.position).normalized;
            
            // Adjust velocity to smoothly track player
            icefin.Rigidbody2D.linearVelocity = direction * icefin.MoveSpeed;
            icefin.SetLocomotionAnimation(true);
        }

        public override void Exit()
        {
            icefin.Rigidbody2D.linearVelocity = Vector2.zero;
        }
    }
}
