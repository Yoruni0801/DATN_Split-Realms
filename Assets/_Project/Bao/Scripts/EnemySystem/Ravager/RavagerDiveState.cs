using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerDiveState : BaoEnemyState
    {
        private RavagerEnemy ravager;
        private bool hasHit;
        private Vector3 diveTargetPosition;

        public RavagerDiveState(BaoEnemyBase enemy) : base(enemy)
        {
            ravager = enemy as RavagerEnemy;
        }

        public override void Enter()
        {
            hasHit = false;
            diveTargetPosition = ravager.Target != null ? ravager.Target.position : ravager.transform.position;
            ravager.SetFlyingAnimation(true);
            ravager.SetLocomotionAnimation(false);
        }

        public override void LogicUpdate()
        {
            if (!ravager.HasTargetInDetectionRange())
            {
                ravager.SetStateToPatrol();
                return;
            }
        }

        public override void PhysicsUpdate()
        {
            if (ravager.Target == null)
            {
                ravager.StopHorizontalMovement();
                return;
            }

            // Lao thẳng về vị trí player tại thời điểm bắt đầu dive
            Vector3 diveDir = (diveTargetPosition - ravager.transform.position).normalized;
            Vector2 diveVelocity = diveDir * ravager.DiveSpeed;

            if (ShouldLandOnGround(diveVelocity, out RaycastHit2D groundHit))
            {
                SnapToGround(groundHit);
                ravager.Rigidbody2D.linearVelocity = Vector2.zero;
                ravager.SetStateToGroundCombat();
                return;
            }

            ravager.Rigidbody2D.linearVelocity = diveVelocity;

            // Kiểm tra va chạm với player
            if (!hasHit && ravager.HasTargetInAttackRange())
            {
                HitPlayer();
                hasHit = true;
            }

            // Nếu va chạm ground thì chuyển phase mặt đất luôn
            if (HasHitGround() || Vector2.Distance(ravager.transform.position, diveTargetPosition) <= 0.25f)
            {
                ravager.Rigidbody2D.linearVelocity = Vector2.zero;
                ravager.SetStateToGroundCombat();
            }
        }

        public override void Exit()
        {
            ravager.StopHorizontalMovement();
        }

        private void HitPlayer()
        {
            Collider2D playerCollider = ravager.Target != null
                ? ravager.Target.GetComponent<Collider2D>()
                : null;
            if (playerCollider != null)
            {
                PlayerHitResolver.TryApplyHitToPlayer(
                    playerCollider,
                    ravager.DiveDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(ravager),
                    ravager.transform,
                    ravager.Target.position - ravager.transform.position);
            }

            ravager.PlayAttackAnimation();
        }

        private bool HasHitGround()
        {
            Bounds bounds = ravager.Collider2D.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                Vector2.down,
                0.3f,
                ravager.EnvironmentLayer
            );

            return hit.collider != null && !hit.collider.isTrigger;
        }

        private bool ShouldLandOnGround(Vector2 diveVelocity, out RaycastHit2D groundHit)
        {
            groundHit = default;

            if (diveVelocity.y > 0f)
            {
                return false;
            }

            Bounds bounds = ravager.Collider2D.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
            float checkDistance = Mathf.Abs(diveVelocity.y) * Time.fixedDeltaTime + 0.15f;

            groundHit = Physics2D.Raycast(
                origin,
                Vector2.down,
                checkDistance,
                ravager.EnvironmentLayer
            );

            return groundHit.collider != null && !groundHit.collider.isTrigger;
        }

        private void SnapToGround(RaycastHit2D groundHit)
        {
            Bounds bounds = ravager.Collider2D.bounds;
            Vector3 position = ravager.transform.position;
            position.y = groundHit.point.y + bounds.extents.y;
            ravager.transform.position = position;
        }
    }
}
