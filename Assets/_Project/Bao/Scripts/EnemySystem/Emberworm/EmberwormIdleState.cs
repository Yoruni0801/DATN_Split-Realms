using UnityEngine;

namespace Bao.Enemies
{
    public class EmberwormIdleState : BaoEnemyState
    {
        private readonly EmberwormEnemy worm;
        private float stateTimer;
        private float currentDelay;

        public EmberwormIdleState(BaoEnemyBase enemy) : base(enemy)
        {
            worm = enemy as EmberwormEnemy;
        }

        public override void Enter()
        {
            worm.StopHorizontalMovement();
            worm.SetIdleAnimation(true);
            
            stateTimer = 0f;
            currentDelay = worm.IsFirstEmerge ? worm.DelayBeforeEmerge : worm.LoopDelay;
            worm.IsFirstEmerge = false;
            worm.transform.position = worm.HiddenPosition;
        }

        public override void LogicUpdate()
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= currentDelay)
            {
                worm.StateMachine.SetState(worm.EmergeState);
            }
        }
    }
}
