using UnityEngine;

namespace Bao.Enemies
{
    public class WildrootCloseState : BaoEnemyState
    {
        private readonly WildrootEnemy wildroot;
        private float startTime;

        public WildrootCloseState(BaoEnemyBase enemy) : base(enemy)
        {
            wildroot = enemy as WildrootEnemy;
        }

        public override void Enter()
        {
            startTime = Time.time;
            wildroot.BeginWhipChargeVFX();

            if (wildroot.Animator != null)
            {
                wildroot.Animator.SetTrigger("Close");
                wildroot.Animator.SetBool("isClose", true);
            }
        }

        public override void LogicUpdate()
        {
            if (Time.time > startTime + wildroot.TrapActiveTime + wildroot.TrapCooldown)
            {
                wildroot.SetStateToIdle();
            }
        }
    }
}