using UnityEngine;

namespace Bao.Enemies
{
    public sealed class MoonflutterDiveBombState : BaoEnemyState
    {
        private enum DivePhase
        {
            Approach,
            Windup,
            DashLocked
        }

        private readonly MoonflutterEnemy moonflutter;
        private DivePhase phase;
        private Vector2 lockedTargetPosition;
        private Vector2 lockedDashDirection;
        private Transform cachedTarget;
        private Collider2D cachedTargetCollider;
        private float phaseStartTime;
        private bool hasDetonated;

        public MoonflutterDiveBombState(BaoEnemyBase enemy) : base(enemy)
        {
            moonflutter = enemy as MoonflutterEnemy;
        }

        public override void Enter()
        {
            hasDetonated = false;
            phase = DivePhase.Approach;
            phaseStartTime = Time.time;
            lockedTargetPosition = Enemy.CenterPosition;
            lockedDashDirection = Vector2.right * Enemy.FacingDirection;
            cachedTarget = null;
            cachedTargetCollider = null;

            Enemy.SetFlyingAnimation(true);
            Enemy.SetLocomotionAnimation(true);

            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.gravityScale = 0f;
            }
        }

        public override void LogicUpdate()
        {
            if (hasDetonated)
            {
                return;
            }

            switch (phase)
            {
                case DivePhase.Approach:
                    UpdateApproachLogic();
                    break;

                case DivePhase.Windup:
                    if (Time.time - phaseStartTime >= moonflutter.LockWindupDuration)
                    {
                        BeginLockedDash();
                    }
                    break;

                case DivePhase.DashLocked:
                    if (ReachedLockedPoint() || Time.time - phaseStartTime >= moonflutter.MaxLockedDashDuration)
                    {
                        Detonate();
                    }
                    break;
            }
        }

        public override void PhysicsUpdate()
        {
            if (hasDetonated || Enemy.Rigidbody2D == null)
            {
                return;
            }

            switch (phase)
            {
                case DivePhase.Approach:
                    MoveTowardCurrentTarget();
                    break;

                case DivePhase.Windup:
                    Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
                    break;

                case DivePhase.DashLocked:
                    Enemy.Rigidbody2D.linearVelocity = lockedDashDirection * moonflutter.LockedDashSpeed;
                    break;
            }
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            Enemy.SetLocomotionAnimation(false);

            if (moonflutter.Animator != null)
            {
                moonflutter.Animator.SetBool("isSwelling", false);
            }

            if (!hasDetonated && Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }

        private void UpdateApproachLogic()
        {
            if (Enemy.Target == null || !Enemy.HasTargetInDetectionRange())
            {
                Enemy.SetStateToPatrol();
                return;
            }

            if (ShouldLockTarget())
            {
                BeginWindup();
            }
        }

        private void MoveTowardCurrentTarget()
        {
            if (Enemy.Target == null)
            {
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 aimPosition = GetTargetAimPosition();
            Vector2 direction = (aimPosition - (Vector2)Enemy.CenterPosition).normalized;
            Enemy.FacePosition(aimPosition);
            Enemy.Rigidbody2D.linearVelocity = direction * (Enemy.MoveSpeed * moonflutter.ApproachSpeedMultiplier);
        }

        private bool ShouldLockTarget()
        {
            if (Enemy.Target == null)
            {
                return false;
            }

            float lockDistance = Mathf.Max(moonflutter.LockTargetDistance, moonflutter.DetonateDistance);
            Vector2 offset = GetTargetAimPosition() - (Vector2)Enemy.CenterPosition;
            return offset.sqrMagnitude <= lockDistance * lockDistance;
        }

        private void BeginWindup()
        {
            phase = DivePhase.Windup;
            phaseStartTime = Time.time;
            lockedTargetPosition = Enemy.Target != null ? GetTargetAimPosition() : (Vector2)Enemy.CenterPosition;

            Enemy.MarkAttackStarted();
            Enemy.StartWarningRoutine();
            Enemy.SetLocomotionAnimation(false);

            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
            }

            if (moonflutter.Animator != null)
            {
                moonflutter.PlayAttackAnimation();
                moonflutter.Animator.SetTrigger("Swell");
                moonflutter.Animator.SetBool("isSwelling", true);
            }
        }

        private void BeginLockedDash()
        {
            phase = DivePhase.DashLocked;
            phaseStartTime = Time.time;

            Vector2 direction = lockedTargetPosition - (Vector2)Enemy.CenterPosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right * Enemy.FacingDirection;
            }

            lockedDashDirection = direction.normalized;
            Enemy.FacePosition((Vector2)Enemy.CenterPosition + lockedDashDirection);
            Enemy.SetLocomotionAnimation(true);
        }

        private bool ReachedLockedPoint()
        {
            float threshold = Mathf.Max(0.01f, moonflutter.DetonateDistance);
            Vector2 offset = (Vector2)Enemy.CenterPosition - lockedTargetPosition;
            if (offset.sqrMagnitude <= threshold * threshold)
            {
                return true;
            }

            Vector2 remaining = lockedTargetPosition - (Vector2)Enemy.CenterPosition;
            return Vector2.Dot(remaining, lockedDashDirection) <= 0f;
        }

        private void Detonate()
        {
            hasDetonated = true;

            if (Enemy.Rigidbody2D != null)
            {
                Enemy.Rigidbody2D.linearVelocity = Vector2.zero;
            }

            moonflutter.Detonate();
        }

        private Vector2 GetTargetAimPosition()
        {
            if (Enemy.Target == null)
            {
                return Enemy.CenterPosition;
            }

            if (cachedTarget != Enemy.Target)
            {
                cachedTarget = Enemy.Target;
                cachedTargetCollider = cachedTarget.GetComponent<Collider2D>();

                if (cachedTargetCollider == null)
                {
                    cachedTargetCollider = cachedTarget.GetComponentInChildren<Collider2D>();
                }
            }

            if (cachedTargetCollider != null && cachedTargetCollider.enabled)
            {
                return cachedTargetCollider.bounds.center;
            }

            return (Vector2)Enemy.Target.position + Vector2.up * moonflutter.TargetAimFallbackHeight;
        }
    }
}
