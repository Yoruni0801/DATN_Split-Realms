using UnityEngine;

namespace Quoc.Boss
{
    public class BossRecoveryState : BossState
    {
        private float timer;

        public BossRecoveryState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            if (!boss.IsAggroEnabled)
            {
                stateMachine.ChangeState(boss.IdleState);
                return;
            }

            timer = 0f;
            boss.Movement.StopMovement();
            boss.PlayRecoveryAnimation();
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
                stateMachine.ChangeState(boss.IdleState);
                return;
            }

            if (boss.CheckPendingPhaseTransition())
            {
                stateMachine.ChangeState(boss.PhaseTransitionState);
                return;
            }

            timer += Time.deltaTime;

            BossAttackData attack = boss.CurrentAttackData;
            float recoveryTime = attack != null ? attack.recoveryDuration : 0.8f;

            if (boss.CurrentPhase == BossPhase.Phase3)
            {
                recoveryTime *= 0.75f;
            }

            if (timer >= recoveryTime)
            {
                stateMachine.ChangeState(boss.IdleState);
            }
        }
    }
}
