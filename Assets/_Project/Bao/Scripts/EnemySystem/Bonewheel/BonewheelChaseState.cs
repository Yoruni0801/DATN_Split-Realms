using UnityEngine;

namespace Bao.Enemies
{
    public class BonewheelChaseState : BaoEnemyState
    {
        private BonewheelEnemy bonewheel;

        public BonewheelChaseState(BonewheelEnemy enemy) : base(enemy)
        {
            bonewheel = enemy;
        }

        public override void Enter()
        {
            bonewheel.SetFlyingAnimation(false);
            bonewheel.SetLocomotionAnimation(true);
        }

        public override void LogicUpdate()
        {
            if (!bonewheel.HasTargetInDetectionRange())
            {
                bonewheel.SetStateToPatrol();
                return;
            }

            bool inAttackRange = bonewheel.HasTargetInAttackRange();
            bool inTeleportRange = bonewheel.HasTargetInTeleportRange();
            bool canAttack = bonewheel.CanAttackNow();
            bool canTeleport = bonewheel.CanTeleportNow();
            
            if (inAttackRange && canAttack)
            {
                bonewheel.SetStateToAttack();
            }
            else if (inTeleportRange && !inAttackRange && canTeleport)
            {
                bonewheel.SetStateToTeleport();
            }
        }

        public override void PhysicsUpdate()
        {
            if (bonewheel.Target == null)
            {
                bonewheel.StopHorizontalMovement();
                bonewheel.SetLocomotionAnimation(false);
                return;
            }

            if (bonewheel.HasTargetInAttackRange())
            {
                bonewheel.StopHorizontalMovement();
                bonewheel.SetLocomotionAnimation(false);
                return;
            }

            float direction = bonewheel.Target.position.x >= bonewheel.transform.position.x ? 1f : -1f;
            bonewheel.SetLocomotionAnimation(true);
            bonewheel.MoveForward(direction);
        }
    }
}
