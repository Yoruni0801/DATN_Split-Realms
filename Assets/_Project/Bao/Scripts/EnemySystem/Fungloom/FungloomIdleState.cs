using UnityEngine;

namespace Bao.Enemies
{
    public class FungloomIdleState : BaoEnemyState
    {
        private readonly FungloomEnemy fungloom;

        public FungloomIdleState(BaoEnemyBase enemy) : base(enemy)
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
            if (fungloom.HasTargetInDetectionRange())
            {
                fungloom.SetStateToChase();
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
