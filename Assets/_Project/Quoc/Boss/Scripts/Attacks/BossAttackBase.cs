using System;
using UnityEngine;

namespace Quoc.Boss
{
    /// <summary>
    /// Lớp cơ sở cho tất cả các Logic Attack cụ thể của Boss
    /// </summary>
    public abstract class BossAttackBase : MonoBehaviour
    {
        public abstract string AttackName { get; }

        protected BossController boss;
        protected BossAttackData attackData;
        protected Action onAttackCompleted;
        protected bool isExecuting;

        public virtual void Execute(BossController boss, BossAttackData data, Action onComplete)
        {
            this.boss = boss;
            this.attackData = data;
            this.onAttackCompleted = onComplete;
            this.isExecuting = true;
        }

        public virtual void UpdateAttack() { }
        public virtual void FixedUpdateAttack() { }

        public virtual void Cleanup()
        {
            isExecuting = false;
            StopAllCoroutines();
        }

        protected void FinishAttack()
        {
            if (!isExecuting) return;
            isExecuting = false;
            onAttackCompleted?.Invoke();
        }
    }
}
