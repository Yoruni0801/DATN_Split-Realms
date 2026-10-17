using UnityEngine;

namespace Bao.Enemies
{
    public class MoltenfishPatrolState : BaoEnemyState
    {
        private readonly MoltenfishEnemy moltenfish;
        private Vector2 targetPoint;

        public MoltenfishPatrolState(BaoEnemyBase enemy) : base(enemy)
        {
            moltenfish = enemy as MoltenfishEnemy;
        }

        public override void Enter()
        {
            moltenfish.SetLocomotionAnimation(true);
            
            if (moltenfish.Rigidbody2D != null)
            {
                moltenfish.Rigidbody2D.gravityScale = 0f; // Swim without gravity
            }

            PickRandomTargetPoint();
        }

        public override void LogicUpdate()
        {
            if (moltenfish.CheckForJumpPoints())
            {
                moltenfish.SetStateToAttack();
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            Vector2 currentPos = moltenfish.transform.position;
            float distance = Vector2.Distance(currentPos, targetPoint);

            if (distance <= 0.35f)
            {
                // Reached target point, immediately pick a new random target point to swim to
                PickRandomTargetPoint();
                return;
            }

            Vector2 direction = (targetPoint - currentPos).normalized;
            moltenfish.FacePosition(targetPoint);
            moltenfish.Rigidbody2D.linearVelocity = direction * moltenfish.SwimSpeed;
            moltenfish.SetLocomotionAnimation(true);
        }

        private void PickRandomTargetPoint()
        {
            float randomX = Random.Range(
                moltenfish.HomePosition.x - moltenfish.PatrolDistance,
                moltenfish.HomePosition.x + moltenfish.PatrolDistance
            );
            float randomY = Random.Range(
                moltenfish.HomePosition.y - moltenfish.SwimDepth,
                moltenfish.HomePosition.y + moltenfish.SwimHeightLimit
            );
            targetPoint = new Vector2(randomX, randomY);
        }
    }
}
