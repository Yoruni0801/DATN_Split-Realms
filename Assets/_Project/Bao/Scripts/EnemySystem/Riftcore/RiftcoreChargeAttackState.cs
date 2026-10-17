using UnityEngine;

namespace Bao.Enemies
{
    public class RiftcoreChargeAttackState : BaoEnemyState
    {
        private readonly RiftcoreEnemy riftcore;
        private float startTime;
        private bool hasAttacked;

        public RiftcoreChargeAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            riftcore = enemy as RiftcoreEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();
            startTime = Time.time;
            hasAttacked = false;

            riftcore.StopHorizontalMovement();
            riftcore.MarkAttackStarted();
            riftcore.BeginChargeVFX();
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;
            riftcore.SetChargeVFXProgress(elapsed / riftcore.ChargeDuration);

            if (!hasAttacked && elapsed > riftcore.ChargeDuration)
            {
                riftcore.PlayAttackAnimation();
                riftcore.ExecuteAoeAttack();
                hasAttacked = true;
            }

            if (hasAttacked && elapsed > riftcore.ChargeDuration + 0.5f)
            {
                if (riftcore.HasTargetInDetectionRange())
                    riftcore.SetStateToChase();
                else
                    riftcore.SetStateToPatrol();
            }
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            riftcore.EndChargeVFX();
        }
    }
}
