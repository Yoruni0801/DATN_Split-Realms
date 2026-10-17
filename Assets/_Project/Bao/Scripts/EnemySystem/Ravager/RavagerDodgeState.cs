using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerDodgeState : BaoEnemyState
    {
        private RavagerEnemy ravager;
        private float dodgeTimer;
        private float dodgeDuration = 0.6f;
        private bool canDodgeBack;

        public RavagerDodgeState(BaoEnemyBase enemy) : base(enemy)
        {
            ravager = enemy as RavagerEnemy;
        }

        public override void Enter()
        {
            dodgeTimer = dodgeDuration;
            ravager.SetFlyingAnimation(false);
            ravager.SetLocomotionAnimation(true);

            // Raycast phía sau để kiểm tra có obstacle không
            Vector2 backDirection = new Vector2(-ravager.FacingDirection, 0);
            RaycastHit2D hit = Physics2D.Raycast(
                ravager.transform.position,
                backDirection,
                ravager.DodgeRaycastDistance,
                LayerMask.GetMask("Default")
            );

            canDodgeBack = hit.collider == null || hit.collider.isTrigger;
        }

        public override void LogicUpdate()
        {
            if (!ravager.HasTargetInDetectionRange())
            {
                ravager.SetStateToPatrol();
                return;
            }

            dodgeTimer -= Time.deltaTime;

            if (dodgeTimer <= 0f)
            {
                // Quay lại ground combat, không phải chase (tránh loop nhanh)
                ravager.SetStateToGroundCombat();
            }
        }

        public override void PhysicsUpdate()
        {
            if (!canDodgeBack)
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            float backDirection = -ravager.FacingDirection;
            Vector2 dodgeVelocity = new Vector2(
                backDirection * ravager.MoveSpeed * 1.5f,
                ravager.Rigidbody2D.linearVelocity.y
            );

            Vector2 dodgeOffset = new Vector2(backDirection * ravager.DodgeBackDistance * Time.fixedDeltaTime, 0f);
            Vector2 newPos = (Vector2)ravager.transform.position + dodgeOffset;
            newPos = ravager.ClampToBoundary(newPos);

            ravager.Rigidbody2D.linearVelocity = dodgeVelocity;
        }
    }
}
