using UnityEngine;

namespace Quoc.Boss
{
    public class BossDeadState : BossState
    {
        public BossDeadState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            boss.Movement.StopMovement();
            boss.SetInvulnerable(true);
            boss.PlayDeathAnimation();
            boss.DisablePhysics();
        }
    }
}
