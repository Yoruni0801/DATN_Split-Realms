using UnityEngine;

namespace Bao.Enemies
{
    public class MoltenfishChaseState : BaoEnemyState
    {
        private readonly MoltenfishEnemy moltenfish;

        public MoltenfishChaseState(BaoEnemyBase enemy) : base(enemy)
        {
            moltenfish = enemy as MoltenfishEnemy;
        }

        public override void Enter()
        {
            moltenfish.SetLocomotionAnimation(true);
            if (moltenfish.Rigidbody2D != null)
            {
                moltenfish.Rigidbody2D.gravityScale = 0f; // Swim
            }
        }

        public override void LogicUpdate()
        {
            if (moltenfish.CheckForJumpPoints())
            {
                moltenfish.SetStateToAttack();
                return;
            }

            if (!moltenfish.HasTargetInDetectionRange())
            {
                moltenfish.SetStateToPatrol();
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            if (moltenfish.Target == null)
            {
                moltenfish.StopHorizontalMovement();
                moltenfish.Rigidbody2D.linearVelocity = Vector2.zero;
                moltenfish.SetLocomotionAnimation(false);
                return;
            }

            moltenfish.FacePosition(moltenfish.Target.position);

            // Swim towards the target but keep Y bound within the lake limits to prevent the fish from swimming into the air
            Vector2 targetPos = moltenfish.Target.position;
            float maxY = moltenfish.HomePosition.y + moltenfish.SwimHeightLimit;
            float minY = moltenfish.HomePosition.y - moltenfish.SwimDepth;
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

            Vector2 direction = (targetPos - (Vector2)moltenfish.transform.position).normalized;
            moltenfish.Rigidbody2D.linearVelocity = direction * moltenfish.SwimSpeed;
            moltenfish.SetLocomotionAnimation(true);
        }

        public override void Exit()
        {
            if (moltenfish.Rigidbody2D != null)
            {
                moltenfish.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }
    }
}
