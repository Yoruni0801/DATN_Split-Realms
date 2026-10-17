using UnityEngine;

namespace Quoc.Boss
{
    public class BossPhaseTransitionState : BossState
    {
        private float transitionTimer;
        private float transitionDuration = 2.5f;

        public BossPhaseTransitionState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            transitionTimer = 0f;
            boss.Movement.StopMovement();
            boss.SetInvulnerable(true);
            boss.PlayPhaseTransitionAnimation();

            if (boss.PhaseTransitionEffect != null)
            {
                boss.PhaseTransitionEffect.SetActive(true);
            }

            boss.PerformPhaseTransition();
        }

        public override void UpdateState()
        {
            transitionTimer += Time.deltaTime;
            if (transitionTimer >= transitionDuration)
            {
                boss.SetInvulnerable(false);
                stateMachine.ChangeState(boss.IdleState);
            }
        }

        public override void Exit()
        {
            if (boss.PhaseTransitionEffect != null)
            {
                boss.PhaseTransitionEffect.SetActive(false);
            }
        }
    }
}
