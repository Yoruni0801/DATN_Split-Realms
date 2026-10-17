using UnityEngine;

namespace Bao.Bosses
{
    public abstract class DreadwormBossState
    {
        protected DreadwormBoss boss;

        public DreadwormBossState(DreadwormBoss boss)
        {
            this.boss = boss;
        }

        public virtual void Enter() { }
        public virtual void LogicUpdate() { }
        public virtual void PhysicsUpdate() { }
        public virtual void Exit() { }
    }
}
