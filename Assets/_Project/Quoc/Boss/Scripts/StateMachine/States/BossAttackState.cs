using UnityEngine;

namespace Quoc.Boss
{
    public class BossAttackState : BossState
    {
        private bool attackFinished;
        private float attackTimer;
        private const float MAX_ATTACK_DURATION = 6.0f; // Timeout phòng hộ để không bao giờ bị đơ

        public BossAttackState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            if (!boss.IsAggroEnabled)
            {
                stateMachine.ChangeState(boss.IdleState);
                return;
            }

            attackFinished = false;
            attackTimer = 0f;

            if (boss.CurrentAttackData != null)
            {
                boss.CurrentAttackData.Use(Time.time);
            }

            boss.PlayAttackAnimation();
            boss.ExecuteCurrentAttack(() => attackFinished = true);
        }

        public override void UpdateState()
        {
            if (boss.IsDead)
            {
                stateMachine.ChangeState(boss.DeadState);
                return;
            }

            if (!boss.IsAggroEnabled)
            {
                boss.CleanupCurrentAttack();
                stateMachine.ChangeState(boss.IdleState);
                return;
            }

            attackTimer += Time.deltaTime;
            boss.UpdateCurrentAttack();

            if (attackFinished || attackTimer >= MAX_ATTACK_DURATION)
            {
                stateMachine.ChangeState(boss.RecoveryState);
            }
        }

        public override void FixedUpdateState()
        {
            boss.FixedUpdateCurrentAttack();
        }

        public override void Exit()
        {
            boss.CleanupCurrentAttack();
        }
    }
}
