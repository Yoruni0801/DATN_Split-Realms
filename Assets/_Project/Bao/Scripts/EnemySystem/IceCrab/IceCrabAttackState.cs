using UnityEngine;

namespace Bao.Enemies
{
    public class IceCrabAttackState : BaoEnemyState
    {
        private readonly IceCrabEnemy iceCrab;
        private float animationStartTime;
        private bool attackFired;
        private bool attackStopped;

        public IceCrabAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            iceCrab = enemy as IceCrabEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();
            attackFired = false;
            attackStopped = false;

            iceCrab.StopHorizontalMovement();
            iceCrab.SetLocomotionAnimation(false);

            if (iceCrab.Target != null)
            {
                iceCrab.FacePosition(iceCrab.Target.position);
            }

            iceCrab.MarkAttackStarted();
            iceCrab.PlayAttackAnimation();
            animationStartTime = Time.time;

            iceCrab.VfxController?.TriggerChargeStart(iceCrab.FireTime);
        }

        public override void LogicUpdate()
        {
            float animationElapsed = Time.time - animationStartTime;

            if (!attackFired && animationElapsed >= iceCrab.FireTime)
            {
                iceCrab.StartAttackVFX();

                float blowDuration = Mathf.Max(0.05f, iceCrab.FireEndTime - iceCrab.FireTime);
                iceCrab.VfxController?.TriggerBubbleBlowStart(blowDuration);

                attackFired = true;
            }

            if (attackFired && !attackStopped && animationElapsed >= iceCrab.FireEndTime)
            {
                iceCrab.StopAttackVFX();

                iceCrab.VfxController?.TriggerBubbleBlowStop();

                attackStopped = true;
            }

            if (animationElapsed >= iceCrab.AttackAnimationDuration)
            {
                if (iceCrab.HasTargetInDetectionRange())
                {
                    iceCrab.SetStateToChase();
                }
                else
                {
                    iceCrab.SetStateToPatrol();
                }
            }
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            iceCrab.StopAttackAnimation();
            iceCrab.StopAttackVFX();

            iceCrab.VfxController?.TriggerAttackEnd();
        }
    }
}
