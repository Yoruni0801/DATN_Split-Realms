using UnityEngine;

namespace Bao.Enemies
{
    public class WildrootIdleState : BaoEnemyState
    {
        private readonly WildrootEnemy wildroot;

        public WildrootIdleState(BaoEnemyBase enemy) : base(enemy)
        {
            wildroot = enemy as WildrootEnemy;
        }

        public override void Enter()
        {
            wildroot.StopHorizontalMovement();
            wildroot.SetIdleAnimation(true);
            if (wildroot.Animator != null)
            {
                wildroot.Animator.SetBool("isClose", false);
            }
        }
    }
}
