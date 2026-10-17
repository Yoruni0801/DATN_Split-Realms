using UnityEngine;

namespace Bao.Enemies
{
    public class ChubblooAttackState : BaoEnemyState
    {
        private readonly ChubblooEnemy chubbloo;
        private float startTime;
        private bool hasShot;

        public ChubblooAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            chubbloo = enemy as ChubblooEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();
            startTime = Time.time;
            hasShot = false;
            
            chubbloo.PrepareForAttack();
            
            if (chubbloo.Target != null)
            {
                chubbloo.FacePosition(chubbloo.Target.position);
            }
            
            chubbloo.MarkAttackStarted();
            
            chubbloo.PlayAttackAnimation();
            chubbloo.VfxController?.TriggerChargeStart(chubbloo.ShootDelay);
        }

        public override void Exit()
        {
            chubbloo.StopWarningRoutine();
            chubbloo.VfxController?.TriggerChargeEnd();
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;
            
            if (!hasShot && elapsed > chubbloo.ShootDelay)
            {
                chubbloo.StopWarningRoutine();
                chubbloo.SpawnProjectile();
                hasShot = true;
            }
            
            // Wait for both the visual attack duration and the shooting delay to complete
            float requiredDuration = Mathf.Max(chubbloo.AttackStateDuration, chubbloo.ShootDelay + 0.1f);
            if (elapsed > requiredDuration)
            {
                if (chubbloo.HasTargetInDetectionRange())
                    chubbloo.SetStateToChase();
                else
                    chubbloo.SetStateToPatrol();
            }
        }
    }
}
