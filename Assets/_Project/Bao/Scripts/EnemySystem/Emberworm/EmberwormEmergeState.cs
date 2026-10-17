using UnityEngine;

namespace Bao.Enemies
{
    public class EmberwormEmergeState : BaoEnemyState
    {
        private readonly EmberwormEnemy worm;
        private float stateTimer;

        public EmberwormEmergeState(BaoEnemyBase enemy) : base(enemy)
        {
            worm = enemy as EmberwormEnemy;
        }

        public override void Enter()
        {
            stateTimer = 0f;
            worm.SetIdleAnimation(false);
        }

        public override void LogicUpdate()
        {
            stateTimer += Time.deltaTime;
            
            float progress = Mathf.Clamp01(stateTimer / worm.EmergeTime);
            worm.transform.position = Vector3.Lerp(worm.HiddenPosition, worm.EmergingPosition, progress);

            if (stateTimer >= worm.EmergeTime)
            {
                worm.StateMachine.SetState(worm.ActiveState);
            }
        }
    }
}
