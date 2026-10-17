using UnityEngine;

namespace Bao.Enemies
{
    public class FungloomChaseState : BaoEnemyState
    {
        private readonly FungloomEnemy fungloom;

        public FungloomChaseState(BaoEnemyBase enemy) : base(enemy)
        {
            fungloom = enemy as FungloomEnemy;
        }

        public override void Enter()
        {
            fungloom.StopHorizontalMovement();
            fungloom.SetLocomotionAnimation(false);
            if (fungloom.Rigidbody2D != null)
            {
                fungloom.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }

        public override void LogicUpdate()
        {
            if (!fungloom.HasTargetInDetectionRange())
            {
                fungloom.SetStateToPatrol();
                return;
            }

            if (fungloom.Target != null)
            {
                fungloom.FacePosition(fungloom.Target.position);
            }

            if (fungloom.CanAttackNow())
            {
                fungloom.SetStateToAttack();
            }
        }

        public override void PhysicsUpdate()
        {
            if (fungloom.Rigidbody2D != null)
            {
                fungloom.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }
    }
}
