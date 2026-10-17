using UnityEngine;

namespace Bao.Bosses
{
    public abstract class NidusBossState
    {
        protected NidusCocoonBoss boss;

        public NidusBossState(NidusCocoonBoss boss)
        {
            this.boss = boss;
        }

        public virtual void Enter() { }
        public virtual void LogicUpdate() { }
        public virtual void PhysicsUpdate() { }
        public virtual void Exit() { }
    }
}
