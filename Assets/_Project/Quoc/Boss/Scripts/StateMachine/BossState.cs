using UnityEngine;

namespace Quoc.Boss
{
    /// <summary>
    /// Lớp cơ sở cho tất cả các State của Boss
    /// </summary>
    public abstract class BossState
    {
        protected BossController boss;
        protected BossStateMachine stateMachine;

        public BossState(BossController boss, BossStateMachine stateMachine)
        {
            this.boss = boss;
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void UpdateState() { }
        public virtual void FixedUpdateState() { }
        public virtual void Exit() { }
    }
}
