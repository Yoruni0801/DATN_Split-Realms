using UnityEngine;

namespace Bao.Enemies
{
    public class BonewheelAttackState : BaoEnemyState
    {
        private readonly BonewheelEnemy bonewheel;
        private float stateStartTime;
        private bool hasDealtDamage;
        private bool attackPlayed;
        private Vector3 targetLockPosition;

        public BonewheelAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            bonewheel = enemy as BonewheelEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();

            stateStartTime = Time.time;
            hasDealtDamage = false;
            attackPlayed = false;
            
            bonewheel.StopHorizontalMovement();
            bonewheel.SetLocomotionAnimation(false);

            if (bonewheel.Target != null)
            {
                targetLockPosition = bonewheel.Target.position;
            }
            else
            {
                targetLockPosition = bonewheel.transform.position;
            }
            bonewheel.FacePosition(targetLockPosition);
            bonewheel.BeginWhipChargeVFX(bonewheel.PreAttackDelay + bonewheel.AttackDelay);
        }

        public override void LogicUpdate()
        {
            float timeInState = Time.time - stateStartTime;

            if (timeInState >= bonewheel.PreAttackDelay && !attackPlayed)
            {
                bonewheel.PlayAttackAnimation();
                attackPlayed = true;
            }

            if (attackPlayed)
            {
                float timeSinceAttackAnim = timeInState - bonewheel.PreAttackDelay;
                if (timeSinceAttackAnim >= bonewheel.AttackDelay && !hasDealtDamage)
                {
                    DealDamage();
                    hasDealtDamage = true;
                }
            }
            
            if (timeInState > bonewheel.PreAttackDelay + bonewheel.AttackStateDuration)
            {
                if (bonewheel.HasTargetInDetectionRange())
                {
                    bonewheel.SetStateToChase();
                }
                else
                {
                    bonewheel.SetStateToPatrol();
                }
            }
        }
        private void DealDamage()
        {
            bonewheel.PlayWhipThrustVFX();

            Vector2 attackCenter = (Vector2)bonewheel.CenterPosition + new Vector2(bonewheel.FacingDirection * (bonewheel.AttackRadius * 0.5f), 0f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackCenter, bonewheel.AttackRadius);
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Player")) continue;

                IncomingAttackResolution hitResult = PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    bonewheel.AttackDamage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(bonewheel),
                    bonewheel.transform,
                    hit.transform.position - bonewheel.transform.position);

                if (hitResult != IncomingAttackResolution.Ignored)
                {
                    bonewheel.PlayWhipImpactVFX(hit.transform.position);
                }
            }
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            bonewheel.CancelWhipAttackVFX();
            bonewheel.MarkAttackStarted();
        }
    }
}