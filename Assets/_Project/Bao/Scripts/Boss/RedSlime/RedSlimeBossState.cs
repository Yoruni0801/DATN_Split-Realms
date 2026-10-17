using UnityEngine;

namespace Bao.Bosses
{
    public abstract class RedSlimeBossState
    {
        protected RedSlimeBoss boss;

        public RedSlimeBossState(RedSlimeBoss boss)
        {
            this.boss = boss;
        }

        public virtual void Enter() { }
        public virtual void LogicUpdate() { }
        public virtual void PhysicsUpdate() { }
        public virtual void Exit() { }
    }
}
