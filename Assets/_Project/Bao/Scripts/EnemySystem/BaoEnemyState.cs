using UnityEngine;

namespace Bao.Enemies
{
    public abstract class BaoEnemyState
    {
        protected BaoEnemyBase Enemy { get; private set; }

        protected BaoEnemyState(BaoEnemyBase enemy)
        {
            Enemy = enemy;
        }

        public virtual void Enter() { }

        public virtual void LogicUpdate() { }

        public virtual void PhysicsUpdate() { }

        public virtual void LateUpdate() { }

        public virtual void Exit() { }
    }
}