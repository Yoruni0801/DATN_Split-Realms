using UnityEngine;

namespace Bao.Enemies
{
    public class DusthornRollState : BaoEnemyState
    {
        private readonly DusthornEnemy dusthorn;
        private float startTime;
        private float rollDirection;
        private bool isRollingStarted;
        private readonly Collider2D[] overlapResults = new Collider2D[8];

        public bool IsRolling => isRollingStarted;

        public float WindupProgress => dusthorn.RollAnticipationDuration <= 0f
            ? 1f
            : Mathf.Clamp01((Time.time - startTime) / dusthorn.RollAnticipationDuration);

        public DusthornRollState(BaoEnemyBase enemy) : base(enemy)
        {
            dusthorn = enemy as DusthornEnemy;
        }

        public override void Enter()
        {
            startTime = Time.time;
            isRollingStarted = false;

            dusthorn.DisableCollisionTag();

            if (dusthorn.Target != null)
                dusthorn.FacePosition(dusthorn.Target.position);

            rollDirection = dusthorn.FacingDirection;

            dusthorn.MarkAttackStarted();
            dusthorn.StartWarningRoutine();

            // Giữ walk trong lúc lấy đà
            dusthorn.SetLocomotionAnimation(true);

            dusthorn.VfxController?.TriggerWindupStart(dusthorn.RollAnticipationDuration);
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;

            if (!isRollingStarted)
            {
                if (dusthorn.Target != null)
                {
                    dusthorn.FacePosition(dusthorn.Target.position);
                    rollDirection = dusthorn.FacingDirection;
                }

                if (elapsed >= dusthorn.RollAnticipationDuration)
                {
                    isRollingStarted = true;
                    dusthorn.StopWarningRoutine();

                    if (dusthorn.Animator != null)
                    {
                        dusthorn.SetLocomotionAnimation(false);
                        dusthorn.Animator.SetTrigger("Roll");
                        dusthorn.Animator.SetBool("isRoll", true);
                        dusthorn.Animator.SetBool("isIdle", false);
                    }

                    dusthorn.VfxController?.TriggerRollStart(rollDirection, dusthorn.RollDuration);
                }
            }

            if (elapsed > dusthorn.RollDuration + dusthorn.RollAnticipationDuration)
            {
                if (dusthorn.HasTargetInDetectionRange())
                    dusthorn.SetStateToChase();
                else
                    dusthorn.SetStateToPatrol();
            }
        }

        public override void PhysicsUpdate()
        {
            if (isRollingStarted)
            {
                dusthorn.Rigidbody2D.linearVelocity = new Vector2(
                    rollDirection * dusthorn.RollSpeed,
                    dusthorn.Rigidbody2D.linearVelocity.y);

                Collider2D dusthornCollider = dusthorn.Collider2D;
                if (dusthornCollider == null)
                    return;

                ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                int count = dusthornCollider.Overlap(filter, overlapResults);
                for (int i = 0; i < count; i++)
                {
                    Collider2D result = overlapResults[i];
                    if (result == null || result == dusthornCollider)
                        continue;

                    if (dusthorn.TryDealRollDamage(result))
                        break;
                }
            }
            else
            {
                dusthorn.Rigidbody2D.linearVelocity = new Vector2(
                    rollDirection * dusthorn.RollWindupSpeed,
                    dusthorn.Rigidbody2D.linearVelocity.y);
            }
        }

        public override void Exit()
        {
            dusthorn.StopWarningRoutine();

            if (dusthorn.Animator != null)
                dusthorn.Animator.SetBool("isRoll", false);

            dusthorn.StopHorizontalMovement();
            dusthorn.VfxController?.TriggerRollEnd();
        }
    }
}
