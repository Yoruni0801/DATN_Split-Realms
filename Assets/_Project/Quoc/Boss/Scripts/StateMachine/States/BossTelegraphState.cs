using UnityEngine;

namespace Quoc.Boss
{
    public class BossTelegraphState : BossState
    {
        private float timer;

        public BossTelegraphState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            if (!boss.IsAggroEnabled)
            {
                stateMachine.ChangeState(boss.IdleState);
                return;
            }

            timer = 0f;
            boss.Movement.StopMovement();
            boss.Movement.FacePlayer();
            boss.PlayTelegraphAnimation();

            if (boss.TelegraphEffect != null)
            {
                boss.TelegraphEffect.SetActive(true);
            }
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
            BossAttackData currentAttack = boss.CurrentAttackData;
            float duration = currentAttack != null ? currentAttack.telegraphDuration : 0.5f;

            if (timer >= duration)
            {
                stateMachine.ChangeState(boss.AttackState);
            }
        }

        public override void Exit()
        {
            if (boss.TelegraphEffect != null)
            {
                boss.TelegraphEffect.SetActive(false);
            }
        }
    }
}
