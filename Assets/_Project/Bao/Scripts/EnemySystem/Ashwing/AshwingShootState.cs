using UnityEngine;

namespace Bao.Enemies
{
    public class AshwingShootState : BaoEnemyState
    {
        private readonly AshwingEnemy ashwing;
        private float startTime;
        private bool hasShot;

        public AshwingShootState(BaoEnemyBase enemy) : base(enemy)
        {
            ashwing = enemy as AshwingEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();
            startTime = Time.time;
            hasShot = false;

            ashwing.StopHorizontalMovement();

            if (ashwing.Target != null)
            {
                ashwing.FacePosition(ashwing.Target.position);
            }

            ashwing.MarkAttackStarted();

            if (ashwing.Animator != null)
            {
                ashwing.PlayAttackAnimation();
            }

            // Cinder Razor anticipation: use the existing shootDelay as the
            // charge window so the enemy stays readable and fair.
            ashwing.BeginChargeVFX();
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;

            if (!hasShot && elapsed >= ashwing.ShootDelay)
            {
                ashwing.SpawnProjectile();
                hasShot = true;
            }

            float requiredDuration = Mathf.Max(
                ashwing.AttackStateDuration,
                ashwing.ShootDelay + 0.16f);

            if (elapsed > requiredDuration)
            {
                if (ashwing.HasTargetInDetectionRange())
                    ashwing.SetStateToChase();
                else
                    ashwing.SetStateToPatrol();
            }
        }
    }
}
