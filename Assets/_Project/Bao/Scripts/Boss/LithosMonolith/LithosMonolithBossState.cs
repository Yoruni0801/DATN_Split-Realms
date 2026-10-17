using UnityEngine;

namespace Bao.Bosses
{
    public abstract class LithosMonolithBossState
    {
        protected LithosMonolithBoss boss;

        public LithosMonolithBossState(LithosMonolithBoss boss)
        {
            this.boss = boss;
        }

        public virtual void Enter() { }
        public virtual void LogicUpdate() { }
        public virtual void PhysicsUpdate() { }
        public virtual void Exit() { }
    }
}
