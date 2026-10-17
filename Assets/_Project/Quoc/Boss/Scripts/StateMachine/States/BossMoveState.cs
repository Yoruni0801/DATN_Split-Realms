using UnityEngine;

namespace Quoc.Boss
{
    public class BossMoveState : BossState
    {
        public BossMoveState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            stateMachine.ChangeState(boss.IdleState);
        }
    }
}
